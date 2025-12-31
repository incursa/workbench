namespace Workbench
{
    public sealed record CommitInfo(
        [property: JsonPropertyName("sha")] string Sha,
        [property: JsonPropertyName("message")] string Message);
}
