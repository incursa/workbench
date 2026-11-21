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
        var before = await File.ReadAllTextAsync(created.Path);

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
        Assert.AreEqual(before, await File.ReadAllTextAsync(created.Path));
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
