namespace Workbench.Core;

/// <summary>
/// Repo-relative paths used by Workbench when creating or locating files.
/// </summary>
public sealed record PathsConfig
{
    /// <summary>Root folder for documentation content.</summary>
    public string DocsRoot { get; init; } = "docs";
    /// <summary>Root folder for canonical SpecTrace content.</summary>
    public string SpecsRoot { get; init; } = "specs";
    /// <summary>Directory for canonical architecture artifacts.</summary>
    public string ArchitectureDir { get; init; } = "architecture";
    /// <summary>Directory for canonical work-item artifacts.</summary>
    public string WorkItemsSpecsDir { get; init; } = "work/items";
    /// <summary>Directory for generated SpecTrace outputs.</summary>
    public string GeneratedDir { get; init; } = "specs/generated";
    /// <summary>Directory for canonical SpecTrace templates.</summary>
    public string SpecsTemplatesDir { get; init; } = "specs/templates";
    /// <summary>Directory for canonical SpecTrace schemas.</summary>
    public string SpecsSchemasDir { get; init; } = "specs/schemas";
    /// <summary>Root folder for work tracking content.</summary>
    public string WorkRoot { get; init; } = "work";
    /// <summary>Directory for active work item files.</summary>
    public string ItemsDir { get; init; } = "work/items";
    /// <summary>Directory for completed/dropped work item files.</summary>
    public string DoneDir { get; init; } = "work/done";
    /// <summary>Directory containing templates for work items and docs.</summary>
    public string TemplatesDir { get; init; } = "work/templates";
    /// <summary>Path to the generated workboard index file.</summary>
    public string WorkboardFile { get; init; } = "work/README.md";
}
