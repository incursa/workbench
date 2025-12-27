namespace Workbench
{
    public sealed record BoardData(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("counts")] IDictionary<string, int> Counts);
}
