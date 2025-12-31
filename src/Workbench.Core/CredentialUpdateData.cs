namespace Workbench
{
    public sealed record CredentialUpdateData(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("created")] bool Created,
        [property: JsonPropertyName("updated")] bool Updated,
        [property: JsonPropertyName("removed")] bool Removed);
}
