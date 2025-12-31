using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace Workbench;

public sealed class OctokitGithubProvider : IGithubProvider
{
    private const string DefaultHost = "github.com";
    private const string TokenEnvPrimary = "WORKBENCH_GITHUB_TOKEN";
    private static readonly string[] fallbackTokenKeys = { "GITHUB_TOKEN", "GH_TOKEN" };

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, GitHubClient> clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HttpClient> graphClients = new(StringComparer.OrdinalIgnoreCase);

    public Task<GithubService.AuthStatus> CheckAuthStatusAsync(string repoRoot, string? host = null)
    {
        var version = typeof(GitHubClient).Assembly.GetName().Version?.ToString();
        var token = ResolveToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(new GithubService.AuthStatus("warn", "Missing GitHub token. Set WORKBENCH_GITHUB_TOKEN or GITHUB_TOKEN.", version));
        }
        return Task.FromResult(new GithubService.AuthStatus("ok", null, version));
    }

    public Task EnsureAuthenticatedAsync(string repoRoot, string? host = null)
    {
        var token = ResolveToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Missing GitHub token. Set WORKBENCH_GITHUB_TOKEN or GITHUB_TOKEN.");
        }
        return Task.CompletedTask;
    }

    public async Task<GithubIssue> FetchIssueAsync(string repoRoot, GithubIssueRef issueRef)
    {
        await EnsureAuthenticatedAsync(repoRoot, issueRef.Repo.Host).ConfigureAwait(false);
        var payload = await ExecuteGraphqlAsync(issueRef).ConfigureAwait(false);
        return ParseIssueGraphql(payload, issueRef);
    }

    public async Task<IList<GithubIssue>> ListIssuesAsync(string repoRoot, GithubRepoRef repo, int limit = 1000)
    {
        await EnsureAuthenticatedAsync(repoRoot, repo.Host).ConfigureAwait(false);
        var client = GetClient(repo.Host);
        var request = new RepositoryIssueRequest
        {
            State = ItemStateFilter.All
        };

        var pageCount = (int)Math.Ceiling(limit / 100.0);
        var options = new ApiOptions
        {
            PageSize = 100,
            PageCount = pageCount,
            StartPage = 1
        };

        var issues = await client.Issue.GetAllForRepository(repo.Owner, repo.Repo, request, options).ConfigureAwait(false);

        var items = new List<GithubIssue>();
        foreach (var issue in issues.Take(limit))
        {
            var labels = issue.Labels.Select(label => label.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
            var state = issue.State.ToString().ToLowerInvariant();
            items.Add(new GithubIssue(
                repo,
                issue.Number,
                issue.Title ?? string.Empty,
                issue.Body ?? string.Empty,
                issue.HtmlUrl?.ToString() ?? string.Empty,
                state,
                labels,
                Array.Empty<string>()));
        }

        return items;
    }

    public async Task<string> CreateIssueAsync(string repoRoot, GithubRepoRef repo, string title, string body, IEnumerable<string> labels)
    {
        await EnsureAuthenticatedAsync(repoRoot, repo.Host).ConfigureAwait(false);
        var client = GetClient(repo.Host);
        var newIssue = new NewIssue(title)
        {
            Body = body
        };
        foreach (var label in labels.Where(label => !string.IsNullOrWhiteSpace(label)))
        {
            newIssue.Labels.Add(label);
        }

        var issue = await client.Issue.Create(repo.Owner, repo.Repo, newIssue).ConfigureAwait(false);
        return issue.HtmlUrl?.ToString() ?? string.Empty;
    }

    public async Task UpdateIssueAsync(string repoRoot, GithubIssueRef issueRef, string title, string body)
    {
        await EnsureAuthenticatedAsync(repoRoot, issueRef.Repo.Host).ConfigureAwait(false);
        var client = GetClient(issueRef.Repo.Host);
        var update = new IssueUpdate
        {
            Title = title,
            Body = body
        };
        await client.Issue.Update(issueRef.Repo.Owner, issueRef.Repo.Repo, issueRef.Number, update).ConfigureAwait(false);
    }

    public async Task<string> CreatePullRequestAsync(string repoRoot, GithubRepoRef repo, string title, string body, string? baseBranch, bool draft)
    {
        await EnsureAuthenticatedAsync(repoRoot, repo.Host).ConfigureAwait(false);
        var client = GetClient(repo.Host);
        var head = GitService.GetCurrentBranch(repoRoot);
        var newPr = new NewPullRequest(title, head, baseBranch ?? "main")
        {
            Body = body,
            Draft = draft
        };

        var pr = await client.PullRequest.Create(repo.Owner, repo.Repo, newPr).ConfigureAwait(false);
        return pr.HtmlUrl?.ToString() ?? string.Empty;
    }

    private GitHubClient GetClient(string? host)
    {
        var normalizedHost = NormalizeHost(host);
        lock (syncRoot)
        {
            if (!clients.TryGetValue(normalizedHost, out var client))
            {
                client = CreateClient(normalizedHost);
                clients[normalizedHost] = client;
            }
            return client;
        }
    }

    private static GitHubClient CreateClient(string host)
    {
        var product = new ProductHeaderValue("Workbench");
        var client = string.Equals(host, DefaultHost, StringComparison.OrdinalIgnoreCase)
            ? new GitHubClient(product)
            : new GitHubClient(product, new Uri($"https://{host}/api/v3"));

        var token = ResolveToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

    private HttpClient GetGraphClient(string? host)
    {
        var normalizedHost = NormalizeHost(host);
        lock (syncRoot)
        {
            if (!graphClients.TryGetValue(normalizedHost, out var client))
            {
                client = CreateGraphClient(normalizedHost);
                graphClients[normalizedHost] = client;
            }
            return client;
        }
    }

    private static HttpClient CreateGraphClient(string host)
    {
        var token = ResolveToken();
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Workbench", "0.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        client.BaseAddress = new Uri(GetGraphqlEndpoint(host));
        return client;
    }

    private static string NormalizeHost(string? host)
    {
        return string.IsNullOrWhiteSpace(host) ? DefaultHost : host;
    }

    private static string GetGraphqlEndpoint(string host)
    {
        return string.Equals(host, DefaultHost, StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com/graphql"
            : $"https://{host}/api/graphql";
    }

    private static string? ResolveToken()
    {
        var token = Environment.GetEnvironmentVariable(TokenEnvPrimary);
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }
        foreach (var key in fallbackTokenKeys)
        {
            token = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }
        return null;
    }

    private async Task<string> ExecuteGraphqlAsync(GithubIssueRef issueRef)
    {
        const string Query = """
            query($owner: String!, $name: String!, $number: Int!) {
              repository(owner: $owner, name: $name) {
                issueOrPullRequest(number: $number) {
                  __typename
                  ... on Issue {
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
                  ... on PullRequest {
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
            }
            """;

        var request = new GraphqlRequest(
            Query,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["owner"] = issueRef.Repo.Owner,
                ["name"] = issueRef.Repo.Repo,
                ["number"] = issueRef.Number
            });

        var payload = JsonSerializer.Serialize(request);
        var client = GetGraphClient(issueRef.Repo.Host);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(string.Empty, content).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(!string.IsNullOrWhiteSpace(responseBody) ? responseBody : "GitHub GraphQL request failed.");
        }
        return responseBody;
    }

    private static GithubIssue ParseIssueGraphql(string json, GithubIssueRef issueRef)
    {
        using var document = JsonDocument.Parse(json);
        var errorMessage = TryReadGraphqlErrors(document.RootElement);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("repository", out var repoElement)
            || !repoElement.TryGetProperty("issueOrPullRequest", out var issueElement)
            || issueElement.ValueKind == JsonValueKind.Null)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new InvalidOperationException($"GitHub GraphQL error for {issueRef.Repo.Owner}/{issueRef.Repo.Repo}#{issueRef.Number}: {errorMessage}");
            }
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

    private static string? TryReadGraphqlErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errorsElement)
            || errorsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var messages = new List<string>();
        foreach (var error in errorsElement.EnumerateArray())
        {
            if (error.TryGetProperty("message", out var messageElement))
            {
                var message = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    messages.Add(message);
                }
            }
        }

        return messages.Count == 0 ? null : string.Join("; ", messages);
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

    private sealed record GraphqlRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("variables")] Dictionary<string, object?> Variables);
}
