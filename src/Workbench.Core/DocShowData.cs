namespace Workbench.Core;

/// <summary>
/// Payload describing a resolved document for CLI output.
/// </summary>
/// <param name="Path">Absolute path to the document.</param>
/// <param name="ArtifactId">Artifact identifier when present.</param>
/// <param name="Domain">Document domain metadata when present.</param>
/// <param name="Capability">Document capability metadata when present.</param>
/// <param name="Type">Document type.</param>
/// <param name="Title">Document title.</param>
/// <param name="Status">Document status.</param>
/// <param name="Owner">Document owner.</param>
/// <param name="WorkItems">Linked work item IDs.</param>
/// <param name="CodeRefs">Linked code references.</param>
/// <param name="Body">Markdown body content.</param>
public sealed record DocShowData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("artifactId")] string? ArtifactId,
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("capability")] string? Capability,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("workItems")] IList<string> WorkItems,
    [property: JsonPropertyName("codeRefs")] IList<string> CodeRefs,
    [property: JsonPropertyName("body")] string Body);
