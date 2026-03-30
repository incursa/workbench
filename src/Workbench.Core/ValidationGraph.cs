namespace Workbench.Core;

/// <summary>
/// In-memory canonical graph collected from repository artifacts.
/// </summary>
#pragma warning disable MA0048
internal sealed class ValidationGraph
{
    public List<SpecificationNode> Specifications { get; } = new();

    public List<RequirementNode> Requirements { get; } = new();

    public List<ArchitectureNode> Architectures { get; } = new();

    public List<WorkItemNode> WorkItems { get; } = new();

    public List<VerificationNode> Verifications { get; } = new();

    public List<CanonicalArtifactNode> Artifacts { get; } = new();

    public Dictionary<string, List<CanonicalArtifactNode>> ArtifactsById { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<RequirementNode>> RequirementsById { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void AddArtifact(CanonicalArtifactNode node)
    {
        Artifacts.Add(node);
        if (!ArtifactsById.TryGetValue(node.ArtifactId, out var list))
        {
            list = new List<CanonicalArtifactNode>();
            ArtifactsById[node.ArtifactId] = list;
        }

        list.Add(node);
    }

    public void AddRequirement(RequirementNode node)
    {
        Requirements.Add(node);
        if (!RequirementsById.TryGetValue(node.RequirementId, out var list))
        {
            list = new List<RequirementNode>();
            RequirementsById[node.RequirementId] = list;
        }

        list.Add(node);
    }

    public IReadOnlyList<CanonicalArtifactNode> ResolveArtifact(string artifactId)
    {
        if (ArtifactsById.TryGetValue(artifactId, out var matches))
        {
            return matches;
        }

        return Array.Empty<CanonicalArtifactNode>();
    }

    public IReadOnlyList<RequirementNode> ResolveRequirement(string requirementId)
    {
        if (RequirementsById.TryGetValue(requirementId, out var matches))
        {
            return matches;
        }

        return Array.Empty<RequirementNode>();
    }
}

internal sealed record CanonicalArtifactNode(
    string ArtifactId,
    string ArtifactType,
    string Path,
    string RepoRelativePath,
    string Domain,
    string Title,
    string Status);

internal sealed record SpecificationNode(
    CanonicalArtifactNode Artifact,
    IReadOnlyList<string> RelatedArtifacts,
    IReadOnlyList<RequirementNode> Requirements);

internal sealed record RequirementNode(
    string RequirementId,
    string SpecArtifactId,
    string SpecPath,
    string SpecRepoRelativePath,
    string Title,
    string Clause,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Trace,
    IReadOnlyList<string> RelatedArtifacts,
    IReadOnlyList<string> SourceRefs,
    IReadOnlyList<string> TestRefs,
    IReadOnlyList<string> CodeRefs);

internal sealed record ArchitectureNode(
    CanonicalArtifactNode Artifact,
    IReadOnlyList<string> Satisfies,
    IReadOnlyList<string> BodySatisfies,
    IReadOnlyList<string> RelatedArtifacts);

internal sealed record WorkItemNode(
    CanonicalArtifactNode Artifact,
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> DesignLinks,
    IReadOnlyList<string> VerificationLinks,
    IReadOnlyList<string> BodyAddresses,
    IReadOnlyList<string> BodyDesignLinks,
    IReadOnlyList<string> BodyVerificationLinks,
    IReadOnlyList<string> TraceAddresses,
    IReadOnlyList<string> TraceDesignLinks,
    IReadOnlyList<string> TraceVerificationLinks,
    IReadOnlyList<string> RelatedArtifacts);

internal sealed record VerificationNode(
    CanonicalArtifactNode Artifact,
    IReadOnlyList<string> Verifies,
    IReadOnlyList<string> BodyVerifies,
    IReadOnlyList<string> BodyRelatedArtifacts,
    IReadOnlyList<string> RelatedArtifacts,
    IReadOnlyList<string> EvidenceRefs,
    bool BenchmarkNotApplicable);
#pragma warning restore MA0048
