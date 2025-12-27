namespace Workbench.IntegrationTests
{
    internal static class TestAssertions
    {
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
