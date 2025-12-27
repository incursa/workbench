namespace Workbench;

public sealed record ItemSyncData(
    [property: JsonPropertyName("imported")] IList<ItemSyncImportEntry> Imported,
    [property: JsonPropertyName("issuesCreated")] IList<ItemSyncIssueEntry> IssuesCreated,
    [property: JsonPropertyName("branchesCreated")] IList<ItemSyncBranchEntry> BranchesCreated,
    [property: JsonPropertyName("dryRun")] bool DryRun);
