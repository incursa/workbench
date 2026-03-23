namespace Workbench;

public sealed record RepoFileSummary(
    string Path,
    string Name,
    string Extension,
    string FileType,
    long SizeBytes,
    DateTime LastModifiedUtc,
    string Excerpt);
