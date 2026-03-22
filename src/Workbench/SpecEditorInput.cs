namespace Workbench;

public sealed class SpecEditorInput
{
    public string Path { get; set; } = string.Empty;

    public string? ArtifactId { get; set; }

    public string? Domain { get; set; }

    public string? Capability { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    public string? Owner { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;

    public string Requirements { get; set; } = string.Empty;

    public string RelatedArtifacts { get; set; } = string.Empty;

    public string RelatedArchitectureDocs { get; set; } = string.Empty;

    public string RelatedWorkItems { get; set; } = string.Empty;

    public string RelatedAdrs { get; set; } = string.Empty;

    public string OpenQuestions { get; set; } = string.Empty;

    public string CodeRefs { get; set; } = string.Empty;
}
