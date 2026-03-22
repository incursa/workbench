namespace Workbench.Core;

/// <summary>
/// Payload for work item close operations.
/// </summary>
/// <param name="Item">Updated work item payload.</param>
public sealed record ItemCloseData(
    [property: JsonPropertyName("item")] WorkItemPayload Item);
