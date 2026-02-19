using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class MigrationCommandTests
{
    [TestMethod]
    public void MigrateCoherentV1_DryRunReportsWithoutMovingFiles()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        RunScaffold(repo.Path);
        var itemPath = CreateDoneItemAndGetPath(repo.Path, "Dry run target");

        var data = RunMigration(repo.Path, dryRun: true).GetProperty("data");
        var movedToDone = data.GetProperty("movedToDone");
        Assert.IsTrue(data.GetProperty("dryRun").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, data.GetProperty("reportPath").ValueKind);
        Assert.IsGreaterThanOrEqualTo(movedToDone.GetArrayLength(), 1);

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
        GitTestRepo.InitializeGitRepo(repo.Path);

        RunScaffold(repo.Path);
        var itemPath = CreateDoneItemAndGetPath(repo.Path, "Migration target");
        var normalizedItemPath = itemPath!.Replace('\\', '/');
        StringAssert.Contains(normalizedItemPath, "/docs/70-work/items/", StringComparison.OrdinalIgnoreCase);

        var movedToDone = RunMigration(repo.Path).GetProperty("data").GetProperty("movedToDone");
        Assert.IsGreaterThanOrEqualTo(movedToDone.GetArrayLength(), 1);

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
        GitTestRepo.InitializeGitRepo(repo.Path);

        RunScaffold(repo.Path);
        var itemPath = CreateDoneItemAndGetPath(repo.Path, "Move back target");

        var donePath = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "done",
            Path.GetFileName(itemPath)!);
        File.Move(itemPath, donePath, overwrite: false);

        var doneContent = File.ReadAllText(donePath);
        File.WriteAllText(donePath, doneContent.Replace("status: done", "status: ready", StringComparison.Ordinal));

        var movedToItems = RunMigration(repo.Path).GetProperty("data").GetProperty("movedToItems");
        Assert.IsGreaterThanOrEqualTo(movedToItems.GetArrayLength(), 1);

        var expectedItemsPath = Path.Combine(
            repo.Path,
            "docs",
            "70-work",
            "items",
            Path.GetFileName(itemPath)!);
        Assert.IsTrue(File.Exists(expectedItemsPath), expectedItemsPath);
        Assert.IsFalse(File.Exists(donePath), donePath);
    }

    private static void RunScaffold(string repoPath)
    {
        var scaffold = WorkbenchCli.Run(
            repoPath,
            "--repo",
            repoPath,
            "scaffold");
        Assert.AreEqual(0, scaffold.ExitCode, $"stderr: {scaffold.StdErr}\nstdout: {scaffold.StdOut}");
    }

    private static string CreateDoneItemAndGetPath(string repoPath, string title)
    {
        var created = WorkbenchCli.Run(
            repoPath,
            "--repo",
            repoPath,
            "--format",
            "json",
            "item",
            "new",
            "--type",
            "task",
            "--title",
            title,
            "--status",
            "done");
        Assert.AreEqual(0, created.ExitCode, $"stderr: {created.StdErr}\nstdout: {created.StdOut}");

        var createdJson = TestAssertions.ParseJson(created.StdOut);
        var itemPath = createdJson.GetProperty("data").GetProperty("path").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(itemPath));
        Assert.IsTrue(File.Exists(itemPath!), itemPath);
        return itemPath;
    }

    private static JsonElement RunMigration(string repoPath, bool dryRun = false)
    {
        CommandResult migrate;
        if (dryRun)
        {
            migrate = WorkbenchCli.Run(
                repoPath,
                "--repo",
                repoPath,
                "--format",
                "json",
                "migrate",
                "coherent-v1",
                "--dry-run");
        }
        else
        {
            migrate = WorkbenchCli.Run(
                repoPath,
                "--repo",
                repoPath,
                "--format",
                "json",
                "migrate",
                "coherent-v1");
        }

        Assert.AreEqual(0, migrate.ExitCode, $"stderr: {migrate.StdErr}\nstdout: {migrate.StdOut}");
        return TestAssertions.ParseJson(migrate.StdOut);
    }
}
