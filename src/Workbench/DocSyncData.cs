namespace Workbench
{
    public sealed record DocSyncData(
        [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
        [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
        [property: JsonPropertyName("missingDocs")] IList<string> MissingDocs,
        [property: JsonPropertyName("missingItems")] IList<string> MissingItems);
}
