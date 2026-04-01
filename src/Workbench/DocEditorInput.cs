namespace Workbench;

public sealed class DocEditorInput
{
    public string Path { get; set; } = string.Empty;

    public string Type { get; set; } = "doc";

    public string? ArtifactId { get; set; }

    public string? Domain { get; set; }

    public string? Capability { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    public string? Owner { get; set; }

    public string Body { get; set; } = string.Empty;

    public string RelatedArtifacts { get; set; } = string.Empty;

    public string Satisfies { get; set; } = string.Empty;

    public string Verifies { get; set; } = string.Empty;

    public string WorkItems { get; set; } = string.Empty;

    public string CodeRefs { get; set; } = string.Empty;
}
