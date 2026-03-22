namespace Workbench;

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
    IReadOnlyList<string> WorkItems);
