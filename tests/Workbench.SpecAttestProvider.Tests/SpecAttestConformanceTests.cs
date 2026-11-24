using System.Text.Json.Nodes;
using Workbench.Core;
using Workbench.SpecAttestProvider;

namespace Workbench.SpecAttestProvider.Tests;

[TestClass]
public sealed class SpecAttestConformanceTests
{
    [TestMethod]
    public void StandardPin_ExactlyMatchesPublishedRc1Tuple()
    {
        ResourceCompatibilityPin pin = SpecAttestInterchangeContract.StandardPin();

        Assert.AreEqual("0.1.0-rc.1", pin.Edition);
        Assert.AreEqual("afb4885980bdcbe250bb18fd07948c8999450330", pin.Revision);
        Assert.AreEqual("27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb", pin.SchemaSha256);
        Assert.AreEqual("22ef8a5347aaa2b430ff5fbca8495f54a0e078ffbcc12d92d9f38389a47fbe98", pin.EditionManifestSha256);
        Assert.AreEqual("eb12cb68f01bd03cb2c29ae549ca8d202a129f75c304ebcbf1b4e64a975048ef", pin.EditionManifestSchemaSha256);
        Assert.AreEqual("0643a8067ed5684dcbaa29da5c9a206af813c142d1a7620bce2a81bddedd4b92", pin.PayloadSha256);
        Assert.AreEqual("b4ab6783a4693069a9eb5f0f96f01f49ae70bf56bb10ba9d9499a5e9c4b554a2", pin.ConformanceCorpusSha256);
        Assert.AreEqual("refs/tags/standard-v0.1.0-rc.1", pin.SignatureReference);
        Assert.AreEqual("de2dc61fee3b0f4137612696b910d8e6b3455011bd46222d1a8a8cfb731ec844", pin.SignatureSha256);
    }

    public static IEnumerable<object[]> PublishedRc1ProtocolVectors
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
    [DynamicData(nameof(PublishedRc1ProtocolVectors))]
    public void Runner_AgreesWithPublishedRc1ProtocolVector(string name, JsonObject vector)
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
