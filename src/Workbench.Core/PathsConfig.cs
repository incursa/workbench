namespace Workbench.Core;

/// <summary>
/// Repo-relative paths used by Workbench when creating or locating files.
/// </summary>
public sealed record PathsConfig
{
    /// <summary>Root folder for overview and high-level documentation content.</summary>
    public string DocsRoot { get; init; } = "overview";
    /// <summary>Root folder for canonical SpecTrace content.</summary>
    public string SpecsRoot { get; init; } = "specs";
    /// <summary>Directory for canonical architecture artifacts.</summary>
    public string ArchitectureDir { get; init; } = "specs/architecture";
    /// <summary>Directory for canonical work-item artifacts.</summary>
    public string WorkItemsSpecsDir { get; init; } = "specs/work-items";
    /// <summary>Directory for generated SpecTrace outputs.</summary>
    public string GeneratedDir { get; init; } = "specs/generated";
    /// <summary>Directory for canonical SpecTrace templates.</summary>
    public string SpecsTemplatesDir { get; init; } = "specs/templates";
    /// <summary>Directory for canonical SpecTrace schemas.</summary>
    public string SpecsSchemasDir { get; init; } = "specs/schemas";
    /// <summary>Root folder for work tracking content.</summary>
    public string WorkRoot { get; init; } = "specs/work-items";
    /// <summary>Directory for active work item files.</summary>
    public string ItemsDir { get; init; } = "specs/work-items/WB";
}
