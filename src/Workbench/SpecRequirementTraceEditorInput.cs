namespace Workbench;

public sealed class SpecRequirementTraceEditorInput
{
    public string SatisfiedByText { get; set; } = string.Empty;

    public string SelectedSatisfiedBy { get; set; } = string.Empty;

    public string ImplementedByText { get; set; } = string.Empty;

    public string SelectedImplementedBy { get; set; } = string.Empty;

    public string VerifiedByText { get; set; } = string.Empty;

    public string SelectedVerifiedBy { get; set; } = string.Empty;

    public string DerivedFromText { get; set; } = string.Empty;

    public string SelectedDerivedFrom { get; set; } = string.Empty;

    public string SupersedesText { get; set; } = string.Empty;

    public string SelectedSupersedes { get; set; } = string.Empty;

    public string UpstreamRefsText { get; set; } = string.Empty;

    public string RelatedText { get; set; } = string.Empty;

    public string SelectedRelated { get; set; } = string.Empty;

    public string ExtensionJson { get; set; } = string.Empty;
}
