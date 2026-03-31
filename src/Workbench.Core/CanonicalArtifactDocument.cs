namespace Workbench.Core;

public sealed record CanonicalArtifactDocument(
    CanonicalArtifactModel Artifact,
    IReadOnlyDictionary<string, object?> Data,
    string SourceText);
