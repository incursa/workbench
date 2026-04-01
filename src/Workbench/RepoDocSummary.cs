namespace Workbench;

/// <summary>
/// Lightweight document summary used by the browser UI.
/// </summary>
/// <param name="Path">Relative repository path.</param>
/// <param name="ArtifactId">Optional artifact identifier.</param>
/// <param name="Domain">Optional domain metadata.</param>
/// <param name="Capability">Optional capability metadata.</param>
/// <param name="Title">Display title.</param>
/// <param name="Type">Document type.</param>
/// <param name="Status">Document status.</param>
/// <param name="Section">Repository section for navigation grouping.</param>
/// <param name="Excerpt">Short body excerpt.</param>
/// <param name="WorkItems">Convenience list of linked work item IDs.</param>
/// <param name="RelatedArtifacts">Full related-artifact list so the shell can render generic doc arrays without re-reading the file.</param>
/// <param name="Tags">Optional topical tags for search and navigation.</param>
/// <param name="Satisfies">Requirement IDs satisfied by architecture artifacts.</param>
/// <param name="Verifies">Requirement IDs verified by verification artifacts.</param>
public sealed record RepoDocSummary(
    string Path,
    string? ArtifactId,
    string? Domain,
    string? Capability,
    string Title,
    string Type,
    string Status,
    string Section,
    string Excerpt,
    IReadOnlyList<string> WorkItems,
    IReadOnlyList<string> RelatedArtifacts,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Satisfies,
    IReadOnlyList<string> Verifies);
