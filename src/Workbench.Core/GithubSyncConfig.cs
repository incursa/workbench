namespace Workbench.Core;

/// <summary>
/// Repository policy defaults for GitHub synchronization.
/// </summary>
public sealed record GithubSyncConfig
{
    /// <summary>
    /// Sync mode policy (manual, generate, active).
    /// </summary>
    public string Mode { get; init; } = "generate";

    /// <summary>
    /// Default conflict resolution behavior when both sides diverge (fail, local, github).
    /// </summary>
    public string ConflictDefault { get; init; } = "fail";

    /// <summary>
    /// Indicates whether scheduled automation is enabled for sync workflows.
    /// </summary>
    public bool ScheduleEnabled { get; init; }
}
