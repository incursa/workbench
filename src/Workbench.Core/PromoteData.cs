namespace Workbench
{
    public sealed record PromoteData(
        [property: JsonPropertyName("item")] WorkItemPayload Item,
        [property: JsonPropertyName("branch")] string Branch,
        [property: JsonPropertyName("commit")] CommitInfo Commit,
        [property: JsonPropertyName("pushed")] bool Pushed,
        [property: JsonPropertyName("pr")] string? Pr);
}
