namespace Workbench.Core;

/// <summary>
/// Payload describing local Codex CLI availability.
/// </summary>
/// <param name="Available">True when Codex is callable from PATH.</param>
/// <param name="Version">Resolved Codex version when available.</param>
/// <param name="Error">Error message when unavailable.</param>
public sealed record CodexDoctorData(
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("error")] string? Error);
