namespace Workbench;

public sealed class SpecSupplementalSectionEditorInput
{
    public string Heading { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string ExtensionJson { get; set; } = string.Empty;

    public static SpecSupplementalSectionEditorInput CreateBlank()
    {
        return new();
    }
}
