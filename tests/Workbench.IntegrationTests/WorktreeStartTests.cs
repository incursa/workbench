namespace Workbench.IntegrationTests;

[TestClass]
public class WorktreeStartTests
{
    [TestMethod]
    public void WorktreeStart_CreatesAndReusesWorktree()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

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
}
