namespace Workbench.Core;

/// <summary>
/// JSON response envelope for Codex run output.
/// </summary>
/// <param name="Ok">True when launch succeeded.</param>
/// <param name="Data">Run details.</param>
public sealed record CodexRunOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] CodexRunData Data);
