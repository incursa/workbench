using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ArtifactIdPolicyTests
{
    [TestMethod]
    public void Load_MissingPolicy_ReturnsDefaultWithoutError()
    {
        using var repo = CreateRepoRoot();

        var policy = ArtifactIdPolicy.Load(repo.Path, out var error);

        Assert.IsNotNull(policy);
        Assert.IsNull(error);
        Assert.AreEqual(ArtifactIdPolicy.Default.MinimumDigits, policy.MinimumDigits);
    }

    [TestMethod]
    public void Load_MalformedPolicy_ReturnsDefaultAndError()
    {
        using var repo = CreateRepoRoot();
        File.WriteAllText(Path.Combine(repo.Path, "artifact-id-policy.json"), "{ invalid");

        var policy = ArtifactIdPolicy.Load(repo.Path, out var error);

        Assert.IsNotNull(policy);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "JsonReaderException", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Load_CustomPolicyAppliesTemplatesAndDigits()
    {
        using var repo = CreateRepoRoot();
        File.WriteAllText(
            Path.Combine(repo.Path, "artifact-id-policy.json"),
            """
            {
              "sequence": {
                "minimum_digits": 5
              },
              "artifact_id_templates": {
                "specification": "SPEC-{domain}{grouping}",
                "architecture": "ARC-{domain}{grouping}-{sequence}",
                "work_item": "WI-{domain}{grouping}-{sequence}",
                "verification": "VER-{domain}-{sequence}"
              }
            }
            """);

        var policy = ArtifactIdPolicy.Load(repo.Path, out var error);

        Assert.IsNotNull(policy);
        Assert.IsNull(error);
        Assert.AreEqual(5, policy.MinimumDigits);
        Assert.AreEqual("SPEC-WB-STD", policy.BuildArtifactId("spec", "wb", "std", 1));
        Assert.AreEqual("ARC-WB-CLI-00007", policy.BuildArtifactId("architecture", "wb", "cli", 7));
        Assert.AreEqual("WI-WB-OPS-00042", policy.BuildArtifactId("work-item", "wb", "ops", 42));
        Assert.AreEqual("VER-WB-00009", policy.BuildArtifactId("verification", "wb", null, 9));
        Assert.AreEqual("ARC-WB-CLI-", policy.BuildArtifactIdPrefix("architecture", "wb", "cli"));
        Assert.AreEqual("SPEC-{domain}{grouping}", policy.GetTemplateForDocType("spec"));
        Assert.AreEqual("WI-{domain}{grouping}-{sequence}", policy.GetTemplateForDocType("work-item"));
    }

    [TestMethod]
    public void Load_MissingTemplates_ReturnsDefaultAndError()
    {
        using var repo = CreateRepoRoot();
        File.WriteAllText(
            Path.Combine(repo.Path, "artifact-id-policy.json"),
            """
            {
              "sequence": {
                "minimum_digits": 4
              }
            }
            """);

        var policy = ArtifactIdPolicy.Load(repo.Path, out var error);

        Assert.IsNotNull(policy);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, "no artifact_id_templates entries were found", StringComparison.Ordinal);
        Assert.AreEqual(ArtifactIdPolicy.Default.MinimumDigits, policy.MinimumDigits);
    }

    [TestMethod]
    public void GetTemplateKey_RecognizesSupportedDocTypes()
    {
        Assert.AreEqual("specification", ArtifactIdPolicy.GetTemplateKey("spec"));
        Assert.AreEqual("specification", ArtifactIdPolicy.GetTemplateKey("specification"));
        Assert.AreEqual("architecture", ArtifactIdPolicy.GetTemplateKey("architecture"));
        Assert.AreEqual("work_item", ArtifactIdPolicy.GetTemplateKey("work_item"));
        Assert.AreEqual("work_item", ArtifactIdPolicy.GetTemplateKey("work-item"));
        Assert.AreEqual("verification", ArtifactIdPolicy.GetTemplateKey("verification"));
        Assert.IsNull(ArtifactIdPolicy.GetTemplateKey("unknown"));
    }

    [TestMethod]
    public void NormalizeHelpers_HandleWhitespaceSeparatorsAndSequences()
    {
        Assert.AreEqual(string.Empty, ArtifactIdPolicy.NormalizeToken(null));
        Assert.AreEqual("WORK-BENCH-CORE", ArtifactIdPolicy.NormalizeToken(" work bench/core "));
        Assert.AreEqual(string.Empty, ArtifactIdPolicy.NormalizeGrouping(null));
        Assert.AreEqual("-QUALITY-EVIDENCE", ArtifactIdPolicy.NormalizeGrouping("quality evidence"));

        Assert.IsTrue(ArtifactIdPolicy.TryParseSequence("0012", out var parsedSequence));
        Assert.AreEqual(12, parsedSequence);
        Assert.IsFalse(ArtifactIdPolicy.TryParseSequence("12a", out _));
    }

    [TestMethod]
    public void MatchesArtifactId_CoversSequenceAndTemplateBranches()
    {
        var defaultPolicy = ArtifactIdPolicy.Default;

        Assert.IsTrue(defaultPolicy.MatchesArtifactId("specification", "SPEC-WB-STD", "WB", "standards-integration"));
        Assert.IsTrue(defaultPolicy.MatchesArtifactId("architecture", "ARC-WB-CLI-0012", "WB", "cli"));
        Assert.IsFalse(defaultPolicy.MatchesArtifactId("architecture", "ARC-CLI-CLI-0012", "WB", "cli"));
        Assert.IsFalse(defaultPolicy.MatchesArtifactId("architecture", "ARC-WB-CLI-ABCD", "WB", "cli"));
        Assert.IsFalse(defaultPolicy.MatchesArtifactId("architecture", "ARC-WB-CLI", "WB", "cli"));
        Assert.IsFalse(defaultPolicy.MatchesArtifactId("architecture", "ARC-WB-CLI-0012", null, "cli"));
        Assert.IsFalse(defaultPolicy.MatchesArtifactId("architecture", string.Empty, "WB", "cli"));
        Assert.IsTrue(defaultPolicy.MatchesArtifactId("unknown", "ANYTHING", "WB", null));

        var customPolicy = new ArtifactIdPolicy(
            3,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["verification"] = "VER-{sequence}"
            });

        Assert.IsTrue(customPolicy.MatchesArtifactId("verification", "VER-007", "WB", null));
        Assert.IsTrue(customPolicy.MatchesArtifactId("verification", "VER-ABC", "WB", null));
    }

    [TestMethod]
    public void BuildArtifactId_AndPrefix_ThrowWhenTemplateMissing()
    {
        var policy = new ArtifactIdPolicy(4, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Throws<InvalidOperationException>(() => policy.BuildArtifactId("unsupported-type", "WB", null, 1));
        Assert.Throws<InvalidOperationException>(() => policy.BuildArtifactIdPrefix("unsupported-type", "WB", null));
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        return new TempRepoRoot(repoRoot);
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
