namespace Workbench.IntegrationTests
{
    internal static class TestAssertions
    {
        public static CommandResult RunWorkbenchAndAssertSuccess(string workingDirectory, params string[] args)
        {
            var result = WorkbenchCli.Run(workingDirectory, args);
            Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
            return result;
        }

        public static JsonElement RunWorkbenchAndParseJson(string workingDirectory, params string[] args)
        {
            var result = RunWorkbenchAndAssertSuccess(workingDirectory, args);
            return ParseJson(result.StdOut);
        }

        public static JsonElement ParseJson(string json)
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

    }
}
