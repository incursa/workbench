namespace Workbench.Core;

/// <summary>
/// Payload describing a created document.
/// </summary>
/// <param name="Path">Absolute path to the document.</param>
/// <param name="ArtifactId">Artifact identifier when the document is tracked as an explicit spec or architecture artifact.</param>
/// <param name="Domain">Document domain metadata when provided.</param>
/// <param name="Capability">Document capability metadata when provided.</param>
/// <param name="Type">Document type (spec, adr, runbook, guide, doc).</param>
/// <param name="WorkItems">Linked work item IDs.</param>
public sealed record DocCreateData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("artifactId")] string? ArtifactId,
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("capability")] string? Capability,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("workItems")] IList<string> WorkItems);
