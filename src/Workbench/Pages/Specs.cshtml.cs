using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Workbench.Core;

namespace Workbench.Pages;

public class SpecsModel : RepoPageModel
{
    private const string DefaultSpecTraceSchemaReference = "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json";
    private const string UnresolvedRequirementPrefix = "REQ-<DOMAIN>[-<GROUPING>...]-";

    public static IReadOnlyList<string> LifecycleStatusOptions { get; } =
    [
        "draft",
        "proposed",
        "approved",
        "implemented",
        "verified",
        "superseded",
        "retired"
    ];

    public SpecsModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool CreateMode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedReference { get; set; }

    [BindProperty]
    public SpecEditorInput Edit { get; set; } = new();

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerMessage { get; set; }

    public IReadOnlyList<RepoDocSummary> Specs { get; private set; } = [];

    public RepoTreeBranch Tree { get; private set; } = new("Specifications", string.Empty, 0, Array.Empty<RepoTreeBranch>(), Array.Empty<RepoTreeEntry>());

    public RepoDocDetail? SelectedSpec { get; private set; }

    public bool IsCreateMode { get; private set; }

    public string SpecIdPolicySummary { get; private set; } = string.Empty;

    public string PathPreview { get; private set; } = string.Empty;

    public bool IsLegacyMarkdownSpec => !IsCreateMode && SelectedSpec is not null && SelectedSpec.Summary.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> BannerLines =>
        string.IsNullOrWhiteSpace(BannerMessage)
            ? Array.Empty<string>()
            : BannerMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<SelectListItem> ArchitectureOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> WorkItemOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> VerificationOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> RequirementOptions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> RelatedArtifactOptions { get; private set; } = [];

    public string RequirementIdPrefix => BuildRequirementIdPrefix();

    public void OnGet()
    {
        ApplyChrome("Specs");
        LoadPage(populateEditor: true);
    }

    public IActionResult OnPostSave()
    {
        try
        {
            ApplyChrome("Specs");
            PrepareEditorForSave();

            var created = ShouldCreateSpec();
            var saved = created ? Workspace.CreateSpec(Edit) : Workspace.GetDoc(Workspace.SaveSpec(Edit).Path);
            if (saved is null)
            {
                throw new InvalidOperationException("Failed to reload spec.");
            }

            SetBanner(created ? "Spec created" : "Spec saved", $"{saved.Summary.Path} updated locally.");
            return RedirectToPage(new { selectedReference = saved.Summary.ArtifactId ?? saved.Summary.Path, query = Query, createMode = false });
        }
        catch (Exception ex)
        {
            ApplyChrome("Specs");
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostDelete()
    {
        try
        {
            ApplyChrome("Specs");

            if (IsCreateMode || SelectedSpec is null)
            {
                throw new InvalidOperationException("A specification must be selected before delete.");
            }

            var deleted = Workspace.DeleteDoc(SelectedSpec.Summary.Path, keepLinks: false);
            SetBanner("Spec deleted", $"{SelectedSpec.Summary.Path} removed locally.\nLinked work items updated: {deleted.ItemsUpdated}");
            return RedirectToPage(new { selectedReference = string.Empty, query = Query, createMode = false });
        }
        catch (Exception ex)
        {
            ApplyChrome("Specs");
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostAddRequirement()
    {
        ApplyChrome("Specs");
        Edit.Requirements ??= [];
        Edit.Requirements.Add(SpecRequirementEditorInput.CreateBlank());
        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostRemoveRequirement(int index)
    {
        ApplyChrome("Specs");
        Edit.Requirements ??= [];
        if (index >= 0 && index < Edit.Requirements.Count)
        {
            Edit.Requirements.RemoveAt(index);
        }

        if (Edit.Requirements.Count == 0)
        {
            Edit.Requirements.Add(SpecRequirementEditorInput.CreateBlank());
        }

        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostAddSupplementalSection()
    {
        ApplyChrome("Specs");
        Edit.SupplementalSections ??= [];
        Edit.SupplementalSections.Add(SpecSupplementalSectionEditorInput.CreateBlank());
        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostRemoveSupplementalSection(int index)
    {
        ApplyChrome("Specs");
        Edit.SupplementalSections ??= [];
        if (index >= 0 && index < Edit.SupplementalSections.Count)
        {
            Edit.SupplementalSections.RemoveAt(index);
        }

        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostAddTraceValue(int index, string field)
    {
        ApplyChrome("Specs");
        AddTraceValue(index, field);
        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostRemoveTraceValue(int index, string field, string value)
    {
        ApplyChrome("Specs");
        RemoveTraceValue(index, field, value);
        LoadPage(populateEditor: false);
        return Page();
    }

    public string GetRequirementDisplayId(SpecRequirementEditorInput requirement)
    {
        var suffix = ExtractRequirementIdSuffix(requirement);
        var prefix = BuildRequirementIdPrefix();

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            return prefix + suffix;
        }

        if (!string.IsNullOrWhiteSpace(requirement.Id))
        {
            return requirement.Id.Trim();
        }

        return prefix + "...";
    }

    public static string GetRequirementSummaryTitle(SpecRequirementEditorInput requirement)
    {
        return CollapseWhitespace(requirement.Title);
    }

    public static string GetRequirementSummaryPreview(SpecRequirementEditorInput requirement)
    {
        var statement = CollapseWhitespace(requirement.Statement);
        if (!string.IsNullOrWhiteSpace(statement))
        {
            return statement;
        }

        var notes = CollapseWhitespace(requirement.NotesText);
        return string.IsNullOrWhiteSpace(notes) ? "Expand to edit statement and trace links." : notes;
    }

    public static IReadOnlyList<string> GetTraceValues(string? valueText)
    {
        return SplitLines(valueText);
    }

    public static string ResolveOptionLabel(string value, IReadOnlyList<SelectListItem> options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match?.Text) ? value : match!.Text;
    }

    private void LoadPage(bool populateEditor)
    {
        if (!string.IsNullOrWhiteSpace(SelectedReference))
        {
            SelectedReference = SelectedReference.Trim();
        }

        Specs = Workspace.ListDocs("specification", Query);
        IsCreateMode = ResolveCreateMode();
        SelectedSpec = IsCreateMode ? null : ResolveSelectedSpec();

        if (populateEditor)
        {
            Edit = IsCreateMode || SelectedSpec is null
                ? CreateBlankSpecEditorInput()
                : Workspace.CreateSpecEditorInput(SelectedSpec);
        }

        if (SelectedSpec is not null)
        {
            SelectedReference = SelectedSpec.Summary.ArtifactId ?? SelectedSpec.Summary.Path;
        }
        else if (Specs.Count > 0 && string.IsNullOrWhiteSpace(SelectedReference) && !IsCreateMode)
        {
            SelectedReference = Specs[0].ArtifactId ?? Specs[0].Path;
        }

        SpecIdPolicySummary = Workspace.GetSpecIdPolicySummary();
        SyncTransientEditorState();
        PopulatePickerOptions();
        PathPreview = BuildPathPreview();
        Tree = BuildSpecTree(
            Specs,
            doc => Url.Page("/Specs", new { selectedReference = doc.ArtifactId ?? doc.Path, query = Query }) ?? doc.Path,
            IsCreateMode ? null : SelectedReference);
    }

    private string BuildPathPreview()
    {
        if (!string.IsNullOrWhiteSpace(Edit.Path))
        {
            return Edit.Path.Replace('\\', '/');
        }

        if (!string.Equals(Edit.SourceFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            return "specs/requirements/<domain>/<artifact-id>.md";
        }

        var domain = string.IsNullOrWhiteSpace(Edit.Domain)
            ? "<domain>"
            : Edit.Domain.Trim().ToLowerInvariant().Replace(' ', '-');
        var artifactId = GetResolvedSpecArtifactId();
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            artifactId = "SPEC-<DOMAIN>-<GROUPING>";
        }

        return $"specs/requirements/{domain}/{artifactId}.json";
    }

    private static RepoTreeBranch BuildSpecTree(IReadOnlyList<RepoDocSummary> specs, Func<RepoDocSummary, string> hrefFactory, string? selectedReference)
    {
        var branches = specs
            .GroupBy(GetSpecGroupName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key.Equals("Ungrouped", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RepoTreeBranch(
                group.Key,
                group.Key,
                group.Count(),
                Array.Empty<RepoTreeBranch>(),
                group.OrderBy(spec => spec.ArtifactId ?? spec.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(spec => spec.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(spec => new RepoTreeEntry(
                        spec.Path,
                        spec.Title,
                        string.IsNullOrWhiteSpace(spec.ArtifactId) ? spec.Domain ?? spec.Type : spec.ArtifactId,
                        string.Empty,
                        null,
                        hrefFactory(spec),
                        IsSelected: !string.IsNullOrWhiteSpace(selectedReference) &&
                            (spec.Path.Equals(selectedReference, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrWhiteSpace(spec.ArtifactId) && spec.ArtifactId.Equals(selectedReference, StringComparison.OrdinalIgnoreCase)))))
                    .ToList()))
            .ToList();

        return new RepoTreeBranch("Specifications", string.Empty, specs.Count, branches, Array.Empty<RepoTreeEntry>());
    }

    private static string GetSpecGroupName(RepoDocSummary spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Domain))
        {
            return spec.Domain.Trim().ToUpperInvariant();
        }

        var folder = Path.GetDirectoryName(spec.Path);
        return string.IsNullOrWhiteSpace(folder) ? "Ungrouped" : Path.GetFileName(folder)!.ToUpperInvariant();
    }

    private void PopulatePickerOptions()
    {
        var specOptions = BuildDocOptions("specification");
        ArchitectureOptions = BuildDocOptions("architecture");
        WorkItemOptions = Workspace.ListItems(includeDone: true, statusFilter: null, query: null)
            .Select(item => new SelectListItem(BuildPickerOptionLabel(item.Id, item.Title), item.Id))
            .ToList();
        VerificationOptions = BuildDocOptions("verification");
        RequirementOptions = BuildRequirementOptions();
        RelatedArtifactOptions = BuildDistinctOptions(specOptions.Concat(ArchitectureOptions).Concat(WorkItemOptions).Concat(VerificationOptions));
    }

    private List<SelectListItem> BuildDocOptions(string type)
    {
        return Workspace.ListDocs(type, query: null)
            .Select(doc => new SelectListItem(BuildDocOptionLabel(doc), doc.ArtifactId ?? doc.Path))
            .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<SelectListItem> BuildRequirementOptions()
    {
        var options = new List<SelectListItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddRequirementOptions(SpecEditorInput editor, bool useCurrentDisplayIds)
        {
            foreach (var requirement in editor.Requirements ?? [])
            {
                var requirementId = useCurrentDisplayIds
                    ? GetRequirementDisplayId(requirement)
                    : DocService.NormalizeArtifactId(requirement.Id) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(requirementId) || requirementId.Contains('<', StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seen.Add(requirementId))
                {
                    continue;
                }

                options.Add(new SelectListItem(BuildRequirementOptionLabel(requirementId, requirement.Title), requirementId));
            }
        }

        AddRequirementOptions(Edit, useCurrentDisplayIds: true);

        foreach (var spec in Specs)
        {
            if (SelectedSpec is not null &&
                string.Equals(spec.Path, SelectedSpec.Summary.Path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var editor = Workspace.CreateSpecEditorInput(spec.ArtifactId ?? spec.Path);
            if (editor is null)
            {
                continue;
            }

            AddRequirementOptions(editor, useCurrentDisplayIds: false);
        }

        return options
            .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SelectListItem> BuildDistinctOptions(IEnumerable<SelectListItem> options)
    {
        return options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .GroupBy(option => option.Value!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDocOptionLabel(RepoDocSummary doc)
    {
        return BuildPickerOptionLabel(doc.ArtifactId ?? doc.Path, doc.Title);
    }

    private static string BuildRequirementOptionLabel(string id, string? title)
    {
        return BuildPickerOptionLabel(id, title);
    }

    private static string BuildPickerOptionLabel(string key, string? title)
    {
        var normalizedKey = key.Trim();
        var normalizedTitle = CollapseWhitespace(title);
        return string.IsNullOrWhiteSpace(normalizedTitle)
            ? normalizedKey
            : $"{normalizedKey} - {TruncatePickerTitle(normalizedTitle, 48)}";
    }

    private static string TruncatePickerTitle(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }

    private string BuildRequirementIdPrefix()
    {
        var requirementStem = GetResolvedRequirementIdStem();
        return string.IsNullOrWhiteSpace(requirementStem)
            ? UnresolvedRequirementPrefix
            : $"REQ-{requirementStem}-";
    }

    private string GetResolvedSpecArtifactId()
    {
        var artifactId = DocService.NormalizeArtifactId(Edit.ArtifactId);
        if (!string.IsNullOrWhiteSpace(artifactId))
        {
            return artifactId;
        }

        return DocService.TryGenerateArtifactId(RepoRoot, Workspace.Config, "specification", Edit.Title ?? string.Empty, Edit.Domain, Edit.Capability) ?? string.Empty;
    }

    private string GetResolvedRequirementIdStem()
    {
        return GetRequirementIdStem(GetResolvedSpecArtifactId());
    }

    private static string GetRequirementIdStem(string? artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return string.Empty;
        }

        var normalized = artifactId.Trim();
        return normalized.StartsWith("SPEC-", StringComparison.OrdinalIgnoreCase)
            ? normalized["SPEC-".Length..]
            : normalized;
    }

    private string ExtractRequirementIdSuffix(SpecRequirementEditorInput requirement)
    {
        var normalizedSuffix = NormalizeRequirementIdSuffix(requirement.IdSuffix);
        if (!string.IsNullOrWhiteSpace(normalizedSuffix))
        {
            return normalizedSuffix;
        }

        var fullId = DocService.NormalizeArtifactId(requirement.Id) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullId))
        {
            return string.Empty;
        }

        var requirementPrefix = BuildRequirementIdPrefix();
        if (!requirementPrefix.Contains('<', StringComparison.Ordinal) &&
            fullId.StartsWith(requirementPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeRequirementIdSuffix(fullId[requirementPrefix.Length..]);
        }

        var legacyPrefix = BuildLegacyRequirementIdPrefix();
        if (!string.IsNullOrWhiteSpace(legacyPrefix) &&
            fullId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeRequirementIdSuffix(fullId[legacyPrefix.Length..]);
        }

        if (fullId.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeRequirementIdSuffix(fullId["REQ-".Length..]);
        }

        return NormalizeRequirementIdSuffix(fullId);
    }

    private string BuildLegacyRequirementIdPrefix()
    {
        var artifactId = GetResolvedSpecArtifactId();
        return string.IsNullOrWhiteSpace(artifactId) ? string.Empty : $"REQ-{artifactId}-";
    }

    private static string NormalizeRequirementIdSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new List<char>(value.Length);
        var previousSeparator = false;

        foreach (var ch in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Add(ch);
                previousSeparator = false;
                continue;
            }

            if (builder.Count > 0 && !previousSeparator)
            {
                builder.Add('-');
                previousSeparator = true;
            }
        }

        while (builder.Count > 0 && builder[^1] == '-')
        {
            builder.RemoveAt(builder.Count - 1);
        }

        return new string(builder.ToArray());
    }

    private static bool IsValidRequirementIdSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value[0] == '-' ||
            value[^1] == '-' ||
            !char.IsDigit(value[^1]))
        {
            return false;
        }

        var previousSeparator = false;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                previousSeparator = false;
                continue;
            }

            if (ch != '-' || previousSeparator)
            {
                return false;
            }

            previousSeparator = true;
        }

        return true;
    }

    private void PrepareEditorForSave()
    {
        Edit.Requirements ??= [];
        var requirementPrefix = BuildRequirementIdPrefix();
        var hasMeaningfulRequirements = Edit.Requirements.Any(HasMeaningfulRequirementContent);
        if (hasMeaningfulRequirements && requirementPrefix.Contains('<', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Domain and capability must resolve before requirement IDs can be generated.");
        }

        foreach (var requirement in Edit.Requirements)
        {
            requirement.Trace ??= new SpecRequirementTraceEditorInput();
            requirement.IdSuffix = ExtractRequirementIdSuffix(requirement);

            if (!HasMeaningfulRequirementContent(requirement))
            {
                requirement.Id = string.Empty;
                requirement.IdSuffix = string.Empty;
                continue;
            }

            if (string.IsNullOrWhiteSpace(requirement.IdSuffix) || !IsValidRequirementIdSuffix(requirement.IdSuffix))
            {
                throw new InvalidOperationException("Requirement ID suffixes must be alphanumeric, may include single hyphens, and must end with a digit.");
            }

            requirement.Id = requirementPrefix + requirement.IdSuffix;
        }
    }

    private static bool HasMeaningfulRequirementContent(SpecRequirementEditorInput requirement)
    {
        return !string.IsNullOrWhiteSpace(requirement.Id) ||
            !string.IsNullOrWhiteSpace(requirement.IdSuffix) ||
            !string.IsNullOrWhiteSpace(requirement.Title) ||
            !string.IsNullOrWhiteSpace(requirement.Statement) ||
            !string.IsNullOrWhiteSpace(requirement.NotesText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.SatisfiedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.ImplementedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.VerifiedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.DerivedFromText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.SupersedesText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.UpstreamRefsText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace?.RelatedText);
    }

    private void AddTraceValue(int index, string field)
    {
        var trace = GetTraceEditor(index);
        if (trace is null)
        {
            return;
        }

        var selectedValue = GetSelectedTraceValue(trace, field);
        if (string.IsNullOrWhiteSpace(selectedValue))
        {
            return;
        }

        var values = GetTraceValues(GetTraceValueText(trace, field)).ToList();
        if (!values.Contains(selectedValue, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(selectedValue);
        }

        SetTraceValueText(trace, field, string.Join(Environment.NewLine, values));
        SetSelectedTraceValue(trace, field, string.Empty);
    }

    private void RemoveTraceValue(int index, string field, string value)
    {
        var trace = GetTraceEditor(index);
        if (trace is null)
        {
            return;
        }

        var values = GetTraceValues(GetTraceValueText(trace, field))
            .Where(entry => !string.Equals(entry, value, StringComparison.OrdinalIgnoreCase))
            .ToList();
        SetTraceValueText(trace, field, string.Join(Environment.NewLine, values));
        SetSelectedTraceValue(trace, field, string.Empty);
    }

    private SpecRequirementTraceEditorInput? GetTraceEditor(int index)
    {
        Edit.Requirements ??= [];
        if (index < 0 || index >= Edit.Requirements.Count)
        {
            return null;
        }

        Edit.Requirements[index].Trace ??= new SpecRequirementTraceEditorInput();
        return Edit.Requirements[index].Trace;
    }

    private static string GetSelectedTraceValue(SpecRequirementTraceEditorInput trace, string field)
    {
        return field switch
        {
            "SatisfiedBy" => trace.SelectedSatisfiedBy,
            "ImplementedBy" => trace.SelectedImplementedBy,
            "VerifiedBy" => trace.SelectedVerifiedBy,
            "DerivedFrom" => trace.SelectedDerivedFrom,
            "Supersedes" => trace.SelectedSupersedes,
            "Related" => trace.SelectedRelated,
            _ => string.Empty
        };
    }

    private static void SetSelectedTraceValue(SpecRequirementTraceEditorInput trace, string field, string value)
    {
        switch (field)
        {
            case "SatisfiedBy":
                trace.SelectedSatisfiedBy = value;
                break;
            case "ImplementedBy":
                trace.SelectedImplementedBy = value;
                break;
            case "VerifiedBy":
                trace.SelectedVerifiedBy = value;
                break;
            case "DerivedFrom":
                trace.SelectedDerivedFrom = value;
                break;
            case "Supersedes":
                trace.SelectedSupersedes = value;
                break;
            case "Related":
                trace.SelectedRelated = value;
                break;
        }
    }

    private static string GetTraceValueText(SpecRequirementTraceEditorInput trace, string field)
    {
        return field switch
        {
            "SatisfiedBy" => trace.SatisfiedByText,
            "ImplementedBy" => trace.ImplementedByText,
            "VerifiedBy" => trace.VerifiedByText,
            "DerivedFrom" => trace.DerivedFromText,
            "Supersedes" => trace.SupersedesText,
            "Related" => trace.RelatedText,
            _ => string.Empty
        };
    }

    private static void SetTraceValueText(SpecRequirementTraceEditorInput trace, string field, string value)
    {
        switch (field)
        {
            case "SatisfiedBy":
                trace.SatisfiedByText = value;
                break;
            case "ImplementedBy":
                trace.ImplementedByText = value;
                break;
            case "VerifiedBy":
                trace.VerifiedByText = value;
                break;
            case "DerivedFrom":
                trace.DerivedFromText = value;
                break;
            case "Supersedes":
                trace.SupersedesText = value;
                break;
            case "Related":
                trace.RelatedText = value;
                break;
        }
    }

    private void SyncTransientEditorState()
    {
        Edit.Requirements ??= [];
        Edit.SupplementalSections ??= [];

        if (Edit.Requirements.Count == 0)
        {
            Edit.Requirements.Add(SpecRequirementEditorInput.CreateBlank());
        }

        Edit.Requirements = Edit.Requirements
            .Select(requirement =>
            {
                requirement.Trace ??= new SpecRequirementTraceEditorInput();
                requirement.IdSuffix = ExtractRequirementIdSuffix(requirement);
                return requirement;
            })
            .OrderBy(requirement => HasMeaningfulRequirementContent(requirement) ? 0 : 1)
            .ThenBy(requirement => GetRequirementDisplayId(requirement), StringComparer.OrdinalIgnoreCase)
            .ThenBy(requirement => CollapseWhitespace(requirement.Title), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private RepoDocDetail? ResolveSelectedSpec()
    {
        if (Specs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedReference))
        {
            var selected = Specs.FirstOrDefault(spec =>
                spec.Path.Equals(SelectedReference, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(spec.ArtifactId) && spec.ArtifactId.Equals(SelectedReference, StringComparison.OrdinalIgnoreCase)));
            if (selected is not null)
            {
                return Workspace.GetDoc(selected.ArtifactId ?? selected.Path);
            }

            var resolved = Workspace.GetDoc(SelectedReference);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return Workspace.GetDoc(Specs[0].ArtifactId ?? Specs[0].Path);
    }

    private bool ResolveCreateMode()
    {
        return CreateMode || string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase) || (Specs.Count == 0 && string.IsNullOrWhiteSpace(Query));
    }

    private SpecEditorInput CreateBlankSpecEditorInput()
    {
        var sourceFormat = Workspace.GetPreferredSpecFormat();

        return new SpecEditorInput
        {
            Owner = Profile.DefaultOwner ?? Profile.EffectiveAuthor,
            SourceFormat = sourceFormat,
            SchemaReference = string.Equals(sourceFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? DefaultSpecTraceSchemaReference
                : string.Empty,
            Requirements = [SpecRequirementEditorInput.CreateBlank()]
        };
    }

    private bool ShouldCreateSpec()
    {
        return CreateMode || string.IsNullOrWhiteSpace(Edit.Path) || string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase);
    }

    private void SetBanner(string title, string message)
    {
        BannerTitle = title;
        BannerMessage = message;
    }

    private static string FormatError(Exception ex)
    {
        return $"{ex.GetType().Name}: {ex.Message}";
    }

    private static List<string> SplitLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(line))
            {
                results.Add(line);
            }
        }

        return results;
    }

    private static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
