namespace Workbench;

public sealed record GithubIssuePayload(
    [property: JsonPropertyName("repo")] string Repo,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("labels")] IList<string> Labels,
    [property: JsonPropertyName("pullRequests")] IList<string> PullRequests);
