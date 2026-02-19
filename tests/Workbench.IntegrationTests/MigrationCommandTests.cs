using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class MigrationCommandTests
{
    [TestMethod]
    public void MigrateCoherentV1_DryRunReportsWithoutMovingFiles()
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
            "Dry run target",
            "--status",
            "done");
        Assert.AreEqual(0, created.ExitCode, $"stderr: {created.StdErr}\nstdout: {created.StdOut}");

        var createdJson = TestAssertions.ParseJson(created.StdOut);
        var itemPath = createdJson.GetProperty("data").GetProperty("path").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(itemPath));
        Assert.IsTrue(File.Exists(itemPath!), itemPath);

        var migrate = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "migrate",
            "coherent-v1",
            "--dry-run");
        Assert.AreEqual(0, migrate.ExitCode, $"stderr: {migrate.StdErr}\nstdout: {migrate.StdOut}");

        var migrateJson = TestAssertions.ParseJson(migrate.StdOut);
        var data = migrateJson.GetProperty("data");
        var movedToDone = data.GetProperty("movedToDone");
        Assert.IsTrue(data.GetProperty("dryRun").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, data.GetProperty("reportPath").ValueKind);
        Assert.IsGreaterThanOrEqualTo(movedToDone.GetArrayLength(), 1, migrate.StdOut);

        var doneFile = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "done",
            Path.GetFileName(itemPath)!);
        Assert.IsFalse(File.Exists(doneFile), doneFile);
        Assert.IsTrue(File.Exists(itemPath), itemPath);
    }

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

    [TestMethod]
    public void MigrateCoherentV1_MovesActiveItemsBackToItemsDirectory()
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
            "Move back target",
            "--status",
            "done");
        Assert.AreEqual(0, created.ExitCode, $"stderr: {created.StdErr}\nstdout: {created.StdOut}");

        var createdJson = TestAssertions.ParseJson(created.StdOut);
        var itemPath = createdJson.GetProperty("data").GetProperty("path").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(itemPath));
        Assert.IsTrue(File.Exists(itemPath!), itemPath);

        var donePath = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "done",
            Path.GetFileName(itemPath)!);
        File.Move(itemPath, donePath, overwrite: false);

        var doneContent = File.ReadAllText(donePath);
        File.WriteAllText(donePath, doneContent.Replace("status: done", "status: ready", StringComparison.Ordinal));

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
        var movedToItems = migrateJson.GetProperty("data").GetProperty("movedToItems");
        Assert.IsGreaterThanOrEqualTo(movedToItems.GetArrayLength(), 1, migrate.StdOut);

        var expectedItemsPath = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "items",
            Path.GetFileName(itemPath)!);
        Assert.IsTrue(File.Exists(expectedItemsPath), expectedItemsPath);
        Assert.IsFalse(File.Exists(donePath), donePath);
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
