namespace Workbench.Core;

/// <summary>
/// Payload describing a structured doc edit.
/// </summary>
/// <param name="Path">Absolute path to the edited document.</param>
/// <param name="ArtifactId">Resolved artifact identifier after the edit.</param>
/// <param name="ArtifactIdUpdated">True when the artifact identifier changed.</param>
/// <param name="TitleUpdated">True when the title changed.</param>
/// <param name="StatusUpdated">True when the status changed.</param>
/// <param name="OwnerUpdated">True when the owner changed.</param>
/// <param name="DomainUpdated">True when the domain changed.</param>
/// <param name="CapabilityUpdated">True when the capability changed.</param>
/// <param name="BodyUpdated">True when the Markdown body changed.</param>
/// <param name="WorkItemsUpdated">True when linked work items changed.</param>
/// <param name="CodeRefsUpdated">True when code refs changed.</param>
public sealed record DocEditData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("artifactId")] string? ArtifactId,
    [property: JsonPropertyName("artifactIdUpdated")] bool ArtifactIdUpdated,
    [property: JsonPropertyName("titleUpdated")] bool TitleUpdated,
    [property: JsonPropertyName("statusUpdated")] bool StatusUpdated,
    [property: JsonPropertyName("ownerUpdated")] bool OwnerUpdated,
    [property: JsonPropertyName("domainUpdated")] bool DomainUpdated,
    [property: JsonPropertyName("capabilityUpdated")] bool CapabilityUpdated,
    [property: JsonPropertyName("bodyUpdated")] bool BodyUpdated,
    [property: JsonPropertyName("workItemsUpdated")] bool WorkItemsUpdated,
    [property: JsonPropertyName("codeRefsUpdated")] bool CodeRefsUpdated);
