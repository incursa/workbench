using System.Text.Json.Serialization;

namespace Workbench;

public sealed record DocLinkData(
    [property: JsonPropertyName("docPath")] string DocPath,
    [property: JsonPropertyName("docType")] string DocType,
    [property: JsonPropertyName("workItems")] IReadOnlyList<string> WorkItems,
    [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
    [property: JsonPropertyName("docUpdated")] bool DocUpdated);
