namespace Workbench;

public sealed record RepoFileDetail(
    RepoFileSummary Summary,
    string Body,
    bool IsMarkdown,
    bool IsBinary)
{
    public bool CanPreview => !IsBinary;
}
