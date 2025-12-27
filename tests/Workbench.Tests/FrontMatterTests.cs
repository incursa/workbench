using Workbench;

namespace Workbench.Tests;

[TestClass]
public class FrontMatterTests
{
    [TestMethod]
    public void ParseAndSerialize_RoundTripsBody()
    {
        var content = """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2025-01-01
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
            ---

            # TASK-0001 - Sample

            ## Summary
            Hello
            """;

        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);
        Assert.Contains("TASK-0001", frontMatter!.Serialize(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Summary", frontMatter.Serialize(), StringComparison.OrdinalIgnoreCase);
    }
}
