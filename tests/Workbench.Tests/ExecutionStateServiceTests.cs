using System.Net;
using System.Text;
using System.Text.Json;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class ExecutionStateServiceTests
{
    private static readonly DateTimeOffset observedAt = new(2026, 11, 22, 1, 2, 3, TimeSpan.Zero);

    [TestMethod]
    public async Task RefreshAsync_WritesVersionedSnapshotAndRetainsObservationsWhenProviderBecomesUnavailableAsync()
    {
        using var repo = new TempRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var created = WorkItemService.CreateItem(
            repo.Path,
            config,
            "work_item",
            "Observe execution state",
            "in_progress",
            "high",
            "platform");
        var workItem = WorkItemService.LoadItem(created.Path);
        Assert.IsNotNull(workItem);

        var repository = new GithubRepoRef("github.com", "incursa", "workbench");
        var references = new[]
        {
            ExecutionStateReferenceParser.ParsePullRequest("incursa/workbench#616", repository),
            ExecutionStateReferenceParser.ParseRelease("incursa/workbench@v1.2.3", repository),
            ExecutionStateReferenceParser.ParseDeployment("incursa/workbench#42", repository)
        };
        var provider = new FakeExecutionStateProvider(observedAt);
        var service = new ExecutionStateService(provider, new FixedTimeProvider(observedAt));

        var first = await service.RefreshAsync(repo.Path, workItem.Id, references);

        Assert.AreEqual(ExecutionStateService.CurrentSchemaVersion, first.Snapshot.SchemaVersion);
        Assert.HasCount(3, first.Snapshot.Observations);
        Assert.IsEmpty(first.Snapshot.Diagnostics);
        Assert.AreEqual("artifacts/execution-state/WI-WB-0001.json", first.Path);
        var snapshotPath = Path.Combine(repo.Path, first.Path);
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath)))
        {
            Assert.AreEqual(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual(workItem.Id, document.RootElement.GetProperty("workItemId").GetString());
        }

        var unavailableProvider = new FakeExecutionStateProvider(observedAt.AddHours(1))
        {
            Status = new ExecutionProviderStatus(false, "Offline test provider is unavailable.")
        };
        var unavailableService = new ExecutionStateService(
            unavailableProvider,
            new FixedTimeProvider(observedAt.AddHours(1)));

        var second = await unavailableService.RefreshAsync(repo.Path, workItem.Id, references);

        Assert.HasCount(3, second.Snapshot.Observations);
        Assert.HasCount(3, second.Snapshot.Diagnostics);
        Assert.IsTrue(second.Snapshot.Diagnostics.All(diagnostic => diagnostic.RetainedObservation));
        Assert.IsTrue(second.Snapshot.Diagnostics.All(diagnostic => diagnostic.PreviousObservedAtUtc == observedAt));
        Assert.IsTrue(second.Snapshot.Diagnostics.All(diagnostic => diagnostic.PreviousObservationAgeSeconds == 3600));
        Assert.IsTrue(second.Snapshot.Diagnostics.All(diagnostic => diagnostic.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(second.Snapshot.Observations.All(observation => observation.ObservedAtUtc == observedAt));
        Assert.AreEqual(0, unavailableProvider.ObserveCallCount);

        var shown = ExecutionStateService.Show(repo.Path, workItem.Id);
        Assert.IsNotNull(shown);
        Assert.AreEqual(second.Snapshot.RefreshedAtUtc, shown.Snapshot.RefreshedAtUtc);
    }

    [TestMethod]
    public async Task GithubProvider_ObservesPullRequestReleaseAndDeploymentWithoutMutationAsync()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Authorization)>();
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString()));
            var json = request.RequestUri.AbsolutePath switch
            {
                "/repos/incursa/workbench/pulls/616" =>
                    """{"state":"closed","merged_at":"2026-11-22T00:00:00Z","html_url":"https://github.com/incursa/workbench/pull/616","merge_commit_sha":"abc123"}""",
                "/repos/incursa/workbench/releases/tags/v1.2.3" =>
                    """{"draft":false,"prerelease":false,"published_at":"2026-11-22T00:00:00Z","html_url":"https://github.com/incursa/workbench/releases/tag/v1.2.3","target_commitish":"main"}""",
                "/repos/incursa/workbench/deployments/42" =>
                    """{"sha":"def456"}""",
                "/repos/incursa/workbench/deployments/42/statuses" =>
                    """[{"state":"success","environment_url":"https://example.test/deployment/42"}]""",
                _ => throw new AssertFailedException($"Unexpected request URI: {request.RequestUri}")
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }));
        var provider = new GithubExecutionStateProvider(client, "test-token", new FixedTimeProvider(observedAt));
        var repository = new GithubRepoRef("github.com", "incursa", "workbench");

        var pullRequest = await provider.ObserveAsync(
            ".",
            ExecutionStateReferenceParser.ParsePullRequest("616", repository));
        var release = await provider.ObserveAsync(
            ".",
            ExecutionStateReferenceParser.ParseRelease("v1.2.3", repository));
        var deployment = await provider.ObserveAsync(
            ".",
            ExecutionStateReferenceParser.ParseDeployment("42", repository));

        Assert.AreEqual(ExecutionStates.Succeeded, pullRequest.NormalizedState);
        Assert.AreEqual("merged", pullRequest.NativeState);
        Assert.AreEqual("abc123", pullRequest.Revision);
        Assert.AreEqual(ExecutionStates.Succeeded, release.NormalizedState);
        Assert.AreEqual("published", release.NativeState);
        Assert.AreEqual(ExecutionStates.Succeeded, deployment.NormalizedState);
        Assert.AreEqual("success", deployment.NativeState);
        Assert.AreEqual("def456", deployment.Revision);
        Assert.HasCount(4, requests);
        Assert.IsTrue(requests.All(request => request.Method == HttpMethod.Get));
        Assert.IsTrue(requests.All(request => string.Equals(request.Authorization, "Bearer test-token", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GithubProvider_MissingCredentialsReturnsClearUnavailableStatusAsync()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            throw new AssertFailedException("Unavailable provider must not issue an HTTP request.")));
        var provider = new GithubExecutionStateProvider(client, token: null, new FixedTimeProvider(observedAt));

        var status = await provider.CheckAvailabilityAsync(".");

        Assert.IsFalse(status.Available);
        StringAssert.Contains(status.Diagnostic, "WORKBENCH_GITHUB_TOKEN", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ReferenceParser_NormalizesUrlsAndQualifiedReferences()
    {
        var defaultRepo = new GithubRepoRef("github.com", "incursa", "workbench");

        var pullRequest = ExecutionStateReferenceParser.ParsePullRequest(
            "https://github.com/incursa/workbench/pull/616",
            defaultRepo);
        var release = ExecutionStateReferenceParser.ParseRelease("incursa/workbench@v1.2.3", defaultRepo);
        var deployment = ExecutionStateReferenceParser.ParseDeployment("incursa/workbench#42", defaultRepo);
        var apiDeployment = ExecutionStateReferenceParser.ParseDeployment(
            "https://api.github.com/repos/incursa/workbench/deployments/43",
            defaultRepo);

        Assert.AreEqual("github.com/incursa/workbench#616", pullRequest.ExternalReference);
        Assert.AreEqual("github.com/incursa/workbench@v1.2.3", release.ExternalReference);
        Assert.AreEqual("github.com/incursa/workbench#42", deployment.ExternalReference);
        Assert.AreEqual("github.com/incursa/workbench#43", apiDeployment.ExternalReference);
        Assert.Throws<InvalidOperationException>(() =>
            ExecutionStateReferenceParser.ParsePullRequest("incursa/workbench#not-a-number", defaultRepo));
    }

    private sealed class FakeExecutionStateProvider(DateTimeOffset observedAtUtc) : IExecutionStateProvider
    {
        public string Name => "fake";

        public ExecutionProviderStatus Status { get; set; } = new(true, null);

        public int ObserveCallCount { get; private set; }

        public Task<ExecutionProviderStatus> CheckAvailabilityAsync(
            string repoRoot,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.Status);
        }

        public Task<ExecutionObservation> ObserveAsync(
            string repoRoot,
            ExecutionStateReference reference,
            CancellationToken cancellationToken = default)
        {
            this.ObserveCallCount++;
            return Task.FromResult(new ExecutionObservation(
                this.Name,
                reference.Kind,
                reference.ExternalReference,
                reference.Url,
                ExecutionStates.Succeeded,
                "success",
                observedAtUtc,
                $"revision-{reference.Identifier}"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot()
        {
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "workbench-execution-state-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
