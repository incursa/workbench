namespace Workbench;

public sealed record RepoDocSummary(
    string Path,
    string Title,
    string Type,
    string Status,
    string Section,
    string Excerpt,
    IReadOnlyList<string> WorkItems);
