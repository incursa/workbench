namespace Workbench;

public sealed record ItemSyncBranchEntry(
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("branch")] string Branch);
