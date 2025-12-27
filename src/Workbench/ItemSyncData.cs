namespace Workbench;

public sealed record ItemSyncData(
    [property: JsonPropertyName("imported")] IList<ItemSyncImportEntry> Imported,
    [property: JsonPropertyName("issuesCreated")] IList<ItemSyncIssueEntry> IssuesCreated,
    [property: JsonPropertyName("issuesUpdated")] IList<ItemSyncIssueUpdateEntry> IssuesUpdated,
    [property: JsonPropertyName("itemsUpdated")] IList<ItemSyncItemUpdateEntry> ItemsUpdated,
    [property: JsonPropertyName("branchesCreated")] IList<ItemSyncBranchEntry> BranchesCreated,
    [property: JsonPropertyName("dryRun")] bool DryRun);
