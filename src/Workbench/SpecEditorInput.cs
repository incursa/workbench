namespace Workbench;

public sealed class SpecEditorInput
{
    public string Path { get; set; } = string.Empty;

    public string SourceFormat { get; set; } = "json";

    public string SchemaReference { get; set; } = string.Empty;

    public string ExtensionJson { get; set; } = string.Empty;

    public string? ArtifactId { get; set; }

    public string? Domain { get; set; }

    public string? Capability { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";

    public string? Owner { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Context { get; set; } = string.Empty;

    public string TagsText { get; set; } = string.Empty;

    public string RelatedArtifactsText { get; set; } = string.Empty;

    public string OpenQuestionsText { get; set; } = string.Empty;

    public IList<SpecSupplementalSectionEditorInput> SupplementalSections { get; set; } = [];

    public IList<SpecRequirementEditorInput> Requirements { get; set; } = [SpecRequirementEditorInput.CreateBlank()];
}
