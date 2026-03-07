using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class NavigationServiceTests
{
    [TestMethod]
    public void ListItems_SkipsWorkHelperReadmes()
    {
        var repoRoot = CreateRepoRoot();
        ScaffoldService.Scaffold(repoRoot, force: true);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "README.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs: []
            ---

            # Items
            """);
        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "done", "README.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs: []
            ---

            # Done
            """);

        WorkItemService.CreateItem(repoRoot, WorkbenchConfig.Default, "task", "Keep indexes readable", "draft", null, null);

        var items = WorkItemService.ListItems(repoRoot, WorkbenchConfig.Default, includeDone: true).Items;

        Assert.HasCount(1, items, string.Join(Environment.NewLine, items.Select(item => item.Path)));
        Assert.AreEqual("TASK-0001", items[0].Id);
    }

    [TestMethod]
    public async Task SyncNavigation_OmitsWorkArtifactDocsFromDocsIndexAsync()
    {
        var repoRoot = CreateRepoRoot();
        ScaffoldService.Scaffold(repoRoot, force: true);
        WorkItemService.CreateItem(repoRoot, WorkbenchConfig.Default, "task", "Keep docs index focused", "draft", null, null);

        await NavigationService.SyncNavigationAsync(
            repoRoot,
            WorkbenchConfig.Default,
            includeDone: true,
            syncIssues: false,
            force: false,
            syncWorkboard: true,
            dryRun: false,
            syncDocs: false).ConfigureAwait(false);

        var docsReadme = await File.ReadAllTextAsync(Path.Combine(repoRoot, "docs", "README.md")).ConfigureAwait(false);
        var workReadme = await File.ReadAllTextAsync(Path.Combine(repoRoot, "docs", "70-work", "README.md")).ConfigureAwait(false);

        Assert.IsTrue(docsReadme.Contains("70-work/README.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("items/README.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("done/README.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("work-item.task.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("TASK-0001", StringComparison.Ordinal), docsReadme);

        Assert.IsFalse(workReadme.Contains("| [ - README](items/README.md)", StringComparison.Ordinal), workReadme);
        Assert.IsFalse(workReadme.Contains("| [ - README](done/README.md)", StringComparison.Ordinal), workReadme);
        Assert.IsTrue(workReadme.Contains("TASK-0001 - Keep docs index focused", StringComparison.Ordinal), workReadme);
    }

    private static string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return repoRoot;
    }
}
