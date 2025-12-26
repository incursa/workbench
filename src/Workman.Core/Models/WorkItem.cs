namespace Workman.Core.Models;

public class WorkItemFrontMatter
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public string? Assignee { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Estimate { get; set; }
    public string? Branch { get; set; }
    public string? Pr { get; set; }
    public List<string> Related { get; set; } = new();
    public List<string> BlockedBy { get; set; } = new();
    public List<string> Blocks { get; set; } = new();

    // Bug-specific
    public string? Severity { get; set; }
    public string? Environment { get; set; }

    // Spike-specific
    public string? Question { get; set; }
    public string? Timebox { get; set; }
    public string? Outcome { get; set; }
}

public class WorkItem
{
    public WorkItemFrontMatter FrontMatter { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
