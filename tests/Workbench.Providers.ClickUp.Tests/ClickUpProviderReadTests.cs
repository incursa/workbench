using System.Net;
using Workbench.Core;

namespace Workbench.Providers.ClickUp.Tests;

[TestClass]
public sealed class ClickUpProviderReadTests
{
    [TestMethod]
    public async Task CapabilitiesReflectExplicitModeAndSafeBatchLimitAsync()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new AssertFailedException("Capability discovery must not contact ClickUp.")));
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(mode: ClickUpProviderMode.ReadOnly),
            httpClient,
            "secret-token");

        var capabilities = await provider.GetCapabilitiesAsync();

        Assert.AreEqual(ClickUpResourceProvider.ProviderId, capabilities.ProviderId);
        CollectionAssert.AreEqual(new[] { ClickUpResourceProvider.ResourceType }, capabilities.ResourceTypes.ToArray());
        Assert.AreEqual(1, capabilities.Batch.MaxItems);
        Assert.IsTrue(capabilities.Batch.AtomicApply);
        Assert.IsFalse(capabilities.SupportsIdempotency);
        Assert.IsFalse(capabilities.SupportsOptimisticConcurrency);
        CollectionAssert.DoesNotContain(capabilities.Operations.ToArray(), ResourceProviderOperations.PlanChanges);
        CollectionAssert.DoesNotContain(capabilities.Operations.ToArray(), ResourceProviderOperations.ApplyChanges);
    }

    [TestMethod]
    public async Task ProtocolHandshakeReturnsClickUpCapabilitiesAsync()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            throw new AssertFailedException("Handshake must not contact ClickUp.")));
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(mode: ClickUpProviderMode.ReadOnly),
            httpClient,
            "secret-token");
        var host = new ResourceProviderProtocolHost(provider);
        var body = JsonSerializer.SerializeToElement(
            new ProviderHandshakeRequest("test", "1", [ResourceProviderProtocol.Version1]),
            ProviderProtocolJsonContext.Default.ProviderHandshakeRequest);

        var response = await host.HandleAsync(new ProviderRequestEnvelope(
            ResourceProviderProtocol.Version1,
            "request-1",
            ResourceProviderOperations.Handshake,
            DeadlineUtc: DateTimeOffset.UtcNow.AddMinutes(1),
            IdempotencyKey: null,
            body));

        Assert.IsTrue(response.Ok);
        var handshake = response.Body!.Value.Deserialize(ProviderProtocolJsonContext.Default.ProviderHandshakeResponse);
        Assert.IsNotNull(handshake);
        Assert.AreEqual(ClickUpResourceProvider.ProviderId, handshake.Capabilities.ProviderId);
    }

    [TestMethod]
    public async Task GetMapsOnlyExecutionFieldsDeterministicallyAsync()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            return ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson());
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(),
            httpClient,
            "secret-token",
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));

        var response = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));

        Assert.HasCount(1, response.Resources);
        var document = response.Resources[0];
        var content = document.Content.Deserialize<ClickUpWorkItemExecutionContent>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(content);
        Assert.AreEqual("in progress", content.OperationalStatus);
        Assert.AreEqual(ClickUpPriority.High, content.Priority);
        CollectionAssert.AreEqual(new[] { "11", "27" }, content.AssigneeIds.ToArray());
        Assert.AreEqual("WI-WB-0042", content.LocalReference);
        Assert.AreEqual("1700000000000", content.SourceRevision);
        Assert.IsFalse(document.Content.TryGetProperty("description", out _));
        Assert.IsFalse(document.Content.TryGetProperty("specificationLinks", out _));
        Assert.IsTrue(document.VersionToken.StartsWith("clickup:1700000000000:", StringComparison.Ordinal));
        Assert.AreEqual("secret-token", handler.Requests[0].Authorization);
    }

    [TestMethod]
    public async Task QueryUsesOpaqueContinuationWithinClickUpPageAsync()
    {
        var pageJson = $$"""
            {
              "tasks": [
                {{ClickUpProviderTestSupport.TaskJson("task-1")}},
                {{ClickUpProviderTestSupport.TaskJson("task-2", dateUpdated: "1700000000001", localReference: "WI-WB-0043")}}
              ],
              "last_page": true
            }
            """;
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, pageJson));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(ClickUpProviderTestSupport.Options(), httpClient, "token");
        var first = await provider.QueryAsync(new ProviderQueryRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            Filter: null,
            PageSize: 1,
            ContinuationToken: null));

        Assert.HasCount(1, first.Resources);
        Assert.AreEqual("task-1", first.Resources[0].Address.ResourceId);
        Assert.IsNotNull(first.ContinuationToken);

        var second = await provider.QueryAsync(new ProviderQueryRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            Filter: null,
            PageSize: 1,
            first.ContinuationToken));

        Assert.HasCount(1, second.Resources);
        Assert.AreEqual("task-2", second.Resources[0].Address.ResourceId);
        Assert.IsNull(second.ContinuationToken);
        Assert.HasCount(2, handler.Requests);
        Assert.IsTrue(handler.Requests.All(request => request.Uri.Query.Contains("order_by=id", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RateLimitFailureIsStructuredRetryableAndRedactedAsync()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = ClickUpProviderTestSupport.Json(
                HttpStatusCode.TooManyRequests,
                """{ "err": "rate limited", "ECODE": "RATE_001" }""");
            response.Headers.Add("X-RateLimit-Limit", "100");
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", "1790000000");
            response.Headers.Add("Retry-After", "60");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(maximumHttpAttempts: 3),
            httpClient,
            "super-secret-token",
            delay: (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.GetAsync(new ProviderGetRequest(
                [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")])));

        Assert.AreEqual("clickup.rate-limit.exceeded", exception.Error.Code);
        Assert.IsTrue(exception.Error.Retryable);
        Assert.HasCount(1, exception.Diagnostics);
        Assert.AreEqual("0", exception.Diagnostics[0].Data!["rateLimitRemaining"]);
        Assert.AreEqual("60", exception.Diagnostics[0].Data!["retryAfter"]);
        Assert.HasCount(1, handler.Requests);
        Assert.DoesNotContain("super-secret-token", exception.ToString(), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ValidationRejectsTamperedSnapshotCoordinatesAsync()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(ClickUpProviderTestSupport.Options(), httpClient, "token");
        var document = (await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]))).Resources[0];
        var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new ResourceSnapshot(
            "snapshot-tampered",
            ClickUpResourceProvider.ResourceType,
            "list-42",
            now,
            new string('0', 64),
            Authoritative: true,
            new ResourceAuthorityDeclaration(
                ClickUpResourceProvider.ResourceType,
                "list-42",
                ClickUpResourceProvider.ProviderId,
                "wrong-epoch",
                now,
                DeclarationDigest: null),
            CompatibilityPin: null,
            [document],
            new ResourceProvenance(
                ClickUpResourceProvider.ProviderId,
                now,
                Actor: null,
                SourceRevision: null,
                new string('0', 64),
                TransferId: null,
                ApprovalReference: null));

        var result = await provider.ValidateAsync(new ProviderValidateRequest([snapshot]));

        Assert.IsFalse(result.Results[0].Valid);
        Assert.IsTrue(result.Results[0].Diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Code, "clickup.snapshot.authority.invalid", StringComparison.Ordinal)));
        Assert.IsTrue(result.Results[0].Diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Code, "clickup.snapshot.digest.invalid", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GetRejectsTaskOutsideConfiguredListAsync()
    {
        var json = ClickUpProviderTestSupport.TaskJson().Replace(
            "\"list\": { \"id\": \"list-42\" }",
            "\"list\": { \"id\": \"another-list\" }",
            StringComparison.Ordinal);
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, json));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(),
            httpClient,
            "token");

        ResourceProviderProtocolException exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.GetAsync(new ProviderGetRequest(
                [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")])));

        Assert.AreEqual("clickup.task.list-mismatch", exception.Error.Code);
    }
}
