using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class FrontMatterTests
{
    [TestMethod]
    public void ParseAndSerialize_RoundTripsBody()
    {
        var content = """
            ---
            artifact_id: WI-WB-0001
            artifact_type: work_item
            title: Sample
            domain: WB
            status: planned
            owner: platform
            addresses: []
            design_links: []
            verification_links: []
            related_artifacts: []
            ---

            # WI-WB-0001 - Sample

            ## Summary
            Hello
            """;

        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);
        Assert.Contains("WI-WB-0001", frontMatter!.Serialize(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## Summary", frontMatter.Serialize(), StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TryParse_ParsesNestedMapsListsAndQuotedScalars()
    {
        var content = """
            ---
            title: "Spec: sync"
            empty: ""
            optional: null
            metadata:
              workItems:
                - WI-WB-0001
                - WI-WB-0002
              codeRefs:
                - src/Workbench.Core/ValidationService.cs
            ---

            Body text
            """;

        var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNotNull(frontMatter);

        Assert.AreEqual("Spec: sync", frontMatter!.Data["title"]);
        Assert.AreEqual(string.Empty, frontMatter.Data["empty"]);
        Assert.IsNull(frontMatter.Data["optional"]);

        var metadata = Assert.IsInstanceOfType<Dictionary<string, object?>>(frontMatter.Data["metadata"]);
        CollectionAssert.AreEqual(new object?[] { "WI-WB-0001", "WI-WB-0002" }, Assert.IsInstanceOfType<List<object?>>(metadata["workItems"]));
        CollectionAssert.AreEqual(new object?[] { "src/Workbench.Core/ValidationService.cs" }, Assert.IsInstanceOfType<List<object?>>(metadata["codeRefs"]));

        var serialized = frontMatter.Serialize();
        Assert.IsTrue(FrontMatter.TryParse(serialized, out var reparsed, out var reparseError), reparseError);
        Assert.IsNotNull(reparsed);
        Assert.AreEqual("Spec: sync", reparsed!.Data["title"]);
    }

    [TestMethod]
    public void TryParse_ReturnsHelpfulErrorsForMalformedYaml()
    {
        var cases = new[]
        {
            (
                """
                ---
                 title:
                  child: value
                ---
                """,
                "Invalid indentation"),
            (
                """
                ---
                	title: value
                ---
                """,
                "Tabs are not supported"),
            (
                """
                ---
                items:
                  nested: value
                    - bad
                ---
                """,
                "Invalid indentation"),
        };

        foreach (var (content, expectedError) in cases)
        {
            var ok = FrontMatter.TryParse(content, out var frontMatter, out var error);
            Assert.IsFalse(ok);
            Assert.IsNull(frontMatter);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains(expectedError, StringComparison.Ordinal), error);
        }
    }

    [TestMethod]
    public void Serialize_QuotesSpecialValuesAndRoundTripsNestedCollections()
    {
        var frontMatter = new FrontMatter(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Needs: quoting",
                ["enabled"] = true,
                ["count"] = 3,
                ["metadata"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["workItems"] = new List<object?> { "WI-WB-0001" },
                    ["codeRefs"] = new List<object?>()
                },
            },
            "Body");

        var serialized = frontMatter.Serialize();
        var ok = FrontMatter.TryParse(serialized, out var reparsed, out var error);

        Assert.Contains("title: \"Needs: quoting\"", serialized, StringComparison.Ordinal);
        Assert.Contains("enabled: true", serialized, StringComparison.Ordinal);
        Assert.Contains("count: 3", serialized, StringComparison.Ordinal);
        Assert.IsTrue(ok, error);
        Assert.AreEqual("Needs: quoting", reparsed!.Data["title"]);
        Assert.AreEqual("true", reparsed.Data["enabled"]);
        Assert.AreEqual("3", reparsed.Data["count"]);
    }
}
