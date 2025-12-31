namespace Workbench;

public sealed record ItemImportEntry(
    [property: JsonPropertyName("issue")] GithubIssuePayload Issue,
    [property: JsonPropertyName("item")] WorkItemPayload Item);
