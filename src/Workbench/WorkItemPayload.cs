namespace Workbench
{
    public sealed record WorkItemPayload(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("priority")] string? Priority,
        [property: JsonPropertyName("owner")] string? Owner,
        [property: JsonPropertyName("created")] string Created,
        [property: JsonPropertyName("updated")] string? Updated,
        [property: JsonPropertyName("tags")] IList<string> Tags,
        [property: JsonPropertyName("related")] RelatedLinksPayload Related,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("body")] string? Body);
}
