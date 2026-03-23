using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench.Pages;

public class SpecsModel : RepoPageModel
{
    private static readonly IReadOnlyList<string> documentTypes = ["specification"];

    public SpecsModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    public static IReadOnlyList<string> TypeOptions => documentTypes;

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

    public IReadOnlyList<RepoDocSummary> Specs { get; private set; } = Array.Empty<RepoDocSummary>();

    public RepoTreeBranch Tree { get; private set; } = new("Repository", string.Empty, 0, Array.Empty<RepoTreeBranch>(), Array.Empty<RepoTreeEntry>());

    public RepoDocDetail? SelectedSpec { get; private set; }

    public bool IsCreateMode { get; private set; }

    public string SpecIdPolicySummary { get; private set; } = string.Empty;

    public string SelectedSpecHtml => SelectedSpec is null
        ? string.Empty
        : RepoContentRenderer.RenderMarkdown(SelectedSpec.Body);

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
            RepoDocDetail? selectedSpec;
            if (created)
            {
                selectedSpec = Workspace.CreateSpec(Edit);
            }
            else
            {
                var result = Workspace.SaveSpec(Edit);
                selectedSpec = Workspace.GetDoc(result.Path);
            }

            if (selectedSpec is null)
            {
                throw new InvalidOperationException("Failed to reload spec.");
            }

            SetBanner(
                created ? "Spec created" : "Spec saved",
                $"{selectedSpec.Summary.Path} updated locally.");
            return RedirectToPage(new
            {
                selectedReference = selectedSpec.Summary.ArtifactId ?? selectedSpec.Summary.Path,
                query = Query,
                createMode = false
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    private void LoadPage(bool populateEditor)
    {
        if (!string.IsNullOrWhiteSpace(SelectedReference))
        {
            SelectedReference = SelectedReference.Trim();
        }

        Specs = Workspace.ListDocs("specification", Query);
        IsCreateMode = ResolveCreateMode();

        var selected = IsCreateMode ? null : ResolveSelectedSpec();
        SelectedSpec = selected;

        if (populateEditor)
        {
            if (IsCreateMode || selected is null)
            {
                Edit = CreateBlankSpecEditorInput();
            }
            else
            {
                Edit = Workspace.CreateSpecEditorInput(selected);
            }
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
        Tree = WorkbenchWorkspace.BuildDocTree(
            Specs,
            doc => Url.Page("/Specs", new { selectedReference = doc.ArtifactId ?? doc.Path, query = Query }) ?? doc.Path,
            IsCreateMode ? null : SelectedReference);
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
        return CreateMode
            || string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase)
            || (Specs.Count == 0 && string.IsNullOrWhiteSpace(Query));
    }

    private SpecEditorInput CreateBlankSpecEditorInput()
    {
        return new SpecEditorInput
        {
            Owner = Profile.DefaultOwner ?? Profile.EffectiveAuthor
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
