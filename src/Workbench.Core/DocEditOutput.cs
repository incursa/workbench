namespace Workbench.Core;

/// <summary>
/// JSON response envelope for doc edit output.
/// </summary>
/// <param name="Ok">True when the edit succeeded.</param>
/// <param name="Data">Structured edit result payload.</param>
public sealed record DocEditOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] DocEditData Data);
