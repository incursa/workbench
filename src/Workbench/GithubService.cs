using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Workbench;

public static class GithubService
{
    public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);
    public sealed record AuthStatus(string Status, string? Reason, string? Version);

    public static CommandResult Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo("gh")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start gh.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static AuthStatus CheckAuthStatus(string repoRoot, string? host = null)
    {
        try
        {
            var versionResult = Run(repoRoot, "--version");
            var version = versionResult.ExitCode == 0 ? versionResult.StdOut : null;
            if (versionResult.ExitCode != 0)
            {
                return new AuthStatus("warn", versionResult.StdErr.Length > 0 ? versionResult.StdErr : "gh --version failed.", version);
            }

            var authArgs = new List<string> { "auth", "status" };
            if (!string.IsNullOrWhiteSpace(host) && !string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                authArgs.Add("--hostname");
                authArgs.Add(host);
            }
            var authResult = Run(repoRoot, authArgs.ToArray());
            if (authResult.ExitCode != 0)
            {
                var reason = authResult.StdErr.Length > 0 ? authResult.StdErr : "gh auth status failed.";
                return new AuthStatus("warn", reason, version);
            }

            return new AuthStatus("ok", null, version);
        }
        catch (Exception)
        {
#pragma warning disable ERP022
            return new AuthStatus("skip", "gh not installed or not on PATH.", null);
#pragma warning restore ERP022
        }
    }

    public static void EnsureAuthenticated(string repoRoot, string? host = null)
    {
        AuthStatus status;
        try
        {
            status = CheckAuthStatus(repoRoot, host);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"gh auth check failed: {ex}");
        }

        if (string.Equals(status.Status, "skip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("gh is not installed or not on PATH.");
        }

        if (string.Equals(status.Status, "warn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("gh is installed but not authenticated. Run `gh auth login`.");
        }
    }

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

    public static GithubIssue FetchIssue(string repoRoot, GithubIssueRef issueRef)
    {
        EnsureAuthenticated(repoRoot, issueRef.Repo.Host);

        const string Query = """
            query($owner: String!, $name: String!, $number: Int!) {
              repository(owner: $owner, name: $name) {
                issue(number: $number) {
                  number
                  title
                  body
                  url
                  state
                  labels(first: 100) { nodes { name } }
                  timelineItems(first: 100, itemTypes: [CROSS_REFERENCED_EVENT, CONNECTED_EVENT, CLOSED_EVENT]) {
                    nodes {
                      __typename
                      ... on CrossReferencedEvent {
                        source {
                          __typename
                          ... on PullRequest { url }
                        }
                      }
                      ... on ConnectedEvent {
                        subject {
                          __typename
                          ... on PullRequest { url }
                        }
                      }
                      ... on ClosedEvent {
                        closer {
                          __typename
                          ... on PullRequest { url }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var args = new List<string> { "api" };
        if (!string.IsNullOrWhiteSpace(issueRef.Repo.Host) && !string.Equals(issueRef.Repo.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(issueRef.Repo.Host);
        }
        args.AddRange(new[]
        {
            "graphql",
            "-f", $"query={Query}",
            "-f", $"owner={issueRef.Repo.Owner}",
            "-f", $"name={issueRef.Repo.Repo}",
            "-F", $"number={issueRef.Number.ToString(CultureInfo.InvariantCulture)}",
        });

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh api graphql failed.");
        }

        using var document = JsonDocument.Parse(result.StdOut);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("repository", out var repoElement)
            || !repoElement.TryGetProperty("issue", out var issueElement)
            || issueElement.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"Issue not found: {issueRef.Repo.Owner}/{issueRef.Repo.Repo}#{issueRef.Number}");
        }

        var title = issueElement.GetProperty("title").GetString() ?? string.Empty;
        var body = issueElement.GetProperty("body").GetString() ?? string.Empty;
        var url = issueElement.GetProperty("url").GetString() ?? string.Empty;
        var state = issueElement.GetProperty("state").GetString() ?? string.Empty;

        var labels = new List<string>();
        if (issueElement.TryGetProperty("labels", out var labelsElement)
            && labelsElement.TryGetProperty("nodes", out var labelNodes)
            && labelNodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var label in labelNodes.EnumerateArray())
            {
                if (label.TryGetProperty("name", out var labelName))
                {
                    var value = labelName.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        labels.Add(value);
                    }
                }
            }
        }

        var pullRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (issueElement.TryGetProperty("timelineItems", out var timeline)
            && timeline.TryGetProperty("nodes", out var timelineNodes)
            && timelineNodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in timelineNodes.EnumerateArray())
            {
                if (!node.TryGetProperty("__typename", out var typeElement))
                {
                    continue;
                }
                var type = typeElement.GetString();
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                JsonElement? prElement = type switch
                {
                    "CrossReferencedEvent" => TryGetPullRequestElement(node, "source"),
                    "ConnectedEvent" => TryGetPullRequestElement(node, "subject"),
                    "ClosedEvent" => TryGetPullRequestElement(node, "closer"),
                    _ => null,
                };

                if (prElement is { ValueKind: JsonValueKind.Object } && prElement.Value.TryGetProperty("url", out var prUrl))
                {
                    var value = prUrl.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        pullRequests.Add(value);
                    }
                }
            }
        }

        return new GithubIssue(
            issueRef.Repo,
            issueRef.Number,
            title,
            body,
            url,
            state,
            labels,
            pullRequests.ToList());
    }

    public static IList<GithubIssue> ListIssues(string repoRoot, GithubRepoRef repo, int limit = 1000)
    {
        EnsureAuthenticated(repoRoot, repo.Host);

        var args = new List<string> { "issue", "list", "--state", "all", "--limit", limit.ToString(CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(repo.Host) && !string.Equals(repo.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(repo.Host);
        }
        args.Add("--repo");
        args.Add($"{repo.Owner}/{repo.Repo}");
        args.Add("--json");
        args.Add("number,title,body,state,url,labels");

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh issue list failed.");
        }

        var issues = new List<GithubIssue>();
        using var document = JsonDocument.Parse(result.StdOut);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return issues;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("number", out var numberElement)
                || numberElement.ValueKind != JsonValueKind.Number
                || !numberElement.TryGetInt32(out var number))
            {
                continue;
            }

            var title = element.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty;
            var body = element.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
            var url = element.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
            var state = element.TryGetProperty("state", out var stateElement) ? stateElement.GetString() ?? string.Empty : string.Empty;

            var labels = new List<string>();
            if (element.TryGetProperty("labels", out var labelsElement) && labelsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var label in labelsElement.EnumerateArray())
                {
                    if (label.TryGetProperty("name", out var nameElement))
                    {
                        var value = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            labels.Add(value);
                        }
                    }
                }
            }

            issues.Add(new GithubIssue(repo, number, title, body, url, state, labels, Array.Empty<string>()));
        }

        return issues;
    }

    public static string CreateIssue(string repoRoot, GithubRepoRef repo, string title, string body, IEnumerable<string> labels)
    {
        EnsureAuthenticated(repoRoot, repo.Host);

        var args = new List<string> { "issue", "create", "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(repo.Host) && !string.Equals(repo.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(repo.Host);
        }
        args.Add("--repo");
        args.Add($"{repo.Owner}/{repo.Repo}");

        foreach (var label in labels.Where(label => !string.IsNullOrWhiteSpace(label)))
        {
            args.Add("--label");
            args.Add(label);
        }

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh issue create failed.");
        }

        return result.StdOut.Trim();
    }

    public static void UpdateIssue(string repoRoot, GithubIssueRef issueRef, string title, string body)
    {
        EnsureAuthenticated(repoRoot, issueRef.Repo.Host);

        var args = new List<string> { "issue", "edit", issueRef.Number.ToString(CultureInfo.InvariantCulture), "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(issueRef.Repo.Host) && !string.Equals(issueRef.Repo.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(issueRef.Repo.Host);
        }
        args.Add("--repo");
        args.Add($"{issueRef.Repo.Owner}/{issueRef.Repo.Repo}");

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh issue edit failed.");
        }
    }

    public static string CreatePullRequest(string repoRoot, string title, string body, string? baseBranch, bool draft)
    {
        EnsureAuthenticated(repoRoot);

        var args = new List<string> { "pr", "create", "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            args.Add("--base");
            args.Add(baseBranch);
        }
        if (draft)
        {
            args.Add("--draft");
        }

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh pr create failed.");
        }
        return result.StdOut.Trim();
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

    private static JsonElement? TryGetPullRequestElement(JsonElement node, string property)
    {
        if (node.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("__typename", out var typeElement)
            && string.Equals(typeElement.GetString(), "PullRequest", StringComparison.OrdinalIgnoreCase))
        {
            return element;
        }
        return null;
    }
}
