namespace Workbench.Core;

/// <summary>
/// Payload for doctor command JSON output.
/// </summary>
/// <param name="RepoRoot">Resolved repository root.</param>
/// <param name="Checks">List of executed checks.</param>
public sealed record DoctorData(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("repoRoot")] string RepoRoot,
    [property: JsonPropertyName("checks")] IList<DoctorCheck> Checks,
    [property: JsonPropertyName("fixRequested")] bool FixRequested,
    [property: JsonPropertyName("repairs")] IList<string> Repairs,
    [property: JsonPropertyName("recommendedNextAction")] string? RecommendedNextAction);
