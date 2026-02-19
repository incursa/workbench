using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class ResilienceTests
{
    [TestMethod]
    public void Doctor_GitRepoWithoutScaffold_ReturnsWarningsInJson()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doctor",
            "--json");

        Assert.AreEqual(1, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        var payload = TestAssertions.ParseJson(result.StdOut);
        Assert.IsTrue(payload.GetProperty("ok").GetBoolean());
        var checks = payload.GetProperty("data").GetProperty("checks").EnumerateArray().ToList();
        Assert.IsTrue(
            checks.Any(c =>
                string.Equals(c.GetProperty("name").GetString(), "config", StringComparison.Ordinal)
                && string.Equals(c.GetProperty("status").GetString(), "warn", StringComparison.Ordinal)),
            result.StdOut);
        Assert.IsTrue(
            checks.Any(c =>
                string.Equals(c.GetProperty("name").GetString(), "paths", StringComparison.Ordinal)
                && string.Equals(c.GetProperty("status").GetString(), "warn", StringComparison.Ordinal)),
            result.StdOut);
    }

    [TestMethod]
    public void Doctor_NonGitRepo_ReturnsFriendlyErrorWithoutStackTrace()
    {
        using var repo = TempRepo.Create();

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doctor");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdErr, "Error: Target path is not inside a git repository.", StringComparison.Ordinal);
        StringAssert.Contains(result.StdErr, "Hint: Run `git init` in the target directory, or pass `--repo <path>` for an existing repository.", StringComparison.Ordinal);
        Assert.IsFalse(result.StdErr.Contains("System.InvalidOperationException", StringComparison.Ordinal), result.StdErr);
        Assert.IsFalse(result.StdErr.Contains(" at Workbench.", StringComparison.Ordinal), result.StdErr);
    }

    [TestMethod]
    public void Doctor_NonGitRepo_DebugIncludesExceptionDetails()
    {
        using var repo = TempRepo.Create();

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--debug",
            "doctor");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdErr, "System.InvalidOperationException", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Doctor_NonGitRepo_JsonFormatAfterCommand_ReturnsErrorEnvelope()
    {
        using var repo = TempRepo.Create();

        var result = WorkbenchCli.Run(
            repo.Path,
            "doctor",
            "--repo",
            repo.Path,
            "--format",
            "json");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        var payload = TestAssertions.ParseJson(result.StdOut);
        Assert.IsFalse(payload.GetProperty("ok").GetBoolean());
        var error = payload.GetProperty("error");
        Assert.AreEqual("repo_not_git", error.GetProperty("code").GetString());
        Assert.AreEqual("Target path is not inside a git repository.", error.GetProperty("message").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(error.GetProperty("hint").GetString()));
        Assert.IsFalse(result.StdErr.Contains("System.InvalidOperationException", StringComparison.Ordinal), result.StdErr);
    }

    [TestMethod]
    public void ConfigShow_MalformedConfig_ReturnsConfigError()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        var workbenchDir = Path.Combine(repo.Path, ".workbench");
        Directory.CreateDirectory(workbenchDir);
        File.WriteAllText(Path.Combine(workbenchDir, "config.json"), "{ invalid");

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "config",
            "show");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Config error:", StringComparison.Ordinal);
        Assert.IsFalse(result.StdErr.Contains("System.", StringComparison.Ordinal), result.StdErr);
    }

    [TestMethod]
    public void ValidateStrict_MissingSchemas_ReturnsErrors()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        File.Delete(Path.Combine(repo.Path, "docs", "30-contracts", "work-item.schema.json"));
        File.Delete(Path.Combine(repo.Path, "docs", "30-contracts", "workbench-config.schema.json"));
        File.Delete(Path.Combine(repo.Path, "docs", "30-contracts", "doc.schema.json"));

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "validate",
            "--strict");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "schema not found at", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ItemList_GlobalOptionsAfterSubcommand_AreAccepted()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        var payload = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "item",
            "list",
            "--repo",
            repo.Path,
            "--format",
            "json");

        Assert.IsTrue(payload.GetProperty("ok").GetBoolean());
        Assert.AreEqual(JsonValueKind.Array, payload.GetProperty("data").GetProperty("items").ValueKind);
    }

    [TestMethod]
    public void RepoSync_IssuesFalse_DoesNotRequireGithubProviderForItemStep()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");

        TestAssertions.RunWorkbenchAndAssertSuccess(
            repo.Path,
            "--repo",
            repo.Path,
            "item",
            "new",
            "--type",
            "task",
            "--title",
            "Local-only sync test item");

        var configPath = Path.Combine(repo.Path, ".workbench", "config.json");
        var configJson = File.ReadAllText(configPath);
        File.WriteAllText(
            configPath,
            configJson.Replace("\"Provider\": \"octokit\"", "\"Provider\": \"broken-provider\"", StringComparison.Ordinal));

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "sync",
            "--issues",
            "false");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        var payload = TestAssertions.ParseJson(result.StdOut);
        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), result.StdOut);
        var itemData = payload.GetProperty("data").GetProperty("items");
        Assert.AreEqual(0, itemData.GetProperty("issuesCreated").GetArrayLength(), result.StdOut);
        Assert.AreEqual(0, itemData.GetProperty("issuesUpdated").GetArrayLength(), result.StdOut);
    }
}
