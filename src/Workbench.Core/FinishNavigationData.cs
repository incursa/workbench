namespace Workbench.Core;

/// <summary>
/// Local generated-view changes produced by finish.
/// </summary>
public sealed record FinishNavigationData(
    [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
    [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
    [property: JsonPropertyName("indexFilesUpdated")] int IndexFilesUpdated,
    [property: JsonPropertyName("missingDocs")] IList<string> MissingDocs,
    [property: JsonPropertyName("missingItems")] IList<string> MissingItems,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);
