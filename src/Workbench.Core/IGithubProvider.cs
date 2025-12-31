namespace Workbench;

public interface IGithubProvider
{
    Task<GithubService.AuthStatus> CheckAuthStatusAsync(string repoRoot, string? host = null);
    Task EnsureAuthenticatedAsync(string repoRoot, string? host = null);
    Task<GithubIssue> FetchIssueAsync(string repoRoot, GithubIssueRef issueRef);
    Task<IList<GithubIssue>> ListIssuesAsync(string repoRoot, GithubRepoRef repo, int limit = 1000);
    Task<string> CreateIssueAsync(string repoRoot, GithubRepoRef repo, string title, string body, IEnumerable<string> labels);
    Task UpdateIssueAsync(string repoRoot, GithubIssueRef issueRef, string title, string body);
    Task<string> CreatePullRequestAsync(string repoRoot, GithubRepoRef repo, string title, string body, string? baseBranch, bool draft);
}
