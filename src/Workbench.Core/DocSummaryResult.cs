namespace Workbench
{
    public sealed record DocSummaryResult(
        int FilesUpdated,
        int NotesAdded,
        IList<string> UpdatedFiles,
        IList<string> SkippedFiles,
        IList<string> Errors,
        IList<string> Warnings);
}
