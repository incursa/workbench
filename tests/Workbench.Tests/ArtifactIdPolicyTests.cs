using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ArtifactIdPolicyTests
{
    [TestMethod]
    public void MatchesArtifactId_AllowsGroupedSpecificationIdsWithoutCapabilityMatch()
    {
        var policy = ArtifactIdPolicy.Default;

        var matches = policy.MatchesArtifactId(
            "specification",
            "SPEC-WB-STD",
            "WB",
            "standards-integration");

        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void MatchesArtifactId_RejectsWrongDomain()
    {
        var policy = ArtifactIdPolicy.Default;

        var matches = policy.MatchesArtifactId(
            "specification",
            "SPEC-CLI-CODEX",
            "WB",
            "command-surface");

        Assert.IsFalse(matches);
    }
}
