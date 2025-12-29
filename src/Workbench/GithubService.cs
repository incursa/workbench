using System.Globalization;
using System.Linq;

namespace Workbench;

public static class GithubService
{
    public sealed record AuthStatus(string Status, string? Reason, string? Version);

    private static readonly Dictionary<string, IGithubProvider> providers = new(StringComparer.OrdinalIgnoreCase);

    public static GithubRepoRef ResolveRepo(string repoRoot, WorkbenchConfig config)
    {
        var repoFromGit = TryResolveRepoFromGit(repoRoot);
        if (repoFromGit is not null)
        {
            return repoFromGit;
        }

        if (!string.IsNullOrWhiteSpace(config.Github.Owner) && !string.IsNullOrWhiteSpace(config.Github.Repository))
        {
            var host = string.IsNullOrWhiteSpace(config.Github.Host) ? "github.com" : config.Github.Host;
            return new GithubRepoRef(host, config.Github.Owner, config.Github.Repository);
        }

        throw new InvalidOperationException("Unable to resolve GitHub repository. Configure github.owner and github.repository in .workbench/config.json or set remote.origin.url.");
    }

    public static Task<AuthStatus> CheckAuthStatusAsync(string repoRoot, WorkbenchConfig config, string? host = null)
    {
        return GetProvider(config).CheckAuthStatusAsync(repoRoot, host);
    }

    public static Task EnsureAuthenticatedAsync(string repoRoot, WorkbenchConfig config, string? host = null)
    {
        return GetProvider(config).EnsureAuthenticatedAsync(repoRoot, host);
    }

    public static GithubIssueRef ParseIssueReference(string input, GithubRepoRef defaultRepo)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Issue reference is empty.");
        }

        var trimmed = input.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 &&
                string.Equals(segments[2], "issues", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segments[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return new GithubIssueRef(new GithubRepoRef(uri.Host, segments[0], segments[1]), number);
            }
            throw new InvalidOperationException($"Unsupported issue URL: {input}");
        }

        var hashIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex > 0)
        {
            var repoPart = trimmed[..hashIndex];
            var numberPart = trimmed[(hashIndex + 1)..];
            if (int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                var repoParts = repoPart.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (repoParts.Length == 2)
                {
                    return new GithubIssueRef(new GithubRepoRef(defaultRepo.Host, repoParts[0], repoParts[1]), number);
                }
            }
        }

        var numberText = trimmed.TrimStart('#');
        if (int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var simpleNumber))
        {
            return new GithubIssueRef(defaultRepo, simpleNumber);
        }

        throw new InvalidOperationException($"Invalid issue reference: {input}");
    }

    public static Task<GithubIssue> FetchIssueAsync(string repoRoot, WorkbenchConfig config, GithubIssueRef issueRef)
    {
        return GetProvider(config).FetchIssueAsync(repoRoot, issueRef);
    }

    public static Task<IList<GithubIssue>> ListIssuesAsync(string repoRoot, WorkbenchConfig config, GithubRepoRef repo, int limit = 1000)
    {
        return GetProvider(config).ListIssuesAsync(repoRoot, repo, limit);
    }

    public static Task<string> CreateIssueAsync(string repoRoot, WorkbenchConfig config, GithubRepoRef repo, string title, string body, IEnumerable<string> labels)
    {
        return GetProvider(config).CreateIssueAsync(repoRoot, repo, title, body, labels);
    }

    public static Task UpdateIssueAsync(string repoRoot, WorkbenchConfig config, GithubIssueRef issueRef, string title, string body)
    {
        return GetProvider(config).UpdateIssueAsync(repoRoot, issueRef, title, body);
    }

    public static Task<string> CreatePullRequestAsync(string repoRoot, WorkbenchConfig config, GithubRepoRef repo, string title, string body, string? baseBranch, bool draft)
    {
        return GetProvider(config).CreatePullRequestAsync(repoRoot, repo, title, body, baseBranch, draft);
    }

    private static GithubRepoRef? TryResolveRepoFromGit(string repoRoot)
    {
        var remote = GitService.Run(repoRoot, "config", "--get", "remote.origin.url");
        if (remote.ExitCode != 0 || string.IsNullOrWhiteSpace(remote.StdOut))
        {
            return null;
        }
        return TryParseRepoUrl(remote.StdOut.Trim());
    }

    private static GithubRepoRef? TryParseRepoUrl(string url)
    {
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var parts = url.Split(':', 2);
            if (parts.Length != 2)
            {
                return null;
            }
            var host = parts[0].Substring("git@".Length);
            return ParseRepoPath(host, parts[1]);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return ParseRepoPath(uri.Host, uri.AbsolutePath);
        }

        return null;
    }

    private static GithubRepoRef? ParseRepoPath(string host, string path)
    {
        var trimmed = path.Trim().Trim('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }
        return new GithubRepoRef(host, parts[0], parts[1]);
    }

    private static IGithubProvider GetProvider(WorkbenchConfig config)
    {
        var providerName = string.IsNullOrWhiteSpace(config.Github.Provider)
            ? "octokit"
            : config.Github.Provider.Trim();
        lock (providers)
        {
            if (!providers.TryGetValue(providerName, out var provider))
            {
                provider = providerName.ToLowerInvariant() switch
                {
                    "octokit" => new OctokitGithubProvider(),
                    "gh" => new GhCliGithubProvider(),
                    _ => throw new InvalidOperationException($"Unsupported GitHub provider '{providerName}'."),
                };
                providers[providerName] = provider;
            }
            return provider;
        }
    }
}
