namespace Workbench;

public sealed class SpecRequirementEditorInput
{
    public string Id { get; set; } = string.Empty;

    public string IdSuffix { get; set; } = string.Empty;

    public long Revision { get; set; } = 1;

    public string Title { get; set; } = string.Empty;

    public string Statement { get; set; } = string.Empty;

    public string NotesText { get; set; } = string.Empty;

    public string ExtensionJson { get; set; } = string.Empty;

    public SpecRequirementTraceEditorInput Trace { get; set; } = new();

    public static SpecRequirementEditorInput CreateBlank()
    {
        return new();
    }
}
