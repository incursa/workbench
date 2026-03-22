using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class IdAllocationTests
{
    [TestMethod]
    public void CreateItem_AllocatesNextIdPerType()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "work-items", "WB"));

        var config = WorkbenchConfig.Default;

        var first = WorkItemService.CreateItem(repoRoot, config, "work_item", "First", "planned", null, null);
        Assert.AreEqual("WI-WB-0001", first.Id);

        var thirdPath = Path.Combine(repoRoot, "specs", "work-items", "WB", "WI-WB-0003-third.md");
        File.WriteAllText(
            thirdPath,
            new FrontMatter(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["artifact_id"] = "WI-WB-0003",
                    ["artifact_type"] = "work_item",
                    ["title"] = "Third",
                    ["domain"] = "WB",
                    ["status"] = "planned",
                    ["owner"] = "platform",
                    ["addresses"] = new List<string> { "- REQ-WB-0001" },
                    ["design_links"] = new List<string> { "- ARC-WB-0001" },
                    ["verification_links"] = new List<string> { "- VER-WB-0001" },
                    ["related_artifacts"] = new List<string> { "SPEC-WB-0001" }
                },
                SpecTraceMarkdown.BuildWorkItemBody(
                    "Third",
                    "- REQ-WB-0001",
                    "- ARC-WB-0001",
                    artifactId: "WI-WB-0003",
                    relatedArtifacts: "- SPEC-WB-0001")).Serialize());

        var result = WorkItemService.CreateItem(repoRoot, config, "work_item", "Next item", null, null, null);
        Assert.AreEqual("WI-WB-0004", result.Id);
    }
}
