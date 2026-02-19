namespace Workbench.IntegrationTests
{
    internal static class TestAssertions
    {
        public static JsonElement RunWorkbenchAndParseJson(string workingDirectory, params string[] args)
        {
            var result = WorkbenchCli.Run(workingDirectory, args);
            Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
            return ParseJson(result.StdOut);
        }

        public static JsonElement ParseJson(string json)
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        public static void RequireGhTestsEnabled()
        {
            var enabled = Environment.GetEnvironmentVariable("WORKBENCH_RUN_GH_TESTS");
            if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Set WORKBENCH_RUN_GH_TESTS=1 to enable GitHub CLI integration tests.");
            }
        }
    }
}
