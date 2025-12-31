namespace Workbench
{
    public sealed record ItemDeleteData(
        [property: JsonPropertyName("item")] WorkItemPayload Item,
        [property: JsonPropertyName("docsUpdated")] int DocsUpdated);
}
