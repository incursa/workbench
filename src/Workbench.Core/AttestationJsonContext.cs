using System.Text.Json.Serialization;

namespace Workbench.Core;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AttestationRepositoryMetadata))]
[JsonSerializable(typeof(AttestationSelection))]
[JsonSerializable(typeof(AttestationValidationProfileSummary))]
[JsonSerializable(typeof(AttestationValidationFindingSummary))]
[JsonSerializable(typeof(AttestationValidationSummary))]
[JsonSerializable(typeof(AttestationTraceCoverageSummary))]
[JsonSerializable(typeof(AttestationTraceReadinessSummary))]
[JsonSerializable(typeof(AttestationWorkItemStatusSummary))]
[JsonSerializable(typeof(AttestationVerificationStatusSummary))]
[JsonSerializable(typeof(AttestationAggregateSummary))]
[JsonSerializable(typeof(AttestationQualityReportEvidenceSummary))]
[JsonSerializable(typeof(AttestationTestEvidenceSummary))]
[JsonSerializable(typeof(AttestationCoverageEvidenceSummary))]
[JsonSerializable(typeof(AttestationSimpleEvidenceSummary))]
[JsonSerializable(typeof(AttestationExecutionCommandResult))]
[JsonSerializable(typeof(AttestationExecutionSummary))]
[JsonSerializable(typeof(AttestationEvidenceSnapshot))]
[JsonSerializable(typeof(AttestationArtifactSummary))]
[JsonSerializable(typeof(AttestationArtifactCollections))]
[JsonSerializable(typeof(AttestationRequirementTraceSummary))]
[JsonSerializable(typeof(AttestationRequirementLineageSummary))]
[JsonSerializable(typeof(AttestationRequirementDirectRefs))]
[JsonSerializable(typeof(AttestationRequirementTraceReadinessSummary))]
[JsonSerializable(typeof(AttestationRequirementRollupSummary))]
[JsonSerializable(typeof(AttestationRequirementRecord))]
[JsonSerializable(typeof(AttestationGapSummary))]
[JsonSerializable(typeof(AttestationDerivedRollupSummary))]
[JsonSerializable(typeof(AttestationSnapshot))]
[JsonSerializable(typeof(AttestationRunData))]
[JsonSerializable(typeof(AttestationOutput))]
public partial class AttestationJsonContext : JsonSerializerContext
{
}
