using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Workbench;

public sealed class GhCliGithubProvider : IGithubProvider
{
    private const string DefaultHost = "github.com";

    private sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

    public async Task<GithubService.AuthStatus> CheckAuthStatusAsync(string repoRoot, string? host = null)
    {
        try
        {
            var versionResult = await RunAsync(repoRoot, "--version").ConfigureAwait(false);
            var version = versionResult.ExitCode == 0 ? versionResult.StdOut : null;
            if (versionResult.ExitCode != 0)
            {
                return new GithubService.AuthStatus("warn", versionResult.StdErr.Length > 0 ? versionResult.StdErr : "gh --version failed.", version);
            }

            var authArgs = new List<string> { "auth", "status" };
            if (!string.IsNullOrWhiteSpace(host) && !string.Equals(host, DefaultHost, StringComparison.OrdinalIgnoreCase))
            {
                authArgs.Add("--hostname");
                authArgs.Add(host);
            }
            var authResult = await RunAsync(repoRoot, authArgs.ToArray()).ConfigureAwait(false);
            if (authResult.ExitCode != 0)
            {
                var reason = authResult.StdErr.Length > 0 ? authResult.StdErr : "gh auth status failed.";
                return new GithubService.AuthStatus("warn", reason, version);
            }

            return new GithubService.AuthStatus("ok", null, version);
        }
        catch (Exception)
        {
#pragma warning disable ERP022
            return new GithubService.AuthStatus("skip", "gh not installed or not on PATH.", null);
#pragma warning restore ERP022
        }
    }

    public async Task EnsureAuthenticatedAsync(string repoRoot, string? host = null)
    {
        GithubService.AuthStatus status;
        try
        {
            status = await CheckAuthStatusAsync(repoRoot, host).ConfigureAwait(false);
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

    public async Task<GithubIssue> FetchIssueAsync(string repoRoot, GithubIssueRef issueRef)
    {
        await EnsureAuthenticatedAsync(repoRoot, issueRef.Repo.Host).ConfigureAwait(false);

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
        if (!string.IsNullOrWhiteSpace(issueRef.Repo.Host) && !string.Equals(issueRef.Repo.Host, DefaultHost, StringComparison.OrdinalIgnoreCase))
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

        var result = await RunAsync(repoRoot, args.ToArray()).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh api graphql failed.");
        }

        return ParseIssueGraphql(result.StdOut, issueRef);
    }

    public async Task<IList<GithubIssue>> ListIssuesAsync(string repoRoot, GithubRepoRef repo, int limit = 1000)
    {
        await EnsureAuthenticatedAsync(repoRoot, repo.Host).ConfigureAwait(false);

        var args = new List<string> { "issue", "list", "--state", "all", "--limit", limit.ToString(CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(repo.Host) && !string.Equals(repo.Host, DefaultHost, StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(repo.Host);
        }
        args.Add("--repo");
        args.Add($"{repo.Owner}/{repo.Repo}");
        args.Add("--json");
        args.Add("number,title,body,state,url,labels");

        var result = await RunAsync(repoRoot, args.ToArray()).ConfigureAwait(false);
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

    public async Task<string> CreateIssueAsync(string repoRoot, GithubRepoRef repo, string title, string body, IEnumerable<string> labels)
    {
        await EnsureAuthenticatedAsync(repoRoot, repo.Host).ConfigureAwait(false);

        var args = new List<string> { "issue", "create", "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(repo.Host) && !string.Equals(repo.Host, DefaultHost, StringComparison.OrdinalIgnoreCase))
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

        var result = await RunAsync(repoRoot, args.ToArray()).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh issue create failed.");
        }

        return result.StdOut.Trim();
    }

    public async Task UpdateIssueAsync(string repoRoot, GithubIssueRef issueRef, string title, string body)
    {
        await EnsureAuthenticatedAsync(repoRoot, issueRef.Repo.Host).ConfigureAwait(false);

        var args = new List<string> { "issue", "edit", issueRef.Number.ToString(CultureInfo.InvariantCulture), "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(issueRef.Repo.Host) && !string.Equals(issueRef.Repo.Host, DefaultHost, StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--hostname");
            args.Add(issueRef.Repo.Host);
        }
        args.Add("--repo");
        args.Add($"{issueRef.Repo.Owner}/{issueRef.Repo.Repo}");

        var result = await RunAsync(repoRoot, args.ToArray()).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh issue edit failed.");
        }
    }

    public async Task<string> CreatePullRequestAsync(string repoRoot, GithubRepoRef repo, string title, string body, string? baseBranch, bool draft)
    {
        await EnsureAuthenticatedAsync(repoRoot).ConfigureAwait(false);

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

        var result = await RunAsync(repoRoot, args.ToArray()).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh pr create failed.");
        }
        return result.StdOut.Trim();
    }

    private static async Task<CommandResult> RunAsync(string repoRoot, params string[] args)
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
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    private static GithubIssue ParseIssueGraphql(string json, GithubIssueRef issueRef)
    {
        using var document = JsonDocument.Parse(json);
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
