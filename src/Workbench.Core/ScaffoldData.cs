namespace Workbench
{
    public sealed record ScaffoldData(
        [property: JsonPropertyName("created")] IList<string> Created,
        [property: JsonPropertyName("skipped")] IList<string> Skipped,
        [property: JsonPropertyName("configPath")] string ConfigPath);
}
