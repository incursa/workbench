namespace Workbench;

public sealed record RepoDocDetail(
    RepoDocSummary Summary,
    string Body,
    IReadOnlyDictionary<string, object?> FrontMatter);
