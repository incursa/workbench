using System.Text.Json.Serialization;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record QualityProofHealthOptions(
    string? ContractPath,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> GapPaths,
    IReadOnlyList<string> DefaultRequiredEvidenceKinds);

public sealed record QualityProofHealthResult(
    QualityProofHealthData Data,
    TestInventory Inventory);

public sealed record QualityProofHealthSummary(
    [property: JsonPropertyName("requirements")] int Requirements,
    [property: JsonPropertyName("states")] IReadOnlyDictionary<string, int> States,
    [property: JsonPropertyName("workQueueTags")] IReadOnlyDictionary<string, int> WorkQueueTags);

public sealed record QualityProofHealthEvidence(
    [property: JsonPropertyName("required")] IList<string> Required,
    [property: JsonPropertyName("observed")] IList<string> Observed,
    [property: JsonPropertyName("missing")] IList<string> Missing,
    [property: JsonPropertyName("coverageContractSource")] string CoverageContractSource,
    [property: JsonPropertyName("coverageContract")] IReadOnlyDictionary<string, string> CoverageContract,
    [property: JsonPropertyName("focusedTestRefs")] IList<string> FocusedTestRefs,
    [property: JsonPropertyName("broadTestRefs")] IList<string> BroadTestRefs,
    [property: JsonPropertyName("specTestRefs")] IList<string> SpecTestRefs,
    [property: JsonPropertyName("unresolvedSpecTestRefs")] IList<string> UnresolvedSpecTestRefs);

public sealed record QualityProofHealthRequirement(
    [property: JsonPropertyName("requirementId")] string RequirementId,
    [property: JsonPropertyName("artifactId")] string ArtifactId,
    [property: JsonPropertyName("artifactPath")] string ArtifactPath,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("workQueueTags")] IList<string> WorkQueueTags,
    [property: JsonPropertyName("evidence")] QualityProofHealthEvidence Evidence,
    [property: JsonPropertyName("notes")] IList<string> Notes);

public sealed record QualityProofHealthData(
    [property: JsonPropertyName("summary")] QualityProofHealthSummary Summary,
    [property: JsonPropertyName("requirements")] IList<QualityProofHealthRequirement> Requirements,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record QualityProofHealthOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] QualityProofHealthData Data);

#pragma warning restore MA0048
