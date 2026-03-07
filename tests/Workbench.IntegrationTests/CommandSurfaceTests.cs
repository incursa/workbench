namespace Workbench.IntegrationTests;

[TestClass]
public class CommandSurfaceTests
{
    [TestMethod]
    public void Help_ExposesGuideAndMigrate_AndOmitsRemovedCommands()
    {
        var result = WorkbenchCli.Run(Environment.CurrentDirectory, "--help");
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");

        StringAssert.Contains(result.StdOut, "guide", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "migrate <target>", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "quality", StringComparison.Ordinal);
        Assert.IsFalse(result.StdOut.Contains("  run ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  tui ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  spec ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  adr ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  pr ", StringComparison.Ordinal), result.StdOut);
    }

    [TestMethod]
    public void Help_ClarifiesSyncCommandRoles()
    {
        var syncHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "sync", "--help");
        Assert.AreEqual(0, syncHelp.ExitCode, $"stderr: {syncHelp.StdErr}\nstdout: {syncHelp.StdOut}");
        StringAssert.Contains(syncHelp.StdOut, "Umbrella repo sync", StringComparison.Ordinal);
        StringAssert.Contains(syncHelp.StdOut, "common happy path", StringComparison.Ordinal);

        var itemSyncHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "item", "sync", "--help");
        Assert.AreEqual(0, itemSyncHelp.ExitCode, $"stderr: {itemSyncHelp.StdErr}\nstdout: {itemSyncHelp.StdOut}");
        StringAssert.Contains(itemSyncHelp.StdOut, "External sync stage", StringComparison.Ordinal);
        StringAssert.Contains(itemSyncHelp.StdOut, "GitHub issues and branch state", StringComparison.Ordinal);

        var docSyncHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "doc", "sync", "--help");
        Assert.AreEqual(0, docSyncHelp.ExitCode, $"stderr: {docSyncHelp.StdErr}\nstdout: {docSyncHelp.StdOut}");
        StringAssert.Contains(docSyncHelp.StdOut, "Repo metadata stage", StringComparison.Ordinal);
        StringAssert.Contains(docSyncHelp.StdOut, "Does not regenerate indexes.", StringComparison.Ordinal);

        var navSyncHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "nav", "sync", "--help");
        Assert.AreEqual(0, navSyncHelp.ExitCode, $"stderr: {navSyncHelp.StdErr}\nstdout: {navSyncHelp.StdOut}");
        StringAssert.Contains(navSyncHelp.StdOut, "Derived view stage", StringComparison.Ordinal);
        StringAssert.Contains(navSyncHelp.StdOut, "syncing links first by default", StringComparison.Ordinal);

        var boardRegenHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "board", "regen", "--help");
        Assert.AreEqual(0, boardRegenHelp.ExitCode, $"stderr: {boardRegenHelp.StdErr}\nstdout: {boardRegenHelp.StdOut}");
        StringAssert.Contains(boardRegenHelp.StdOut, "only the workboard section", StringComparison.Ordinal);

        var qualityHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "quality", "--help");
        Assert.AreEqual(0, qualityHelp.ExitCode, $"stderr: {qualityHelp.StdErr}\nstdout: {qualityHelp.StdOut}");
        StringAssert.Contains(qualityHelp.StdOut, "repo-native quality evidence", StringComparison.Ordinal);

        var qualitySyncHelp = WorkbenchCli.Run(Environment.CurrentDirectory, "quality", "sync", "--help");
        Assert.AreEqual(0, qualitySyncHelp.ExitCode, $"stderr: {qualitySyncHelp.StdErr}\nstdout: {qualitySyncHelp.StdOut}");
        StringAssert.Contains(qualitySyncHelp.StdOut, "--results <results>", StringComparison.Ordinal);
        StringAssert.Contains(qualitySyncHelp.StdOut, "--coverage <coverage>", StringComparison.Ordinal);
    }
}
