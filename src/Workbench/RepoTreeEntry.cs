namespace Workbench;

public sealed record RepoTreeEntry(
    string Path,
    string Title,
    string Badge,
    string Secondary,
    string? Excerpt,
    string Href,
    bool IsSelected);
