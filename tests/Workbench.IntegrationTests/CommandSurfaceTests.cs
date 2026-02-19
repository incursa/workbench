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
        Assert.IsFalse(result.StdOut.Contains("  run ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  tui ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  spec ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  adr ", StringComparison.Ordinal), result.StdOut);
        Assert.IsFalse(result.StdOut.Contains("  pr ", StringComparison.Ordinal), result.StdOut);
    }
}
