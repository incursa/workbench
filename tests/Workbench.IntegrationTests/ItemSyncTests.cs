using System.Text.Json;
using System.Text.Json.Nodes;
using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public class ItemSyncTests
{
    [TestMethod]
    public void ItemSync_DryRunJson_ReportsSelectedItemIssueAndBranchCreation()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");
        ConfigureOfflineGithub(repo.Path);

        var selected = CreateItem(repo.Path, "Selected sync item");
        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "sync",
            "--id",
            selected.Id,
            "--dry-run");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.IsTrue(data.GetProperty("dryRun").GetBoolean(), payload.ToString());
        Assert.AreEqual(1, data.GetProperty("issuesCreated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("branchesCreated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("issuesUpdated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("itemsUpdated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("conflicts").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("warnings").GetArrayLength(), payload.ToString());

        var issue = data.GetProperty("issuesCreated")[0];
        Assert.AreEqual(selected.Id, issue.GetProperty("itemId").GetString());
        Assert.AreEqual(string.Empty, issue.GetProperty("issueUrl").GetString());

    }

    [TestMethod]
    public void ItemSync_DryRunTable_PrintsPendingIssueAndBranchActions()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");
        ConfigureOfflineGithub(repo.Path);

        var item = CreateItem(repo.Path, "Table sync item");
        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "item",
            "sync",
            "--id",
            item.Id,
            "--dry-run");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, $"Would create issue for {item.Id}.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ItemSync_DryRun_SkipsTerminalItems()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");
        ConfigureOfflineGithub(repo.Path);

        var item = CreateItem(repo.Path, "Done sync item");
        WorkItemService.UpdateStatus(item.Path, "complete", null);

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "sync",
            "--id",
            item.Id,
            "--dry-run");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.AreEqual(0, data.GetProperty("issuesCreated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("branchesCreated").GetArrayLength(), payload.ToString());
        Assert.AreEqual(0, data.GetProperty("warnings").GetArrayLength(), payload.ToString());
    }

    [TestMethod]
    public void ItemSync_InvalidIssueReference_JsonReturnsErrorEnvelope()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");
        ConfigureOfflineGithub(repo.Path);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "sync",
            "--issue",
            "not-an-issue");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        var payload = TestAssertions.ParseJson(result.StdOut);
        Assert.IsFalse(payload.GetProperty("ok").GetBoolean(), result.StdOut);
        var error = payload.GetProperty("error");
        Assert.AreEqual("unexpected_error", error.GetProperty("code").GetString());
        Assert.AreEqual("Invalid issue reference: not-an-issue", error.GetProperty("message").GetString());
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

    private static void ConfigureOfflineGithub(string repoRoot)
    {
        var configPath = Path.Combine(repoRoot, ".workbench", "config.json");
        var root = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var github = root["Github"]!.AsObject();
        github["Provider"] = "broken-provider";
        github["Host"] = "github.com";
        github["Owner"] = "octo";
        github["Repository"] = "demo";

        File.WriteAllText(
            configPath,
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }
}
