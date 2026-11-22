using System.Net.Http.Headers;
using System.Text.Json;

namespace Workbench.Core;

public sealed class GithubExecutionStateProvider : IExecutionStateProvider
{
    private const string TokenEnvPrimary = "WORKBENCH_GITHUB_TOKEN";
    private static readonly string[] fallbackTokenKeys = ["GITHUB_TOKEN", "GH_TOKEN"];
    private readonly HttpClient httpClient;
    private readonly string? token;
    private readonly TimeProvider timeProvider;

    public GithubExecutionStateProvider()
        : this(new HttpClient(), ResolveToken(), TimeProvider.System)
    {
    }

    public GithubExecutionStateProvider(HttpClient httpClient, string? token, TimeProvider? timeProvider = null)
    {
        this.httpClient = httpClient;
        this.token = token;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Name => "github";

    public Task<ExecutionProviderStatus> CheckAvailabilityAsync(
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(this.token)
            ? new ExecutionProviderStatus(false, "Missing GitHub token. Set WORKBENCH_GITHUB_TOKEN, GITHUB_TOKEN, or GH_TOKEN.")
            : new ExecutionProviderStatus(true, null));
    }

    public async Task<ExecutionObservation> ObserveAsync(
        string repoRoot,
        ExecutionStateReference reference,
        CancellationToken cancellationToken = default)
    {
        var availability = await this.CheckAvailabilityAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        if (!availability.Available)
        {
            throw new InvalidOperationException(availability.Diagnostic);
        }

        return reference.Kind switch
        {
            ExecutionKinds.PullRequest => await this.ObservePullRequestAsync(reference, cancellationToken).ConfigureAwait(false),
            ExecutionKinds.Release => await this.ObserveReleaseAsync(reference, cancellationToken).ConfigureAwait(false),
            ExecutionKinds.Deployment => await this.ObserveDeploymentAsync(reference, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported execution kind '{reference.Kind}'.")
        };
    }

    private async Task<ExecutionObservation> ObservePullRequestAsync(
        ExecutionStateReference reference,
        CancellationToken cancellationToken)
    {
        using var document = await this.GetJsonAsync(
            reference.Repository,
            $"repos/{Escape(reference.Repository.Owner)}/{Escape(reference.Repository.Repo)}/pulls/{Escape(reference.Identifier)}",
            cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var state = GetString(root, "state") ?? "unknown";
        var merged = root.TryGetProperty("merged_at", out var mergedAt) && mergedAt.ValueKind != JsonValueKind.Null;
        var nativeState = merged ? "merged" : state;
        var normalizedState = ExecutionStates.Unknown;
        if (merged)
        {
            normalizedState = ExecutionStates.Succeeded;
        }
        else if (string.Equals(state, "open", StringComparison.OrdinalIgnoreCase))
        {
            normalizedState = ExecutionStates.Active;
        }
        else if (string.Equals(state, "closed", StringComparison.OrdinalIgnoreCase))
        {
            normalizedState = ExecutionStates.Cancelled;
        }
        var revision = GetString(root, "merge_commit_sha");
        if (string.IsNullOrWhiteSpace(revision) && root.TryGetProperty("head", out var head))
        {
            revision = GetString(head, "sha");
        }

        return this.CreateObservation(reference, GetString(root, "html_url"), normalizedState, nativeState, revision);
    }

    private async Task<ExecutionObservation> ObserveReleaseAsync(
        ExecutionStateReference reference,
        CancellationToken cancellationToken)
    {
        using var document = await this.GetJsonAsync(
            reference.Repository,
            $"repos/{Escape(reference.Repository.Owner)}/{Escape(reference.Repository.Repo)}/releases/tags/{Escape(reference.Identifier)}",
            cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var draft = GetBoolean(root, "draft");
        var prerelease = GetBoolean(root, "prerelease");
        var published = root.TryGetProperty("published_at", out var publishedAt) && publishedAt.ValueKind != JsonValueKind.Null;
        var nativeState = "created";
        if (draft)
        {
            nativeState = "draft";
        }
        else if (prerelease)
        {
            nativeState = "prerelease";
        }
        else if (published)
        {
            nativeState = "published";
        }

        var normalizedState = ExecutionStates.Active;
        if (draft)
        {
            normalizedState = ExecutionStates.Pending;
        }
        else if (published)
        {
            normalizedState = ExecutionStates.Succeeded;
        }

        return this.CreateObservation(
            reference,
            GetString(root, "html_url"),
            normalizedState,
            nativeState,
            GetString(root, "target_commitish"));
    }

    private async Task<ExecutionObservation> ObserveDeploymentAsync(
        ExecutionStateReference reference,
        CancellationToken cancellationToken)
    {
        var repoPath = $"repos/{Escape(reference.Repository.Owner)}/{Escape(reference.Repository.Repo)}";
        using var deployment = await this.GetJsonAsync(
            reference.Repository,
            $"{repoPath}/deployments/{Escape(reference.Identifier)}",
            cancellationToken).ConfigureAwait(false);
        using var statuses = await this.GetJsonAsync(
            reference.Repository,
            $"{repoPath}/deployments/{Escape(reference.Identifier)}/statuses?per_page=1",
            cancellationToken).ConfigureAwait(false);
        var deploymentRoot = deployment.RootElement;
        var statusRoot = statuses.RootElement.ValueKind == JsonValueKind.Array && statuses.RootElement.GetArrayLength() > 0
            ? statuses.RootElement[0]
            : default;
        var nativeState = statusRoot.ValueKind == JsonValueKind.Object
            ? GetString(statusRoot, "state") ?? "unknown"
            : "unknown";
        var normalizedState = nativeState.ToLowerInvariant() switch
        {
            "queued" or "pending" => ExecutionStates.Pending,
            "in_progress" => ExecutionStates.Active,
            "success" => ExecutionStates.Succeeded,
            "failure" or "error" => ExecutionStates.Failed,
            "inactive" => ExecutionStates.Cancelled,
            _ => ExecutionStates.Unknown
        };
        var url = statusRoot.ValueKind == JsonValueKind.Object
            ? GetString(statusRoot, "target_url") ?? GetString(statusRoot, "environment_url")
            : null;

        return this.CreateObservation(
            reference,
            url,
            normalizedState,
            nativeState,
            GetString(deploymentRoot, "sha"));
    }

    private async Task<JsonDocument> GetJsonAsync(
        GithubRepoRef repo,
        string path,
        CancellationToken cancellationToken)
    {
        var baseUri = string.Equals(repo.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            ? new UriBuilder(Uri.UriSchemeHttps, "api.github.com").Uri
            : new Uri($"https://{repo.Host}/api/v3/");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Workbench", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.token);
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub observation failed with HTTP {(int)response.StatusCode}: " +
                (string.IsNullOrWhiteSpace(responseBody) ? response.ReasonPhrase : responseBody));
        }

        return JsonDocument.Parse(responseBody);
    }

    private ExecutionObservation CreateObservation(
        ExecutionStateReference reference,
        string? observedUrl,
        string normalizedState,
        string nativeState,
        string? revision)
    {
        return new ExecutionObservation(
            this.Name,
            reference.Kind,
            reference.ExternalReference,
            observedUrl ?? reference.Url,
            normalizedState,
            nativeState,
            this.timeProvider.GetUtcNow(),
            revision);
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBoolean(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static string? ResolveToken()
    {
        var resolvedToken = Environment.GetEnvironmentVariable(TokenEnvPrimary);
        if (!string.IsNullOrWhiteSpace(resolvedToken))
        {
            return resolvedToken;
        }

        return fallbackTokenKeys
            .Select(Environment.GetEnvironmentVariable)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
