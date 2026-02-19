namespace Workbench.Core;

/// <summary>
/// Payload describing repository migration results.
/// </summary>
/// <param name="MovedToDone">Work item files moved from active to done.</param>
/// <param name="MovedToItems">Work item files moved from done to active.</param>
/// <param name="ItemsNormalized">Number of normalized work items.</param>
/// <param name="DocsUpdated">Number of docs updated by doc sync.</param>
/// <param name="ItemLinksUpdated">Number of work items updated by doc sync.</param>
/// <param name="IndexFilesUpdated">Number of index files updated by nav sync.</param>
/// <param name="WorkboardUpdated">Number of workboards updated by nav sync.</param>
/// <param name="ReportPath">Path to generated migration report (null for dry-run).</param>
/// <param name="DryRun">True when files were not mutated.</param>
public sealed record MigrationData(
    [property: JsonPropertyName("movedToDone")] IList<string> MovedToDone,
    [property: JsonPropertyName("movedToItems")] IList<string> MovedToItems,
    [property: JsonPropertyName("itemsNormalized")] int ItemsNormalized,
    [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
    [property: JsonPropertyName("itemLinksUpdated")] int ItemLinksUpdated,
    [property: JsonPropertyName("indexFilesUpdated")] int IndexFilesUpdated,
    [property: JsonPropertyName("workboardUpdated")] int WorkboardUpdated,
    [property: JsonPropertyName("reportPath")] string? ReportPath,
    [property: JsonPropertyName("dryRun")] bool DryRun);
