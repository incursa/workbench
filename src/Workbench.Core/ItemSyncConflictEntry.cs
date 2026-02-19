namespace Workbench.Core;

/// <summary>
/// Describes an item/issue sync conflict that requires explicit resolution.
/// </summary>
/// <param name="ItemId">Work item identifier.</param>
/// <param name="IssueUrl">Canonical issue URL.</param>
/// <param name="Reason">Human-readable reason for the conflict.</param>
public sealed record ItemSyncConflictEntry(
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("issueUrl")] string IssueUrl,
    [property: JsonPropertyName("reason")] string Reason);
