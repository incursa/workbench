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
            Path.Combine(repoRoot, "work", "items", "README.md"),
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
            Path.Combine(repoRoot, "work", "done", "README.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs: []
            ---

            # Done
            """);

        var created = WorkItemService.CreateItem(repoRoot, WorkbenchConfig.Default, "task", "Keep indexes readable", "draft", null, null);

        var items = WorkItemService.ListItems(repoRoot, WorkbenchConfig.Default, includeDone: true).Items;

        Assert.HasCount(1, items, string.Join(Environment.NewLine, items.Select(item => item.Path)));
        Assert.AreEqual(created.Id, items[0].Id);
        Assert.IsTrue(items[0].Id.StartsWith("WI-", StringComparison.Ordinal), items[0].Id);
    }

    [TestMethod]
    public async Task SyncNavigation_OmitsWorkArtifactDocsFromDocsIndexAsync()
    {
        var repoRoot = CreateRepoRoot();
        ScaffoldService.Scaffold(repoRoot, force: true);
        var created = WorkItemService.CreateItem(repoRoot, WorkbenchConfig.Default, "task", "Keep docs index focused", "draft", null, null);

        await NavigationService.SyncNavigationAsync(
            repoRoot,
            WorkbenchConfig.Default,
            includeDone: true,
            syncIssues: false,
            force: false,
            syncWorkboard: false,
            dryRun: false,
            syncDocs: false).ConfigureAwait(false);

        var docsReadme = await File.ReadAllTextAsync(Path.Combine(repoRoot, "docs", "README.md")).ConfigureAwait(false);
        var workReadme = await File.ReadAllTextAsync(Path.Combine(repoRoot, "work", "README.md")).ConfigureAwait(false);

        Assert.IsTrue(docsReadme.Contains("# Docs", StringComparison.Ordinal), docsReadme);
        Assert.IsTrue(docsReadme.Contains("_No docs found._", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("items/README.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("done/README.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains("work-item.task.md", StringComparison.Ordinal), docsReadme);
        Assert.IsFalse(docsReadme.Contains(created.Id, StringComparison.Ordinal), docsReadme);

        Assert.IsFalse(workReadme.Contains("| [ - README](items/README.md)", StringComparison.Ordinal), workReadme);
        Assert.IsFalse(workReadme.Contains("| [ - README](done/README.md)", StringComparison.Ordinal), workReadme);
        Assert.IsTrue(workReadme.Contains("# Workboard", StringComparison.Ordinal), workReadme);
    }

    private static string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return repoRoot;
    }
}
