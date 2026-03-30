namespace Workbench.Core;

internal sealed record RequirementTraceSyncResult(
    int FilesUpdated,
    int RequirementsUpdated,
    IList<string> Warnings);
