namespace Workbench.IntegrationTests;

[TestClass]
public sealed class ItemHierarchyCommandTests
{
    [TestMethod]
    public void HierarchySetAndShow_UseVersionedJsonAndGeneratedNavigation()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        TestAssertions.RunWorkbenchAndAssertSuccess(repo.Path, "--repo", repo.Path, "scaffold");
        var parentId = CreateItem(repo.Path, "Parent initiative");
        var childId = CreateItem(repo.Path, "Child delivery");

        var updated = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "hierarchy",
            "set",
            childId,
            "--parent",
            parentId,
            "--initiative",
            "workbench://github.com/incursa/portfolio/WI-PORTFOLIO-0042");

        Assert.IsTrue(updated.GetProperty("ok").GetBoolean());
        var data = updated.GetProperty("data");
        Assert.AreEqual(1, data.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual(parentId, data.GetProperty("parentRef").GetString());

        var parent = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "hierarchy",
            "show",
            parentId);
        Assert.AreEqual(childId, parent.GetProperty("data").GetProperty("childRefs")[0].GetString());

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "nav",
            "sync");
        var index = File.ReadAllText(Path.Combine(repo.Path, "specs", "work-items", "README.md"));
        StringAssert.Contains(index, $"Parent: {parentId}", StringComparison.Ordinal);
        StringAssert.Contains(index, "Initiatives: workbench://github.com/incursa/portfolio/WI-PORTFOLIO-0042", StringComparison.Ordinal);
    }

    private static string CreateItem(string repoRoot, string title)
    {
        var result = TestAssertions.RunWorkbenchAndParseJson(
            repoRoot,
            "--repo",
            repoRoot,
            "--format",
            "json",
            "item",
            "new",
            "--type",
            "work_item",
            "--title",
            title);
        return result.GetProperty("data").GetProperty("id").GetString()
            ?? throw new AssertFailedException("Item ID was missing.");
    }
}
