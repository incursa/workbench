using System.Text.Json.Serialization;

namespace Workbench
{
    public sealed record NavSyncData(
        [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
        [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
        [property: JsonPropertyName("indexFilesUpdated")] int IndexFilesUpdated,
        [property: JsonPropertyName("missingDocs")] IList<string> MissingDocs,
        [property: JsonPropertyName("missingItems")] IList<string> MissingItems,
        [property: JsonPropertyName("warnings")] IList<string> Warnings);
}
