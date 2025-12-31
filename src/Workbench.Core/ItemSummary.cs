namespace Workbench
{
    public sealed record ItemSummary(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("path")] string Path);
}
