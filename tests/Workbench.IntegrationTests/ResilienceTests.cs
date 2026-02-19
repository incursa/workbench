using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class ResilienceTests
{
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
}
