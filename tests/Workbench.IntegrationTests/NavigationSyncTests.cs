using System.Text.Json;
using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public class NavigationSyncTests
{
    [TestMethod]
    public void NavSync_JsonRepairsCanonicalDocBacklinkAndWorkItemLists()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Navigation sync coverage item");
        var docPath = CreateRunbookDoc(repo.Path, "Navigation sync runbook", item.Id);

        RemoveWorkbenchWorkItem(docPath, item.Id);
        SetWorkbenchWorkItems(docPath, "WI-WB-9999");
        ClearCanonicalLists(item.Path);

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "nav",
            "sync",
            "--issues",
            "false");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.IsGreaterThan(0, data.GetProperty("docsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThan(0, data.GetProperty("itemsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThan(0, data.GetProperty("missingItems").GetArrayLength(), payload.ToString());
        StringAssert.Contains(data.GetProperty("missingItems")[0].GetString()!, "WI-WB-9999", StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "work-items", "WB", "_index.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "requirements", "_index.md")));

        var itemContent = File.ReadAllText(item.Path);
        StringAssert.Contains(itemContent, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", StringComparison.Ordinal);
        StringAssert.Contains(itemContent, "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", StringComparison.Ordinal);
        StringAssert.Contains(itemContent, "- VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", StringComparison.Ordinal);
        StringAssert.Contains(itemContent, "- SPEC-<DOMAIN>[-<GROUPING>...]", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NavSync_JsonRebuildsDerivedReadmesAndNavigationIndexes()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var config = WorkbenchConfig.Default with
        {
            Github = WorkbenchConfig.Default.Github with
            {
                Owner = "octo",
                Repository = "demo"
            }
        };
        File.WriteAllText(
            Path.Combine(repo.Path, ".workbench", "config.json"),
            JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig));

        var item = CreateItem(repo.Path, "Navigation sync coverage item");
        var docPath = CreateRunbookDoc(repo.Path, "Navigation sync runbook", item.Id);
        RemoveWorkbenchWorkItem(docPath, item.Id);
        SetWorkbenchWorkItems(docPath, "WI-WB-9999");
        ClearCanonicalLists(item.Path);

        CreateDoc(repo.Path, "doc", "Overview intro", "overview/intro.md");
        CreateDoc(repo.Path, "doc", "Tracking note", "tracking/nav-sync.md");
        CreateSpecificationDoc(repo.Path, "Navigation spec", "SPEC-WB-9000-NAV-SYNC", "WB");
        CreateDoc(repo.Path, "architecture", "Navigation architecture", "specs/architecture/WB/ARC-WB-9000-nav-sync.md");
        CreateDoc(repo.Path, "verification", "Navigation verification", "specs/verification/WB/VER-WB-9000-nav-sync.md");
        File.Delete(Path.Combine(repo.Path, "README.md"));

        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-9000-legacy-nav-sync.md"),
            """
            ---
            id: TASK-9000
            type: task
            status: complete
            title: Legacy navigation item
            owner: platform
            created: 2026-03-24
            updated: 2026-03-24
            related:
              specs:
                - <specs/requirements/WB/SPEC-WB-9000-nav-sync.md>
                - overview/intro.md
              files:
                - src/Workbench.Core/NavigationService.cs
              prs:
                - https://github.com/octo/demo/pull/77
              issues:
                - https://github.com/octo/demo/issues/42
              branches:
                - work/TASK-9000-nav-sync
            ---

            # TASK-9000 - Legacy navigation item

            ## Summary
            Legacy work item for navigation coverage.
            """);

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "nav",
            "sync",
            "--issues",
            "false");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.IsGreaterThan(0, data.GetProperty("docsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThan(0, data.GetProperty("itemsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThan(0, data.GetProperty("indexFilesUpdated").GetInt32(), payload.ToString());

        var rootReadme = Path.Combine(repo.Path, "README.md");
        var overviewReadme = Path.Combine(repo.Path, "overview", "README.md");
        var specsReadme = Path.Combine(repo.Path, "specs", "README.md");
        var architectureReadme = Path.Combine(repo.Path, "specs", "architecture", "README.md");
        var workReadme = Path.Combine(repo.Path, "specs", "work-items", "README.md");

        Assert.IsTrue(File.Exists(rootReadme), rootReadme);
        Assert.IsTrue(File.Exists(overviewReadme), overviewReadme);
        Assert.IsTrue(File.Exists(specsReadme), specsReadme);
        Assert.IsTrue(File.Exists(architectureReadme), architectureReadme);
        Assert.IsTrue(File.Exists(workReadme), workReadme);

        var rootContent = File.ReadAllText(rootReadme);
        StringAssert.Contains(rootContent, "Generated by `workbench nav sync`.", StringComparison.Ordinal);
        StringAssert.Contains(rootContent, "- [Requirements](specs/requirements/_index.md)", StringComparison.Ordinal);

        var overviewContent = File.ReadAllText(overviewReadme);
        StringAssert.Contains(overviewContent, "### Overview", StringComparison.Ordinal);
        StringAssert.Contains(overviewContent, "### Runbooks", StringComparison.Ordinal);
        StringAssert.Contains(overviewContent, "### Tracking", StringComparison.Ordinal);
        StringAssert.Contains(overviewContent, "### Specs", StringComparison.Ordinal);

        var specsContent = File.ReadAllText(specsReadme);
        StringAssert.Contains(specsContent, "Navigation spec", StringComparison.Ordinal);
        StringAssert.Contains(specsContent, "🧭 specification", StringComparison.Ordinal);
        StringAssert.Contains(specsContent, "SPEC-WB-9000-NAV-SYNC", StringComparison.Ordinal);

        var architectureContent = File.ReadAllText(architectureReadme);
        StringAssert.Contains(architectureContent, "Navigation architecture", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "🧩 architecture", StringComparison.Ordinal);

        var workContent = File.ReadAllText(workReadme);
        StringAssert.Contains(workContent, "Legacy navigation item", StringComparison.Ordinal);
        StringAssert.Contains(workContent, "#42", StringComparison.Ordinal);
        StringAssert.Contains(workContent, "NavigationService.cs", StringComparison.Ordinal);
        StringAssert.Contains(workContent, "specs/requirements/WB/SPEC-WB-9000-nav-sync.md", StringComparison.Ordinal);
        StringAssert.Contains(workContent, "Overview intro", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NavSync_DryRun_DoesNotMutateWorkItemOrDocFiles()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Navigation sync dry run item");
        var docPath = CreateRunbookDoc(repo.Path, "Navigation sync dry run runbook", item.Id);

        RemoveWorkbenchWorkItem(docPath, item.Id);
        ClearCanonicalLists(item.Path);

        var beforeItem = File.ReadAllText(item.Path);
        var beforeDoc = File.ReadAllText(docPath);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "nav",
            "sync",
            "--dry-run");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Docs updated:", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Work items updated:", StringComparison.Ordinal);

        var afterItem = File.ReadAllText(item.Path);
        var afterDoc = File.ReadAllText(docPath);
        Assert.AreEqual(beforeItem, afterItem);
        Assert.AreEqual(beforeDoc, afterDoc);
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "overview", "README.md")));
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "specs", "README.md")));
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "specs", "architecture", "README.md")));
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "specs", "work-items", "README.md")));
        StringAssert.Contains(File.ReadAllText(Path.Combine(repo.Path, "README.md")), "# Temp Repo", StringComparison.Ordinal);
    }

    [TestMethod]
    public void RepoSync_DocAndNavStages_JsonOutputCoversCombinedSyncPayload()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var item = CreateItem(repo.Path, "Repo sync coverage item");
        var docPath = CreateRunbookDoc(repo.Path, "Repo sync runbook", item.Id);

        RemoveWorkbenchWorkItem(docPath, item.Id);
        SetWorkbenchWorkItems(docPath, "WI-WB-9999");
        ClearCanonicalLists(item.Path);

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "sync",
            "--docs",
            "--nav",
            "--issues",
            "false");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.AreEqual(JsonValueKind.Null, data.GetProperty("items").ValueKind, payload.ToString());

        var docs = data.GetProperty("docs");
        Assert.IsGreaterThan(0, docs.GetProperty("docsUpdated").GetInt32(), payload.ToString());
        Assert.AreEqual(0, docs.GetProperty("itemsUpdated").GetInt32(), payload.ToString());
        Assert.AreEqual(1, docs.GetProperty("missingItems").GetArrayLength(), payload.ToString());
        StringAssert.Contains(docs.GetProperty("missingItems")[0].GetString()!, "WI-WB-9999", StringComparison.Ordinal);

        var nav = data.GetProperty("nav");
        Assert.IsGreaterThanOrEqualTo(0, nav.GetProperty("itemsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThanOrEqualTo(0, nav.GetProperty("docsUpdated").GetInt32(), payload.ToString());
        Assert.IsGreaterThan(0, nav.GetProperty("indexFilesUpdated").GetInt32(), payload.ToString());
        Assert.AreEqual(0, nav.GetProperty("warnings").GetArrayLength(), payload.ToString());

        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "overview", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "architecture", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "work-items", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "work-items", "WB", "_index.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "specs", "requirements", "_index.md")));
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
            "work_item",
            "--title",
            title);

        var data = payload.GetProperty("data");
        return (
            data.GetProperty("id").GetString()!,
            data.GetProperty("path").GetString()!);
    }

    private static string CreateRunbookDoc(string repoRoot, string title, string workItemId)
    {
        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repoRoot,
            "--repo",
            repoRoot,
            "--format",
            "json",
            "doc",
            "new",
            "--type",
            "runbook",
            "--path",
            "runbooks/navigation-sync.md",
            "--title",
            title,
            "--work-item",
            workItemId);

        return payload.GetProperty("data").GetProperty("path").GetString()!;
    }

    private static void CreateDoc(string repoRoot, string type, string title, string path)
    {
        TestAssertions.RunWorkbenchAndParseJson(
            repoRoot,
            "--repo",
            repoRoot,
            "--format",
            "json",
            "doc",
            "new",
            "--type",
            type,
            "--path",
            path,
            "--title",
            title);
    }

    private static void CreateSpecificationDoc(string repoRoot, string title, string artifactId, string domain)
    {
        TestAssertions.RunWorkbenchAndParseJson(
            repoRoot,
            "--repo",
            repoRoot,
            "--format",
            "json",
            "spec",
            "new",
            "--artifact-id",
            artifactId,
            "--domain",
            domain,
            "--title",
            title);
    }

    private static void RemoveWorkbenchWorkItem(string path, string workItemId)
    {
        var content = File.ReadAllText(path);
        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);

        Assert.IsTrue(frontMatter!.Data.TryGetValue("workbench", out var workbenchValue));
        Assert.IsNotNull(workbenchValue);

        var workbench = workbenchValue as Dictionary<string, object?>;
        Assert.IsNotNull(workbench);

        if (workbench!.TryGetValue("workItems", out var workItemsValue) && workItemsValue is IList<object?> workItems)
        {
            var beforeCount = workItems.Count;
            for (var i = workItems.Count - 1; i >= 0; i--)
            {
                if (string.Equals(workItems[i]?.ToString(), workItemId, StringComparison.OrdinalIgnoreCase))
                {
                    workItems.RemoveAt(i);
                }
            }

            Assert.IsGreaterThan(workItems.Count, beforeCount, frontMatter.Serialize());
            File.WriteAllText(path, frontMatter.Serialize());
            return;
        }

        Assert.Fail(frontMatter.Serialize());
    }

    private static void SetWorkbenchWorkItems(string path, params string[] workItemIds)
    {
        var content = File.ReadAllText(path);
        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);

        Assert.IsTrue(frontMatter!.Data.TryGetValue("workbench", out var workbenchValue));
        Assert.IsNotNull(workbenchValue);

        var workbench = workbenchValue as Dictionary<string, object?>;
        Assert.IsNotNull(workbench);
        workbench!["workItems"] = workItemIds.Cast<object?>().ToList();

        File.WriteAllText(path, frontMatter.Serialize());
    }

    private static void ClearCanonicalLists(string path)
    {
        var content = File.ReadAllText(path);
        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);

        frontMatter!.Data["addresses"] = new List<object?>();
        frontMatter.Data["design_links"] = new List<object?>();
        frontMatter.Data["verification_links"] = new List<object?>();
        File.WriteAllText(path, frontMatter.Serialize());
    }
}
