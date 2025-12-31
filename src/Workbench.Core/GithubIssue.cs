namespace Workbench;

public sealed record GithubIssue(
    GithubRepoRef Repo,
    int Number,
    string Title,
    string Body,
    string Url,
    string State,
    IList<string> Labels,
    IList<string> PullRequests);
