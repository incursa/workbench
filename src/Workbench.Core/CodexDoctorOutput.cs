namespace Workbench.Core;

/// <summary>
/// JSON response envelope for Codex doctor output.
/// </summary>
/// <param name="Ok">True when Codex is available.</param>
/// <param name="Data">Resolved availability data.</param>
public sealed record CodexDoctorOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] CodexDoctorData Data);
