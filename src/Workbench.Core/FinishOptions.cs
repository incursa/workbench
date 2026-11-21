namespace Workbench.Core;

/// <summary>
/// Options for completing a work item and reconciling local repository state.
/// </summary>
public sealed record FinishOptions(
    string WorkItemId,
    string? PullRequest = null,
    IList<string>? Evidence = null,
    string? Note = null,
    string? ValidationProfile = null,
    bool DryRun = false);
