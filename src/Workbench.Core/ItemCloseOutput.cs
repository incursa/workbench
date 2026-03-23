namespace Workbench.Core;

/// <summary>
/// JSON response envelope for closing a work item.
/// </summary>
/// <param name="Ok">True when the item was closed.</param>
/// <param name="Data">Updated item payload.</param>
public sealed record ItemCloseOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] ItemCloseData Data);
