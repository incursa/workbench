namespace Workbench.Core;

#pragma warning disable MA0048

/// <summary>Stable, read-only repository context for people and agents.</summary>
public sealed record ContextData(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("repository")] ContextRepository Repository,
    [property: JsonPropertyName("policy")] WorkflowPolicy Policy,
    [property: JsonPropertyName("selectedWorkItem")] ContextWorkItemSummary? SelectedWorkItem,
    [property: JsonPropertyName("activeWork")] IReadOnlyList<ContextWorkItemSummary> ActiveWork,
    [property: JsonPropertyName("applicableArtifacts")] IReadOnlyList<ContextArtifactReference> ApplicableArtifacts,
    [property: JsonPropertyName("constraints")] IReadOnlyList<string> Constraints,
    [property: JsonPropertyName("recommendedNextAction")] ContextRecommendedAction RecommendedNextAction);

/// <summary>Repository facts used to orient a caller.</summary>
public sealed record ContextRepository(
    [property: JsonPropertyName("root")] string Root,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("clean")] bool? Clean);

/// <summary>Concise work-item representation with a repo-relative path.</summary>
public sealed record ContextWorkItemSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("path")] string Path);

/// <summary>A selected work item's relevant requirement, design, or verification reference.</summary>
public sealed record ContextArtifactReference(
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("artifactId")] string? ArtifactId,
    [property: JsonPropertyName("artifactType")] string? ArtifactType,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("resolved")] bool Resolved);

/// <summary>The single best next command and why it was selected.</summary>
public sealed record ContextRecommendedAction(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("reason")] string Reason);

#pragma warning restore MA0048
