namespace Workbench.IntegrationTests;

[TestClass]
public class WorktreeStartTests
{
    [TestMethod]
    public void WorktreeStart_CreatesAndReusesWorktree()
    {
        using var repo = TempRepo.Create();
        InitializeGitRepo(repo.Path);

        var first = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "worktree",
            "start",
            "--slug",
            "agent-cli-check");
        Assert.AreEqual(0, first.ExitCode, $"stderr: {first.StdErr}\nstdout: {first.StdOut}");

        var firstJson = TestAssertions.ParseJson(first.StdOut);
        var firstData = firstJson.GetProperty("data");
        var branch = firstData.GetProperty("branch").GetString();
        var worktreePath = firstData.GetProperty("worktreePath").GetString();
        var reused = firstData.GetProperty("reused").GetBoolean();

        Assert.AreEqual("feature/agent-cli-check", branch);
        Assert.IsFalse(reused);
        Assert.IsFalse(string.IsNullOrWhiteSpace(worktreePath));
        Assert.IsTrue(Directory.Exists(worktreePath!));

        var second = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "worktree",
            "start",
            "--slug",
            "agent-cli-check");
        Assert.AreEqual(0, second.ExitCode, $"stderr: {second.StdErr}\nstdout: {second.StdOut}");

        var secondJson = TestAssertions.ParseJson(second.StdOut);
        var secondData = secondJson.GetProperty("data");
        Assert.IsTrue(secondData.GetProperty("reused").GetBoolean());
        Assert.AreEqual(worktreePath, secondData.GetProperty("worktreePath").GetString());
    }

    private static void InitializeGitRepo(string repoRoot)
    {
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "init"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "checkout", "-b", "main"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "config", "user.email", "workbench@example.com"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "config", "user.name", "Workbench Tests"));

        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Temp Repo\n");
        CommitAll(repoRoot, "Initial commit");
    }

    private static void CommitAll(string repoRoot, string message)
    {
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "add", "."));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "commit", "-m", message));
    }

    private static void EnsureSuccess(CommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed: {result.StdErr}\n{result.StdOut}");
        }
    }
}
