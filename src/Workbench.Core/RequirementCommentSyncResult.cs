namespace Workbench.Core;

internal sealed record RequirementCommentSyncResult(
    int FilesUpdated,
    int RequirementsUpdated,
    IList<string> Warnings);
