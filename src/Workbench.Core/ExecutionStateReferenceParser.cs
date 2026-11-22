using System.Globalization;

namespace Workbench.Core;

public static class ExecutionStateReferenceParser
{
    public static ExecutionStateReference ParsePullRequest(string value, GithubRepoRef defaultRepo)
    {
        return ParseNumberedReference(value, defaultRepo, ExecutionKinds.PullRequest, "pull");
    }

    public static ExecutionStateReference ParseDeployment(string value, GithubRepoRef defaultRepo)
    {
        return ParseNumberedReference(value, defaultRepo, ExecutionKinds.Deployment, "deployments");
    }

    public static ExecutionStateReference ParseRelease(string value, GithubRepoRef defaultRepo)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Release reference is empty.");
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var releaseIndex = Array.FindIndex(segments, segment => string.Equals(segment, "releases", StringComparison.OrdinalIgnoreCase));
            if (releaseIndex >= 2 && releaseIndex + 2 < segments.Length &&
                string.Equals(segments[releaseIndex + 1], "tag", StringComparison.OrdinalIgnoreCase))
            {
                var repo = CreateRepositoryFromUri(uri, segments, releaseIndex);
                var tag = Uri.UnescapeDataString(string.Join('/', segments[(releaseIndex + 2)..]));
                return Create(ExecutionKinds.Release, repo, tag, trimmed);
            }

            throw new InvalidOperationException($"Unsupported release URL: {value}");
        }

        var atIndex = trimmed.LastIndexOf('@');
        if (atIndex > 0)
        {
            var repo = ParseRepository(trimmed[..atIndex], defaultRepo.Host);
            return Create(ExecutionKinds.Release, repo, trimmed[(atIndex + 1)..], null);
        }

        return Create(ExecutionKinds.Release, defaultRepo, trimmed, null);
    }

    private static ExecutionStateReference ParseNumberedReference(
        string value,
        GithubRepoRef defaultRepo,
        string kind,
        string urlSegment)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{kind} reference is empty.");
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var segmentIndex = Array.FindIndex(segments, segment => string.Equals(segment, urlSegment, StringComparison.OrdinalIgnoreCase));
            if (segmentIndex >= 2 && segmentIndex + 1 < segments.Length &&
                long.TryParse(segments[segmentIndex + 1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                var repo = CreateRepositoryFromUri(uri, segments, segmentIndex);
                return Create(kind, repo, segments[segmentIndex + 1], trimmed);
            }

            throw new InvalidOperationException($"Unsupported {kind} URL: {value}");
        }

        var hashIndex = trimmed.LastIndexOf('#');
        if (hashIndex > 0)
        {
            var repo = ParseRepository(trimmed[..hashIndex], defaultRepo.Host);
            return CreateNumbered(kind, repo, trimmed[(hashIndex + 1)..], null);
        }

        return CreateNumbered(kind, defaultRepo, trimmed.TrimStart('#'), null);
    }

    private static ExecutionStateReference CreateNumbered(
        string kind,
        GithubRepoRef repo,
        string identifier,
        string? url)
    {
        if (!long.TryParse(identifier, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException($"Invalid {kind} identifier: {identifier}");
        }

        return Create(kind, repo, identifier, url);
    }

    private static ExecutionStateReference Create(
        string kind,
        GithubRepoRef repo,
        string identifier,
        string? url)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException($"{kind} identifier is empty.");
        }

        var separator = string.Equals(kind, ExecutionKinds.Release, StringComparison.Ordinal) ? "@" : "#";
        return new ExecutionStateReference(
            kind,
            repo,
            identifier,
            $"{repo.Host}/{repo.Owner}/{repo.Repo}{separator}{identifier}",
            url);
    }

    private static GithubRepoRef ParseRepository(string value, string defaultHost)
    {
        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length switch
        {
            2 => new GithubRepoRef(defaultHost, segments[0], segments[1]),
            3 => new GithubRepoRef(segments[0], segments[1], segments[2]),
            _ => throw new InvalidOperationException($"Invalid repository reference: {value}")
        };
    }

    private static GithubRepoRef CreateRepositoryFromUri(Uri uri, string[] segments, int resourceIndex)
    {
        var host = string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            ? "github.com"
            : uri.Host;
        return new GithubRepoRef(host, segments[resourceIndex - 2], segments[resourceIndex - 1]);
    }
}
