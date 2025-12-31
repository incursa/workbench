namespace Workbench
{
    public sealed record ConfigSetData(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("config")] WorkbenchConfig Config,
        [property: JsonPropertyName("changed")] bool Changed);
}
