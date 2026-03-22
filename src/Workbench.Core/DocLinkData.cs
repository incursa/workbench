namespace Workbench.Core;

/// <summary>
/// Payload describing doc link/unlink results.
/// </summary>
/// <param name="DocPath">Normalized doc link.</param>
/// <param name="DocType">Doc type for the linked artifact.</param>
/// <param name="WorkItems">Work item IDs processed.</param>
/// <param name="ItemsUpdated">Count of work items updated.</param>
/// <param name="DocUpdated">True when the doc file was updated.</param>
public sealed record DocLinkData(
    [property: JsonPropertyName("docPath")] string DocPath,
    [property: JsonPropertyName("docType")] string DocType,
    [property: JsonPropertyName("workItems")] IReadOnlyList<string> WorkItems,
    [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
    [property: JsonPropertyName("docUpdated")] bool DocUpdated);
