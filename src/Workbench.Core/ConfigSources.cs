namespace Workbench
{
    public sealed record ConfigSources(
        [property: JsonPropertyName("defaults")] bool Defaults,
        [property: JsonPropertyName("repoConfig")] string RepoConfig);
}
