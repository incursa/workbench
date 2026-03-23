namespace Workbench;

public sealed record RepoTreeBranch(
    string Name,
    string Path,
    int EntryCount,
    IReadOnlyList<RepoTreeBranch> Children,
    IReadOnlyList<RepoTreeEntry> Entries);
