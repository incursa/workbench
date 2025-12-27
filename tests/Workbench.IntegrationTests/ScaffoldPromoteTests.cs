namespace Workbench.IntegrationTests;

[TestClass]
public class ScaffoldPromoteTests
{
    [TestMethod]
    public void ScaffoldAndPromote_CreateBranchAndCommit()
    {
        using var repo = TempRepo.Create();
        InitializeGitRepo(repo.Path);

        var scaffoldResult = WorkbenchCli.Run(repo.Path, "scaffold", "--repo", repo.Path, "--format", "json");
        Assert.AreEqual(0, scaffoldResult.ExitCode);

        var scaffoldJson = TestAssertions.ParseJson(scaffoldResult.StdOut);
        var configPath = scaffoldJson.GetProperty("data").GetProperty("configPath").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(configPath));
        Assert.IsTrue(File.Exists(configPath!));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "work", "templates", "work-item.task.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "work", "WORKBOARD.md")));

        CommitAll(repo.Path, "Add scaffold");

        const string Title = "Add integration coverage";
        var promoteResult = WorkbenchCli.Run(repo.Path,
            "promote",
            "--type",
            "task",
            "--title",
            Title,
            "--repo",
            repo.Path,
            "--format",
            "json");
        Assert.AreEqual(0, promoteResult.ExitCode);

        var promoteJson = TestAssertions.ParseJson(promoteResult.StdOut);
        var data = promoteJson.GetProperty("data");
        var item = data.GetProperty("item");
        var itemId = item.GetProperty("id").GetString();
        var slug = item.GetProperty("slug").GetString();
        var itemPath = item.GetProperty("path").GetString();
        var branch = data.GetProperty("branch").GetString();
        var commitMessage = data.GetProperty("commit").GetProperty("message").GetString();
        var sha = data.GetProperty("commit").GetProperty("sha").GetString();
        var pushed = data.GetProperty("pushed").GetBoolean();

        Assert.IsFalse(string.IsNullOrWhiteSpace(itemId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(slug));
        Assert.IsFalse(string.IsNullOrWhiteSpace(itemPath));
        Assert.IsFalse(string.IsNullOrWhiteSpace(branch));
        Assert.IsFalse(string.IsNullOrWhiteSpace(commitMessage));
        Assert.IsFalse(string.IsNullOrWhiteSpace(sha));
        Assert.IsFalse(pushed);

        var expectedBranch = $"work/{itemId}-{slug}";
        Assert.AreEqual(expectedBranch, branch);
        Assert.AreEqual($"Promote {itemId}: {Title}", commitMessage);

        var expectedItemPath = Path.Combine(repo.Path, "work", "items", $"{itemId}-{slug}.md");
        Assert.AreEqual(expectedItemPath, itemPath);
        Assert.IsTrue(File.Exists(itemPath!));

        var branchResult = ProcessRunner.Run(repo.Path, "git", "rev-parse", "--abbrev-ref", "HEAD");
        Assert.AreEqual(0, branchResult.ExitCode);
        Assert.AreEqual(expectedBranch, branchResult.StdOut);

        var messageResult = ProcessRunner.Run(repo.Path, "git", "log", "-1", "--pretty=%B");
        Assert.AreEqual(0, messageResult.ExitCode);
        Assert.AreEqual(commitMessage, messageResult.StdOut.Trim());
    }

    [TestMethod]
    public void GhCliIsAvailableWhenEnabled()
    {
        TestAssertions.RequireGhTestsEnabled();

        var result = ProcessRunner.Run(Environment.CurrentDirectory, "gh", "--version");
        Assert.AreEqual(0, result.ExitCode);
        Assert.Contains("gh version", result.StdOut, StringComparison.OrdinalIgnoreCase);
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
