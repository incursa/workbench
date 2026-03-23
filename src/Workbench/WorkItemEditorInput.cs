namespace Workbench;

public sealed class WorkItemEditorInput
{
    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    public string? Priority { get; set; }

    public string? Owner { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string AcceptanceCriteria { get; set; } = string.Empty;

    public string? AppendNote { get; set; }

    public bool RenameFile { get; set; } = true;
}
