using System.Text.Json.Nodes;
using Workbench.Core;
using Workbench.SpecAttestProvider;

namespace Workbench.SpecAttestProvider.Tests;

[TestClass]
public sealed class SpecAttestConformanceTests
{
    [TestMethod]
    public void StandardPin_ExactlyMatchesPublishedDraft2Tuple()
    {
        ResourceCompatibilityPin pin = SpecAttestInterchangeContract.StandardPin();

        Assert.AreEqual("0.1.0-draft.2", pin.Edition);
        Assert.AreEqual("3dedb948549ee3bc07adc7360e80f9c4125076a0", pin.Revision);
        Assert.AreEqual("27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb", pin.SchemaSha256);
        Assert.AreEqual("885cb5168dcdd528b53046c6e50fe65ca4ebd8aa903ea4867b4e78b12f92abea", pin.EditionManifestSha256);
        Assert.AreEqual("eb12cb68f01bd03cb2c29ae549ca8d202a129f75c304ebcbf1b4e64a975048ef", pin.EditionManifestSchemaSha256);
        Assert.AreEqual("0643a8067ed5684dcbaa29da5c9a206af813c142d1a7620bce2a81bddedd4b92", pin.PayloadSha256);
        Assert.AreEqual("19498f4987e2babde1e8151c9886bc74ac491aca44f5ecdfb7af6added24cc87", pin.ConformanceCorpusSha256);
        Assert.AreEqual("refs/tags/standard-v0.1.0-draft.2", pin.SignatureReference);
        Assert.AreEqual("607d32b380053b7cb2c748e0e81dd0d3dd943f2d9a71df7dc2ff8575fa88c865", pin.SignatureSha256);
    }

    public static IEnumerable<object[]> PublishedDraft2ProtocolVectors
    {
        get
        {
            yield return Vector("""{"case":"protocol-atomic-failure","kind":"safe-mutation-protocol","valid":true,"scenario":"atomic-failure","restOutcome":"rejected","mcpOutcome":"rejected","partialMutation":false}""");
            yield return Vector("""{"case":"protocol-cancel","kind":"safe-mutation-protocol","valid":true,"scenario":"cancel","restOutcome":"canceled","mcpOutcome":"canceled","partialMutation":false}""");
            yield return Vector("""{"case":"protocol-deterministic-replay","kind":"safe-mutation-protocol","valid":true,"scenario":"deterministic-replay","restOutcome":"staged","mcpOutcome":"staged","serverDefaultsExcluded":true,"logicalRequest":{"idempotencyKey":"qualification-replay-001","expectedProjectVersion":4,"requirements":[{"id":"REQ-DEMO-0003","revision":11}]},"replayedLogicalRequest":{"requirements":[{"revision":11,"id":"REQ-DEMO-0003"}],"expectedProjectVersion":4,"idempotencyKey":"qualification-replay-001"}}""");
            yield return Vector("""{"case":"protocol-resume","kind":"safe-mutation-protocol","valid":true,"scenario":"resume","restOutcome":"committed","mcpOutcome":"committed","atomicCommit":true}""");
            yield return Vector("""{"case":"protocol-stale-commit","kind":"safe-mutation-protocol","valid":true,"scenario":"stale-commit","restOutcome":"rejected","mcpOutcome":"rejected","partialMutation":false}""");
            yield return Vector("""{"case":"revision-storage-version-divergence","kind":"interchange-roundtrip","valid":true,"providerRecordVersion":1,"inputRequirement":{"id":"REQ-DEMO-0001","revision":7,"trace":{"supersedes":["REQ-DEMO-0000"]}},"outputRequirement":{"id":"REQ-DEMO-0001","revision":7,"trace":{"supersedes":["REQ-DEMO-0000"]}}}""");
            yield return Vector("""{"case":"canonical-reorder-equivalence","kind":"interchange-roundtrip","valid":true,"providerRecordVersion":3,"inputRequirement":{"id":"REQ-DEMO-0002","revision":9,"trace":{"derived_from":["REQ-DEMO-0001"],"supersedes":["REQ-DEMO-0000"]}},"outputRequirement":{"trace":{"supersedes":["REQ-DEMO-0000"],"derived_from":["REQ-DEMO-0001"]},"revision":9,"id":"REQ-DEMO-0002"},"canonicalA":{"id":"REQ-DEMO-0002","revision":9,"statement":"A demo MUST preserve meaning."},"canonicalB":{"statement":"A demo MUST preserve meaning.","revision":9,"id":"REQ-DEMO-0002"}}""");
        }
    }

    [TestMethod]
    [DynamicData(nameof(PublishedDraft2ProtocolVectors))]
    public void Runner_AgreesWithPublishedDraft2ProtocolVector(string name, JsonObject vector)
    {
        Assert.IsTrue(SpecAttestConformanceCorpusRunner.Validate(vector), name);
    }

    [TestMethod]
    public void Runner_FailsClosedWhenExpectedOutcomeDoesNotMatch()
    {
        using TempDirectory corpus = new();
        File.WriteAllText(
            Path.Combine(corpus.Path, "altered.json"),
            """{"kind":"safe-mutation-protocol","valid":true,"scenario":"stale-commit","restOutcome":"committed","mcpOutcome":"committed","partialMutation":true}""");

        Assert.Throws<InvalidOperationException>(() => SpecAttestConformanceCorpusRunner.Run(corpus.Path));
    }

    private static object[] Vector(string json)
    {
        JsonObject vector = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Test vector is not an object.");
        return [vector["case"]?.GetValue<string>() ?? string.Empty, vector];
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-specattest-corpus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
