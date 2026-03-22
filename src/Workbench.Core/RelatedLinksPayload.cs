namespace Workbench.Core;

/// <summary>
/// JSON-serializable related links grouped by category.
/// </summary>
/// <param name="Specs">Spec document links.</param>
/// <param name="Files">Code or file links.</param>
/// <param name="Prs">Pull request URLs.</param>
/// <param name="Issues">Issue URLs or references.</param>
/// <param name="Branches">Git branch names.</param>
public sealed record RelatedLinksPayload(
    [property: JsonPropertyName("specs")] IList<string> Specs,
    [property: JsonPropertyName("files")] IList<string> Files,
    [property: JsonPropertyName("prs")] IList<string> Prs,
    [property: JsonPropertyName("issues")] IList<string> Issues,
    [property: JsonPropertyName("branches")] IList<string> Branches);
