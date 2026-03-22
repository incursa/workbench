namespace Workbench;

public sealed class WorkItemCreateInput
{
    public string Type { get; set; } = "work_item";

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "planned";

    public string? Priority { get; set; } = "medium";

    public string? Owner { get; set; }
}
