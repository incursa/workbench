using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public class DocHandlerTests
{
    [TestMethod]
    public void DocNew_JsonCreatesSpecAndLinksWorkItem()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Doc create item");
        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "doc",
            "new",
            "--type",
            "spec",
            "--title",
            "Quality workflow spec",
            "--work-item",
            item.Id,
            "--code-ref",
            "src/Workbench.Cli/Program.cs#L1-L3");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.AreEqual("spec", data.GetProperty("type").GetString());
        Assert.AreEqual(1, data.GetProperty("workItems").GetArrayLength());
        Assert.AreEqual(item.Id, data.GetProperty("workItems")[0].GetString());
        var artifactId = data.GetProperty("artifactId").GetString()!;

        var docPath = data.GetProperty("path").GetString()!;
        Assert.IsTrue(File.Exists(docPath), docPath);
        StringAssert.Contains(Path.GetFileName(docPath), artifactId + ".md", StringComparison.Ordinal);

        var docContent = File.ReadAllText(docPath);
        StringAssert.Contains(docContent, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(docContent, $"artifact_id: {artifactId}", StringComparison.Ordinal);
        StringAssert.Contains(docContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(docContent, $"- {item.Id}", StringComparison.Ordinal);
        Assert.IsFalse(docContent.Contains("workItems:", StringComparison.Ordinal), docContent);
        Assert.IsFalse(docContent.Contains("codeRefs:", StringComparison.Ordinal), docContent);

        var updatedItem = WorkItemService.LoadItem(item.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        var expectedSpecLink = "/" + Path.GetRelativePath(repo.Path, docPath).Replace('\\', '/');
        CollectionAssert.Contains(updatedItem.Related.Specs.ToArray(), expectedSpecLink);
    }

    [TestMethod]
    public void DocLink_JsonLinksExistingSpecToWorkItem()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Doc link item");
        var docPath = CreateDoc(repo.Path, "Standalone spec", "specs/requirements/standalone-spec.md");

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "doc",
            "link",
            "--type",
            "spec",
            "--path",
            docPath,
            "--work-item",
            item.Id);

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.AreEqual("/specs/requirements/standalone-spec.md", data.GetProperty("docPath").GetString());
        Assert.AreEqual("spec", data.GetProperty("docType").GetString());
        Assert.AreEqual(1, data.GetProperty("itemsUpdated").GetInt32());
        Assert.IsTrue(data.GetProperty("docUpdated").GetBoolean());

        var updatedItem = WorkItemService.LoadItem(item.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.Contains(updatedItem.Related.Specs.ToArray(), "/specs/requirements/standalone-spec.md");

        var docContent = File.ReadAllText(docPath);
        StringAssert.Contains(docContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(docContent, $"- {item.Id}", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocUnlink_DryRunTable_PrintsSummaryWithoutMutatingFiles()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Doc unlink item");
        var docPath = CreateDoc(repo.Path, "Linked spec", "specs/requirements/linked-spec.md", item.Id);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doc",
            "unlink",
            "--type",
            "spec",
            "--path",
            docPath,
            "--work-item",
            item.Id,
            "--dry-run");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "SPEC unlinked: /specs/requirements/linked-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Work items updated: 1", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Doc updated: yes", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Dry run: no files were modified.", StringComparison.Ordinal);

        var unchangedItem = WorkItemService.LoadItem(item.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.Contains(unchangedItem.Related.Specs.ToArray(), "/specs/requirements/linked-spec.md");

        var docContent = File.ReadAllText(docPath);
        StringAssert.Contains(docContent, $"- {item.Id}", StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpecUnlink_DryRunTable_PrintsFriendlySummaryWithoutMutatingFiles()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Spec unlink item");
        var docPath = CreateDoc(repo.Path, "Linked spec", "specs/requirements/linked-spec.md", item.Id);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "spec",
            "unlink",
            "--path",
            docPath,
            "--work-item",
            item.Id,
            "--dry-run");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Spec unlinked: /specs/requirements/linked-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Work items updated: 1", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Doc updated: yes", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Dry run: no files were modified.", StringComparison.Ordinal);

        var unchangedItem = WorkItemService.LoadItem(item.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.Contains(unchangedItem.Related.Specs.ToArray(), "/specs/requirements/linked-spec.md");

        var docContent = File.ReadAllText(docPath);
        StringAssert.Contains(docContent, $"- {item.Id}", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocLink_WithoutWorkItems_ReturnsFriendlyError()
    {
        using var repo = TempRepo.Create();

        var result = WorkbenchCli.Run(
            repo.Path,
            "doc",
            "link",
            "--type",
            "spec",
            "--path",
            "specs/requirements/missing.md");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        Assert.AreEqual("No work items provided.", result.StdOut);
    }

    private static (string Id, string Path) CreateItem(string repoRoot, string title)
    {
        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repoRoot,
            "--repo",
            repoRoot,
            "--format",
            "json",
            "item",
            "new",
            "--type",
            "task",
            "--title",
            title);

        var data = payload.GetProperty("data");
        return (
            data.GetProperty("id").GetString()!,
            data.GetProperty("path").GetString()!);
    }

    private static string CreateDoc(string repoRoot, string title, string path, params string[] workItems)
    {
        var args = new List<string>
        {
            "--repo",
            repoRoot,
            "--format",
            "json",
            "doc",
            "new",
            "--type",
            "spec",
            "--title",
            title,
            "--path",
            path
        };

        foreach (var workItem in workItems)
        {
            args.Add("--work-item");
            args.Add(workItem);
        }

        var payload = TestAssertions.RunWorkbenchAndParseJson(repoRoot, args.ToArray());
        return payload.GetProperty("data").GetProperty("path").GetString()!;
    }
}
