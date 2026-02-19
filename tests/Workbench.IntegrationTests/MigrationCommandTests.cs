namespace Workbench.IntegrationTests;

[TestClass]
public class MigrationCommandTests
{
    [TestMethod]
    public void MigrateCoherentV1_MovesTerminalItemsToDoneDirectory()
    {
        using var repo = TempRepo.Create();
        InitializeGitRepo(repo.Path);

        var scaffold = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "scaffold");
        Assert.AreEqual(0, scaffold.ExitCode, $"stderr: {scaffold.StdErr}\nstdout: {scaffold.StdOut}");

        var created = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "new",
            "--type",
            "task",
            "--title",
            "Migration target",
            "--status",
            "done");
        Assert.AreEqual(0, created.ExitCode, $"stderr: {created.StdErr}\nstdout: {created.StdOut}");

        var createdJson = TestAssertions.ParseJson(created.StdOut);
        var itemPath = createdJson.GetProperty("data").GetProperty("path").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(itemPath));
        Assert.IsTrue(File.Exists(itemPath!), itemPath);
        var normalizedItemPath = itemPath!.Replace('\\', '/');
        StringAssert.Contains(normalizedItemPath, "/docs/70-work/items/", StringComparison.OrdinalIgnoreCase);

        var migrate = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "migrate",
            "coherent-v1");
        Assert.AreEqual(0, migrate.ExitCode, $"stderr: {migrate.StdErr}\nstdout: {migrate.StdOut}");

        var migrateJson = TestAssertions.ParseJson(migrate.StdOut);
        var movedToDone = migrateJson.GetProperty("data").GetProperty("movedToDone");
        Assert.IsGreaterThanOrEqualTo(movedToDone.GetArrayLength(), 1, migrate.StdOut);

        var doneFile = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "done",
            Path.GetFileName(itemPath)!);
        Assert.IsTrue(File.Exists(doneFile), doneFile);
        Assert.IsFalse(File.Exists(itemPath), itemPath);

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
