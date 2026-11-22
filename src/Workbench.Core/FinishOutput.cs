namespace Workbench.Core;

/// <summary>
/// JSON response envelope for the finish command.
/// </summary>
public sealed record FinishOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] FinishData Data);
