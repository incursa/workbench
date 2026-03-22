namespace Workbench.Core;

/// <summary>
/// Parsed work item model loaded from front matter and body content.
/// </summary>
/// <param name="Id">Stable work item identifier.</param>
/// <param name="Type">Compatibility type label used by legacy views (bug, task, spike, or work_item).</param>
/// <param name="Status">Compatibility workflow status string.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="Priority">Optional compatibility priority label.</param>
/// <param name="Owner">Optional owner or assignee.</param>
/// <param name="Created">Compatibility created date string.</param>
/// <param name="Updated">Optional compatibility last-updated date string.</param>
/// <param name="Tags">Compatibility tag labels.</param>
/// <param name="Related">Compatibility related links.</param>
/// <param name="Slug">Slugified title used for filenames.</param>
/// <param name="Path">Absolute path to the work item file.</param>
/// <param name="Body">Markdown body content (without front matter).</param>
public sealed record WorkItem(
    string Id,
    string Type,
    string Status,
    string Title,
    string? Priority,
    string? Owner,
    string Created,
    string? Updated,
    IList<string> Tags,
    RelatedLinks Related,
    string Slug,
    string Path,
    string Body)
{
    /// <summary>Canonical work-item identifier.</summary>
    public string ArtifactId { get; init; } = Id;

    /// <summary>Canonical work-item artifact type.</summary>
    public string ArtifactType { get; init; } = "work_item";

    /// <summary>Canonical work-item workflow status.</summary>
    public string ArtifactStatus { get; init; } = string.Empty;

    /// <summary>Canonical work-item domain.</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>Requirement identifiers addressed by this work item.</summary>
    public IList<string> Addresses { get; init; } = new List<string>();

    /// <summary>Architecture artifact identifiers used as design inputs.</summary>
    public IList<string> DesignLinks { get; init; } = new List<string>();

    /// <summary>Verification artifact identifiers associated with this work item.</summary>
    public IList<string> VerificationLinks { get; init; } = new List<string>();

    /// <summary>Additional related artifact identifiers carried alongside the canonical trace model.</summary>
    public IList<string> RelatedArtifacts { get; init; } = new List<string>();

    /// <summary>Optional GitHub sync timestamp retained for compatibility.</summary>
    public string? GithubSynced { get; init; }

    /// <summary>Compatibility work-item type retained for legacy UI and sync behavior.</summary>
    public string CompatibilityType => Type;

    /// <summary>Compatibility workflow status retained for legacy UI and sync behavior.</summary>
    public string CompatibilityStatus => Status;
}
