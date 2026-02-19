namespace Workbench.Core;

/// <summary>
/// Payload describing work item sync results.
/// </summary>
/// <param name="Imported">Issues imported as work items.</param>
/// <param name="IssuesCreated">Issues created from local items.</param>
/// <param name="IssuesUpdated">Issues updated from local items.</param>
/// <param name="ItemsUpdated">Items updated from GitHub issues.</param>
/// <param name="BranchesCreated">Branches created during sync.</param>
/// <param name="Conflicts">Conflicts that require an explicit sync preference.</param>
/// <param name="Warnings">Warnings emitted during sync.</param>
/// <param name="DryRun">True when no remote or file changes were applied.</param>
public sealed record ItemSyncData(
    [property: JsonPropertyName("imported")] IList<ItemSyncImportEntry> Imported,
    [property: JsonPropertyName("issuesCreated")] IList<ItemSyncIssueEntry> IssuesCreated,
    [property: JsonPropertyName("issuesUpdated")] IList<ItemSyncIssueUpdateEntry> IssuesUpdated,
    [property: JsonPropertyName("itemsUpdated")] IList<ItemSyncItemUpdateEntry> ItemsUpdated,
    [property: JsonPropertyName("branchesCreated")] IList<ItemSyncBranchEntry> BranchesCreated,
    [property: JsonPropertyName("conflicts")] IList<ItemSyncConflictEntry> Conflicts,
    [property: JsonPropertyName("warnings")] IList<string> Warnings,
    [property: JsonPropertyName("dryRun")] bool DryRun);
