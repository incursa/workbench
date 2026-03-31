using System.Text.Json.Serialization;

#pragma warning disable MA0048

namespace Workbench.Core;

internal sealed class CueArtifactModel
{
    [JsonPropertyName("artifact_id")]
    public string ArtifactId { get; init; } = string.Empty;

    [JsonPropertyName("artifact_type")]
    public string ArtifactType { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; init; } = string.Empty;

    [JsonPropertyName("capability")]
    public string? Capability { get; init; }

    [JsonPropertyName("related_artifacts")]
    public List<string>? RelatedArtifacts { get; init; }

    [JsonPropertyName("requirements")]
    public List<CueRequirementModel>? Requirements { get; init; }

    [JsonPropertyName("satisfies")]
    public List<string>? Satisfies { get; init; }

    [JsonPropertyName("addresses")]
    public List<string>? Addresses { get; init; }

    [JsonPropertyName("design_links")]
    public List<string>? DesignLinks { get; init; }

    [JsonPropertyName("verification_links")]
    public List<string>? VerificationLinks { get; init; }

    [JsonPropertyName("verifies")]
    public List<string>? Verifies { get; init; }

    [JsonPropertyName("evidence")]
    public List<string>? Evidence { get; init; }
}

internal sealed class CueRequirementModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("statement")]
    public string Statement { get; init; } = string.Empty;

    [JsonPropertyName("trace")]
    public CueRequirementTraceModel? Trace { get; init; }
}

internal sealed class CueRequirementTraceModel
{
    [JsonPropertyName("satisfied_by")]
    public List<string>? SatisfiedBy { get; init; }

    [JsonPropertyName("implemented_by")]
    public List<string>? ImplementedBy { get; init; }

    [JsonPropertyName("verified_by")]
    public List<string>? VerifiedBy { get; init; }

    [JsonPropertyName("derived_from")]
    public List<string>? DerivedFrom { get; init; }

    [JsonPropertyName("supersedes")]
    public List<string>? Supersedes { get; init; }

    [JsonPropertyName("upstream_refs")]
    public List<string>? UpstreamRefs { get; init; }

    [JsonPropertyName("related")]
    public List<string>? Related { get; init; }
}

#pragma warning restore MA0048
