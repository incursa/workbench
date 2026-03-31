using System.Text.Json.Serialization;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed class CanonicalArtifactModel
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

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("context")]
    public string? Context { get; init; }

    [JsonPropertyName("related_artifacts")]
    public IReadOnlyList<string>? RelatedArtifacts { get; init; }

    [JsonPropertyName("requirements")]
    public IReadOnlyList<CanonicalRequirementModel>? Requirements { get; init; }

    [JsonPropertyName("satisfies")]
    public IReadOnlyList<string>? Satisfies { get; init; }

    [JsonPropertyName("addresses")]
    public IReadOnlyList<string>? Addresses { get; init; }

    [JsonPropertyName("design_links")]
    public IReadOnlyList<string>? DesignLinks { get; init; }

    [JsonPropertyName("verification_links")]
    public IReadOnlyList<string>? VerificationLinks { get; init; }

    [JsonPropertyName("verifies")]
    public IReadOnlyList<string>? Verifies { get; init; }

    [JsonPropertyName("evidence")]
    public IReadOnlyList<string>? Evidence { get; init; }
}

public sealed class CanonicalRequirementModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("statement")]
    public string Statement { get; init; } = string.Empty;

    [JsonPropertyName("trace")]
    public CanonicalRequirementTraceModel? Trace { get; init; }
}

public sealed class CanonicalRequirementTraceModel
{
    [JsonPropertyName("satisfied_by")]
    public IReadOnlyList<string>? SatisfiedBy { get; init; }

    [JsonPropertyName("implemented_by")]
    public IReadOnlyList<string>? ImplementedBy { get; init; }

    [JsonPropertyName("verified_by")]
    public IReadOnlyList<string>? VerifiedBy { get; init; }

    [JsonPropertyName("derived_from")]
    public IReadOnlyList<string>? DerivedFrom { get; init; }

    [JsonPropertyName("supersedes")]
    public IReadOnlyList<string>? Supersedes { get; init; }

    [JsonPropertyName("upstream_refs")]
    public IReadOnlyList<string>? UpstreamRefs { get; init; }

    [JsonPropertyName("related")]
    public IReadOnlyList<string>? Related { get; init; }
}

#pragma warning restore MA0048
