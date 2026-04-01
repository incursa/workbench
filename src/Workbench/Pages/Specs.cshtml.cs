using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench.Pages;

public class SpecsModel : RepoPageModel
{
    private const string DefaultSpecTraceSchemaReference = "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json";

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

    public bool IsJsonEditor => string.Equals(Edit.SourceFormat, "json", StringComparison.OrdinalIgnoreCase);

    public bool IsLegacyMarkdownSpec => !IsCreateMode && SelectedSpec is not null && SelectedSpec.Summary.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public string SelectedSpecSource => SelectedSpec?.Body ?? string.Empty;

    public IReadOnlyList<string> BannerLines =>
        string.IsNullOrWhiteSpace(BannerMessage)
            ? Array.Empty<string>()
            : BannerMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void OnGet()
    {
        ApplyChrome("Specs");
        LoadPage(populateEditor: true);
    }

    public IActionResult OnPostSave()
    {
        try
        {
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
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostDelete()
    {
        try
        {
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
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostAddRequirement()
    {
        ApplyChrome("Specs");
        Edit.Requirements.Add(SpecRequirementEditorInput.CreateBlank());
        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostRemoveRequirement(int index)
    {
        ApplyChrome("Specs");
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
        Edit.SupplementalSections.Add(SpecSupplementalSectionEditorInput.CreateBlank());
        LoadPage(populateEditor: false);
        return Page();
    }

    public IActionResult OnPostRemoveSupplementalSection(int index)
    {
        ApplyChrome("Specs");
        if (index >= 0 && index < Edit.SupplementalSections.Count)
        {
            Edit.SupplementalSections.RemoveAt(index);
        }

        LoadPage(populateEditor: false);
        return Page();
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

        var domain = string.IsNullOrWhiteSpace(Edit.Domain) ? "<domain>" : Edit.Domain.Trim().ToLowerInvariant().Replace(' ', '-');
        var artifactId = string.IsNullOrWhiteSpace(Edit.ArtifactId) ? "SPEC-<DOMAIN>-<GROUPING>" : Edit.ArtifactId.Trim();
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
                        Path.GetExtension(spec.Path).TrimStart('.'),
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
}
