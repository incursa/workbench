namespace Workbench.Core;

/// <summary>
/// Stable result of the local finish workflow.
/// </summary>
public sealed record FinishData(
    [property: JsonPropertyName("workItemId")] string WorkItemId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("previousStatus")] string PreviousStatus,
    [property: JsonPropertyName("finalStatus")] string FinalStatus,
    [property: JsonPropertyName("statusChanged")] bool StatusChanged,
    [property: JsonPropertyName("evidenceChanged")] bool EvidenceChanged,
    [property: JsonPropertyName("dryRun")] bool DryRun,
    [property: JsonPropertyName("navigation")] FinishNavigationData Navigation,
    [property: JsonPropertyName("validation")] FinishValidationData Validation,
    [property: JsonPropertyName("recommendedNextAction")] string? RecommendedNextAction)
{
    /// <summary>
    /// Version of the finish JSON data contract.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 2;
}
