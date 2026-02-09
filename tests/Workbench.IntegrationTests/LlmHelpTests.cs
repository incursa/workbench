namespace Workbench.IntegrationTests;

[TestClass]
public class LlmHelpTests
{
    [TestMethod]
    public void LlmHelp_PrintsComprehensiveReference()
    {
        var result = WorkbenchCli.Run(Environment.CurrentDirectory, "llm", "help");
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "# Workbench LLM Help", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "workbench item new", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "workbench worktree start", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "workbench codex run", StringComparison.Ordinal);
    }
}
