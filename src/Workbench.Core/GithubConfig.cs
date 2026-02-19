namespace Workbench.Core;

/// <summary>
/// GitHub integration settings and defaults.
/// </summary>
public sealed record GithubConfig
{
    /// <summary>Provider key (e.g., "octokit" or "gh").</summary>
    public string Provider { get; init; } = "octokit";
    /// <summary>Default draft setting for new pull requests.</summary>
    public bool DefaultDraft { get; init; } = false;
    /// <summary>GitHub host for API and URL resolution.</summary>
    public string Host { get; init; } = "github.com";
    /// <summary>Default repository owner when not provided by inputs.</summary>
    public string? Owner { get; init; }
    /// <summary>Default repository name when not provided by inputs.</summary>
    public string? Repository { get; init; }
    /// <summary>Sync behavior defaults for GitHub mirroring.</summary>
    public GithubSyncConfig Sync { get; init; } = new();
}
