namespace Workbench.Core;

/// <summary>
/// Payload describing navigation sync results.
/// </summary>
/// <param name="DocsUpdated">Number of docs updated.</param>
/// <param name="ItemsUpdated">Number of items updated.</param>
/// <param name="IndexFilesUpdated">Number of index files updated.</param>
/// <param name="MissingDocs">Referenced docs not found.</param>
/// <param name="MissingItems">Referenced work items not found.</param>
/// <param name="Warnings">Warnings emitted during sync.</param>
public sealed record NavSyncData(
    [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
    [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
    [property: JsonPropertyName("indexFilesUpdated")] int IndexFilesUpdated,
    [property: JsonPropertyName("missingDocs")] IList<string> MissingDocs,
    [property: JsonPropertyName("missingItems")] IList<string> MissingItems,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);
