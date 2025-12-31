namespace Workbench
{
    public sealed record ItemCreateData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("path")] string Path);
}
