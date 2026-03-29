namespace Workbench.Core;

/// <summary>
/// Structured validation finding emitted by repository validation.
/// </summary>
public sealed record ValidationFinding(
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("file")] string? File,
    [property: JsonPropertyName("artifactId")] string? ArtifactId,
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("targetId")] string? TargetId,
    [property: JsonPropertyName("targetType")] string? TargetType,
    [property: JsonPropertyName("targetFile")] string? TargetFile);
