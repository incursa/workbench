namespace Workbench
{
    public sealed record DocCreateData(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("workItems")] IList<string> WorkItems);
}
