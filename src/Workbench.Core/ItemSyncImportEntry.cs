namespace Workbench;

public sealed record ItemSyncImportEntry(
    [property: JsonPropertyName("issue")] GithubIssuePayload Issue,
    [property: JsonPropertyName("item")] WorkItemPayload? Item);
