namespace Workbench.Core;

/// <summary>
/// Serializable view of a work item for CLI JSON output.
/// </summary>
/// <param name="Id">Stable work item identifier.</param>
/// <param name="Type">Work item type.</param>
/// <param name="Status">Workflow status.</param>
/// <param name="Title">Human-readable title.</param>
/// <param name="ArtifactId">Canonical artifact identifier.</param>
/// <param name="ArtifactType">Canonical artifact type.</param>
/// <param name="ArtifactStatus">Canonical workflow status.</param>
/// <param name="Domain">Canonical domain.</param>
/// <param name="Priority">Optional priority label.</param>
/// <param name="Owner">Optional owner or assignee.</param>
/// <param name="Created">Created date string.</param>
/// <param name="Updated">Updated date string when available.</param>
/// <param name="Tags">Tag labels.</param>
/// <param name="Related">Related links grouped by type.</param>
/// <param name="Addresses">Requirement identifiers addressed by the work item.</param>
/// <param name="DesignLinks">Architecture identifiers used by the work item.</param>
/// <param name="VerificationLinks">Verification identifiers associated with the work item.</param>
/// <param name="RelatedArtifacts">Additional artifact identifiers carried alongside the canonical model.</param>
/// <param name="GithubSynced">Compatibility GitHub sync timestamp.</param>
/// <param name="Slug">Slugified title.</param>
/// <param name="Path">Absolute path to the work item file.</param>
/// <param name="Body">Optional markdown body content.</param>
public sealed record WorkItemPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artifactId")] string ArtifactId,
    [property: JsonPropertyName("artifactType")] string ArtifactType,
    [property: JsonPropertyName("artifactStatus")] string ArtifactStatus,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("priority")] string? Priority,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("created")] string Created,
    [property: JsonPropertyName("updated")] string? Updated,
    [property: JsonPropertyName("tags")] IList<string> Tags,
    [property: JsonPropertyName("related")] RelatedLinksPayload Related,
    [property: JsonPropertyName("addresses")] IList<string> Addresses,
    [property: JsonPropertyName("designLinks")] IList<string> DesignLinks,
    [property: JsonPropertyName("verificationLinks")] IList<string> VerificationLinks,
    [property: JsonPropertyName("relatedArtifacts")] IList<string> RelatedArtifacts,
    [property: JsonPropertyName("githubSynced")] string? GithubSynced,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("body")] string? Body);
