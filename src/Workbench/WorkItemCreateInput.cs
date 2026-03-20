namespace Workbench;

public sealed class WorkItemCreateInput
{
    public string Type { get; set; } = "task";

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    public string? Priority { get; set; } = "medium";

    public string? Owner { get; set; }
}
