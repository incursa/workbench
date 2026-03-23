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
            Path.Combine(repoRoot, "specs", "work-items", "WB", "README.md"),
            """
            ---
            artifact_id: WI-WB-0000
            artifact_type: work_item
            title: Helper readme
            domain: WB
            status: planned
            owner: platform
            addresses: []
            design_links: []
            verification_links: []
            related_artifacts: []
            ---

            # Items
            """);
        File.WriteAllText(
            Path.Combine(repoRoot, "specs", "work-items", "WB", "_index.md"),
            """
            ---
            artifact_id: WI-WB-0000
            artifact_type: work_item
            title: Helper index
            domain: WB
            status: planned
            owner: platform
            addresses: []
            design_links: []
            verification_links: []
            related_artifacts: []
            ---

            # Index
            """);

        var created = WorkItemService.CreateItem(repoRoot, WorkbenchConfig.Default, "work_item", "Keep indexes readable", "planned", null, null);

        var items = WorkItemService.ListItems(repoRoot, WorkbenchConfig.Default, includeDone: true).Items;

        Assert.HasCount(1, items, string.Join(Environment.NewLine, items.Select(item => item.Path)));
        Assert.AreEqual(created.Id, items[0].Id);
        Assert.IsTrue(items[0].Id.StartsWith("WI-", StringComparison.Ordinal), items[0].Id);
    }

    private static string CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return repoRoot;
    }
}
