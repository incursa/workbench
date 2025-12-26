namespace Workbench.IntegrationTests;

public class ScaffoldPromoteTests
{
    [Fact]
    public void ScaffoldAndPromote_CreateBranchAndCommit()
    {
        using var repo = TempRepo.Create();
        InitializeGitRepo(repo.Path);

        var scaffoldResult = WorkbenchCli.Run(repo.Path, "scaffold", "--repo", repo.Path, "--format", "json");
        Assert.Equal(0, scaffoldResult.ExitCode);

        var scaffoldJson = TestAssertions.ParseJson(scaffoldResult.StdOut);
        var configPath = scaffoldJson.GetProperty("data").GetProperty("configPath").GetString();
        Assert.False(string.IsNullOrWhiteSpace(configPath));
        Assert.True(File.Exists(configPath!));
        Assert.True(File.Exists(Path.Combine(repo.Path, "work", "templates", "work-item.task.md")));
        Assert.True(File.Exists(Path.Combine(repo.Path, "work", "WORKBOARD.md")));

        CommitAll(repo.Path, "Add scaffold");

        const string title = "Add integration coverage";
        var promoteResult = WorkbenchCli.Run(repo.Path,
            "promote",
            "--type",
            "task",
            "--title",
            title,
            "--repo",
            repo.Path,
            "--format",
            "json");
        Assert.Equal(0, promoteResult.ExitCode);

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

        Assert.False(string.IsNullOrWhiteSpace(itemId));
        Assert.False(string.IsNullOrWhiteSpace(slug));
        Assert.False(string.IsNullOrWhiteSpace(itemPath));
        Assert.False(string.IsNullOrWhiteSpace(branch));
        Assert.False(string.IsNullOrWhiteSpace(commitMessage));
        Assert.False(string.IsNullOrWhiteSpace(sha));
        Assert.False(pushed);

        var expectedBranch = $"work/{itemId}-{slug}";
        Assert.Equal(expectedBranch, branch);
        Assert.Equal($"Promote {itemId}: {title}", commitMessage);

        var expectedItemPath = Path.Combine(repo.Path, "work", "items", $"{itemId}-{slug}.md");
        Assert.Equal(expectedItemPath, itemPath);
        Assert.True(File.Exists(itemPath!));

        var branchResult = ProcessRunner.Run(repo.Path, "git", "rev-parse", "--abbrev-ref", "HEAD");
        Assert.Equal(0, branchResult.ExitCode);
        Assert.Equal(expectedBranch, branchResult.StdOut);

        var messageResult = ProcessRunner.Run(repo.Path, "git", "log", "-1", "--pretty=%B");
        Assert.Equal(0, messageResult.ExitCode);
        Assert.Equal(commitMessage, messageResult.StdOut.Trim());
    }

    [Fact]
    public void GhCliIsAvailableWhenEnabled()
    {
        TestAssertions.RequireGhTestsEnabled();

        var result = ProcessRunner.Run(Environment.CurrentDirectory, "gh", "--version");
        Assert.Equal(0, result.ExitCode);
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
