namespace Workbench.Core;

/// <summary>
/// JSON response envelope for structured work item edit operations.
/// </summary>
/// <param name="Ok">True when the edit succeeded.</param>
/// <param name="Data">Structured edit result payload.</param>
public sealed record ItemEditOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] ItemEditData Data);
