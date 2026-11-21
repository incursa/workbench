using System.Security.Cryptography;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class FinishServiceTests
{
    [TestMethod]
    public async Task FinishAsync_CompletesCanonicalItemAndDeduplicatesEvidence()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);
        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Finish workflow",
            "planned",
            null,
            null);

        var options = new FinishOptions(
            created.Id,
            PullRequest: "https://github.com/incursa/workbench/pull/42",
            Evidence: new List<string> { "201 tests passed" },
            Note: "Released to production",
            ValidationProfile: "core");
        var unrelatedDocPath = Path.Combine(repo.Path, "runbooks", "README.md");
        var unrelatedDocBefore = await File.ReadAllTextAsync(unrelatedDocPath);

        var first = await FinishService.FinishAsync(repo.Path, WorkbenchConfig.Default, options);

        Assert.IsTrue(first.StatusChanged);
        Assert.IsTrue(first.EvidenceChanged);
        Assert.AreEqual("planned", first.PreviousStatus);
        Assert.AreEqual("complete", first.FinalStatus);
        Assert.AreEqual(created.Id, first.WorkItemId);
        Assert.AreEqual(
            Path.GetRelativePath(repo.Path, created.Path).Replace('\\', '/'),
            first.Path);

        var firstContent = await File.ReadAllTextAsync(created.Path);
        StringAssert.Contains(firstContent, "status: complete", StringComparison.Ordinal);
        StringAssert.Contains(firstContent, "- Pull request: https://github.com/incursa/workbench/pull/42", StringComparison.Ordinal);
        StringAssert.Contains(firstContent, "- Evidence: 201 tests passed", StringComparison.Ordinal);
        StringAssert.Contains(firstContent, "- Note: Released to production", StringComparison.Ordinal);

        var second = await FinishService.FinishAsync(repo.Path, WorkbenchConfig.Default, options);
        var secondContent = await File.ReadAllTextAsync(created.Path);

        Assert.IsFalse(second.StatusChanged);
        Assert.IsFalse(second.EvidenceChanged);
        Assert.AreEqual("complete", second.PreviousStatus);
        Assert.AreEqual(firstContent, secondContent);
        Assert.AreEqual(1, CountOccurrences(secondContent, "Pull request: https://github.com/incursa/workbench/pull/42"));
        Assert.AreEqual(1, CountOccurrences(secondContent, "Evidence: 201 tests passed"));
        Assert.AreEqual(1, CountOccurrences(secondContent, "Note: Released to production"));
        Assert.AreEqual(unrelatedDocBefore, await File.ReadAllTextAsync(unrelatedDocPath));
    }

    [TestMethod]
    public async Task FinishAsync_DryRunReportsChangesWithoutWriting()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);
        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Dry run finish",
            "in_progress",
            null,
            null);
        await NavigationService.SyncNavigationAsync(
            repo.Path,
            WorkbenchConfig.Default,
            includeDone: false,
            syncIssues: false,
            force: false,
            dryRun: false,
            syncDocs: false,
            normalizeItems: false);
        var before = SnapshotFiles(repo.Path);

        var result = await FinishService.FinishAsync(
            repo.Path,
            WorkbenchConfig.Default,
            new FinishOptions(
                created.Id,
                PullRequest: "https://github.com/incursa/workbench/pull/43",
                DryRun: true));

        Assert.IsTrue(result.DryRun);
        Assert.IsTrue(result.StatusChanged);
        Assert.IsTrue(result.EvidenceChanged);
        Assert.AreEqual("complete", result.FinalStatus);
        Assert.IsTrue(result.Navigation.Ok, result.Navigation.Error);
        Assert.IsGreaterThan(0, result.Navigation.IndexFilesUpdated);
        CollectionAssert.AreEquivalent(before, SnapshotFiles(repo.Path));

        var actual = await FinishService.FinishAsync(
            repo.Path,
            WorkbenchConfig.Default,
            new FinishOptions(
                created.Id,
                PullRequest: "https://github.com/incursa/workbench/pull/43"));

        Assert.AreEqual(result.Navigation.IndexFilesUpdated, actual.Navigation.IndexFilesUpdated);
        Assert.AreEqual(result.Validation.Ok, actual.Validation.Ok);
        CollectionAssert.AreEqual(result.Validation.Errors.ToArray(), actual.Validation.Errors.ToArray());
    }

    [TestMethod]
    public async Task FinishAsync_ReturnsStageResultsWhenNavigationFailsAfterMutation()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);
        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Structured failure",
            "in_progress",
            null,
            null);
        var config = WorkbenchConfig.Default with
        {
            Paths = WorkbenchConfig.Default.Paths with { DocsRoot = "blocked" }
        };
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "blocked"), "not a directory");

        var result = await FinishService.FinishAsync(
            repo.Path,
            config,
            new FinishOptions(created.Id));

        Assert.IsTrue(result.StatusChanged);
        Assert.AreEqual("complete", result.FinalStatus);
        Assert.IsFalse(result.Navigation.Ok);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Navigation.Error));
        Assert.IsTrue(result.Validation.Executed);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.RecommendedNextAction));
        StringAssert.Contains(await File.ReadAllTextAsync(created.Path), "status: complete", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task FinishAsync_RejectsNonHttpPullRequest()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);
        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Invalid PR finish",
            "planned",
            null,
            null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FinishService.FinishAsync(
                repo.Path,
                WorkbenchConfig.Default,
                new FinishOptions(created.Id, PullRequest: "not-a-url")));

        StringAssert.Contains(exception.Message, "absolute HTTP or HTTPS URL", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("cancelled")]
    [DataRow("superseded")]
    public async Task FinishAsync_RejectsTerminalOutcomeWithoutRewritingIt(string status)
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);
        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Terminal outcome",
            status,
            null,
            null);
        var before = await File.ReadAllTextAsync(created.Path);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FinishService.FinishAsync(
                repo.Path,
                WorkbenchConfig.Default,
                new FinishOptions(created.Id)));

        StringAssert.Contains(exception.Message, $"is {status}", StringComparison.Ordinal);
        Assert.AreEqual(before, await File.ReadAllTextAsync(created.Path));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string[] SnapshotFiles(string repoRoot)
    {
        return Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(repoRoot, path)}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}")
            .ToArray();
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
