namespace Workbench
{
    public sealed record ConfigData(
        [property: JsonPropertyName("config")] WorkbenchConfig Config,
        [property: JsonPropertyName("sources")] ConfigSources Sources);
}