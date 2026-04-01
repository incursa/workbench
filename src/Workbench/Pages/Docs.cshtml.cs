using Microsoft.AspNetCore.Mvc;
using Workbench;
using Workbench.Core;

namespace Workbench.Pages;

public class DocsModel : RepoPageModel
{
    private static readonly IReadOnlyList<string> filterTypeOptions = ["all", "architecture", "verification", "runbook", "doc"];
    private static readonly IReadOnlyList<string> editorTypeOptions = ["architecture", "verification", "runbook", "doc"];

    public DocsModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    public static IReadOnlyList<string> TypeOptions => filterTypeOptions;

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedReference { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool CreateMode { get; set; }

    [BindProperty]
    public DocEditorInput Edit { get; set; } = new();

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerMessage { get; set; }

    public IReadOnlyList<RepoDocSummary> Docs { get; private set; } = Array.Empty<RepoDocSummary>();

    public RepoTreeBranch Tree { get; private set; } = new("Documents", string.Empty, 0, Array.Empty<RepoTreeBranch>(), Array.Empty<RepoTreeEntry>());

    public RepoDocDetail? SelectedDoc { get; private set; }

    public bool IsCreateMode { get; private set; }

    public string SelectedDocHtml => SelectedDoc is null
        ? string.Empty
        : RepoContentRenderer.RenderMarkdown(SelectedDoc.Body);

    public IReadOnlyList<string> BannerLines =>
        string.IsNullOrWhiteSpace(BannerMessage)
            ? Array.Empty<string>()
            : BannerMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsArchitectureEditor => string.Equals(Edit.Type, "architecture", StringComparison.OrdinalIgnoreCase);

    public bool IsVerificationEditor => string.Equals(Edit.Type, "verification", StringComparison.OrdinalIgnoreCase);

    public bool IsSchemaEditor => IsArchitectureEditor || IsVerificationEditor;

    public string TraceSectionTitle => IsSchemaEditor ? "Schema metadata" : "Linked arrays";

    public string PrimaryArrayLabel
    {
        get
        {
            if (IsArchitectureEditor)
            {
                return "Satisfies";
            }

            if (IsVerificationEditor)
            {
                return "Verifies";
            }

            return "Work items";
        }
    }

    public string PrimaryArrayHint => IsSchemaEditor
        ? "List one requirement ID per line."
        : "List linked work item IDs, one per line.";

    public string PrimaryArrayPlaceholder => IsSchemaEditor
        ? "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"
        : "WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>";

    public static string RelatedArtifactsHint => "Use one artifact ID or repo path per line.";

    public static string CodeRefsHint => "Store repo-relative file references or code anchors here.";

    public string StatusHint
    {
        get
        {
            if (IsArchitectureEditor)
            {
                return "Use draft, proposed, approved, implemented, verified, superseded, or retired.";
            }

            if (IsVerificationEditor)
            {
                return "Use planned, passed, failed, blocked, waived, or obsolete.";
            }

            return "Use the repository's preferred status value.";
        }
    }

    public string PathHint => ResolvePathHint(Edit.Type);

    public string PathPreview => string.IsNullOrWhiteSpace(Edit.Path) ? PathHint : Edit.Path;

    public string SnapshotHint
    {
        get
        {
            if (!IsCreateMode)
            {
                return SelectedDoc?.Summary.Excerpt ?? string.Empty;
            }

            if (IsSchemaEditor)
            {
                return "Required fields: title, domain, status, owner, and the required array.";
            }

            return "Required fields are highlighted by the editor.";
        }
    }

    public void OnGet()
    {
        ApplyChrome("Docs");
        LoadPage(populateEditor: true);
    }

    public IActionResult OnPostSave()
    {
        try
        {
            LoadPage(populateEditor: false);

            var creating = ShouldCreateDoc();
            var selectedDoc = ResolveSelectedDoc(allowFallback: false);
            EnsureManagedDocMutation(creating, selectedDoc);

            RepoDocDetail savedDoc;
            if (creating)
            {
                savedDoc = Workspace.CreateDoc(Edit);
            }
            else
            {
                // The docs page edits in place. Keep the existing path as the reference that identifies the file.
                if (selectedDoc is not null)
                {
                    Edit.Path = selectedDoc.Summary.Path;
                }

                var result = Workspace.SaveDoc(Edit);
                savedDoc = Workspace.GetDoc(result.Path) ?? throw new InvalidOperationException("Failed to reload the saved document.");
            }

            SetBanner(
                creating ? "Doc created" : "Doc saved",
                $"{savedDoc.Summary.Path} updated locally.");

            return RedirectToPage(new
            {
                selectedReference = savedDoc.Summary.ArtifactId ?? savedDoc.Summary.Path,
                query = Query,
                typeFilter = TypeFilter,
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

    public IActionResult OnPostDelete()
    {
        try
        {
            LoadPage(populateEditor: false);

            var selectedDoc = ResolveSelectedDoc(allowFallback: false);
            if (selectedDoc is null)
            {
                throw new InvalidOperationException("A managed document must be selected before delete.");
            }

            var deleted = Workspace.DeleteDoc(selectedDoc.Summary.Path, keepLinks: false);
            SetBanner(
                "Doc deleted",
                $"{deleted.Doc.Path} removed locally.");

            return RedirectToPage(new
            {
                selectedReference = string.Empty,
                query = Query,
                typeFilter = TypeFilter,
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
        ApplyChrome("Docs");

        if (string.IsNullOrWhiteSpace(TypeFilter))
        {
            TypeFilter = "all";
        }

        Docs = Workspace.ListDocs(TypeFilter, Query)
            .Where(doc => IsEditorDocType(doc.Type))
            .ToList();
        IsCreateMode = ResolveCreateMode();
        CreateMode = IsCreateMode;
        SelectedDoc = IsCreateMode ? null : ResolveSelectedDoc(allowFallback: true);

        if (populateEditor)
        {
            Edit = IsCreateMode || SelectedDoc is null
                ? CreateBlankDocEditorInput(SelectedDoc)
                : Workspace.CreateDocEditorInput(SelectedDoc) ?? CreateBlankDocEditorInput(SelectedDoc);
        }

        if (SelectedDoc is not null && string.IsNullOrWhiteSpace(SelectedReference))
        {
            SelectedReference = SelectedDoc.Summary.ArtifactId ?? SelectedDoc.Summary.Path;
        }
        else if (!IsCreateMode && SelectedDoc is null && Docs.Count > 0 && string.IsNullOrWhiteSpace(SelectedReference))
        {
            SelectedReference = Docs[0].ArtifactId ?? Docs[0].Path;
        }

        Tree = WorkbenchWorkspace.BuildDocTree(
            Docs,
            doc => Url.Page("/Docs", new { selectedReference = doc.ArtifactId ?? doc.Path, query = Query, typeFilter = TypeFilter, createMode = CreateMode }) ?? doc.Path,
            IsCreateMode ? null : SelectedReference);
    }

    private RepoDocDetail? ResolveSelectedDoc(bool allowFallback)
    {
        if (Docs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedReference) &&
            !string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase))
        {
            var direct = Docs.FirstOrDefault(doc => ReferenceMatches(doc, SelectedReference));
            if (direct is not null)
            {
                var resolved = Workspace.GetDoc(direct.ArtifactId ?? direct.Path);
                if (resolved is not null && IsEditorDocType(resolved.Summary.Type))
                {
                    return resolved;
                }
            }

            var selected = Workspace.GetDoc(SelectedReference);
            if (selected is not null && IsEditorDocType(selected.Summary.Type))
            {
                return selected;
            }
        }

        if (!allowFallback)
        {
            return null;
        }

        var fallback = Workspace.GetDoc(Docs[0].ArtifactId ?? Docs[0].Path);
        return fallback is not null && IsEditorDocType(fallback.Summary.Type) ? fallback : null;
    }

    private bool ResolveCreateMode()
    {
        return CreateMode
            || string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase)
            || (Docs.Count == 0 && string.IsNullOrWhiteSpace(Query));
    }

    private DocEditorInput CreateBlankDocEditorInput(RepoDocDetail? selectedDoc)
    {
        var type = ResolveDefaultDocType(selectedDoc);
        return new DocEditorInput
        {
            Type = type,
            Status = ResolveDefaultDocStatus(type),
            Owner = Profile.DefaultOwner ?? Profile.EffectiveAuthor,
            Body = DocBodyBuilder.BuildSkeleton(type, "New document")
        };
    }

    private string ResolveDefaultDocType(RepoDocDetail? selectedDoc)
    {
        if (selectedDoc is not null && IsEditorDocType(selectedDoc.Summary.Type))
        {
            return selectedDoc.Summary.Type;
        }

        if (!string.IsNullOrWhiteSpace(TypeFilter) &&
            !string.Equals(TypeFilter, "all", StringComparison.OrdinalIgnoreCase) &&
            IsEditorDocType(TypeFilter))
        {
            return TypeFilter!;
        }

        return "doc";
    }

    private bool ShouldCreateDoc()
    {
        return IsCreateMode || string.Equals(SelectedReference, "new", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureManagedDocMutation(bool creating, RepoDocDetail? selectedDoc)
    {
        if (creating)
        {
            if (!IsEditorDocType(Edit.Type))
            {
                throw new InvalidOperationException("Docs page only manages architecture, verification, runbook, and doc files.");
            }

            ValidateManagedDocFields(Edit.Type, Edit);

            return;
        }

        var currentDoc = selectedDoc ?? Workspace.GetDoc(Edit.Path);
        if (currentDoc is null || !IsEditorDocType(currentDoc.Summary.Type))
        {
            throw new InvalidOperationException("Docs page only manages architecture, verification, runbook, and doc files.");
        }

        Edit.Path = currentDoc.Summary.Path;
        Edit.Type = currentDoc.Summary.Type;
        ValidateManagedDocFields(Edit.Type, Edit);
    }

    private static bool IsEditorDocType(string? type)
    {
        return !string.IsNullOrWhiteSpace(type) &&
            editorTypeOptions.Any(entry => string.Equals(entry, type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReferenceMatches(RepoDocSummary doc, string reference)
    {
        return doc.Path.Equals(reference, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(doc.ArtifactId) &&
             doc.ArtifactId.Equals(reference, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveDefaultDocStatus(string type)
    {
        return string.Equals(type, "verification", StringComparison.OrdinalIgnoreCase)
            ? "planned"
            : "draft";
    }

    private static string ResolvePathHint(string? type)
    {
        if (string.Equals(type, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "specs/architecture/<DOMAIN>/ARC-<DOMAIN>-....md";
        }

        if (string.Equals(type, "verification", StringComparison.OrdinalIgnoreCase))
        {
            return "specs/verification/<DOMAIN>/VER-<DOMAIN>-....md";
        }

        if (string.Equals(type, "runbook", StringComparison.OrdinalIgnoreCase))
        {
            return "runbooks/incident-response.md";
        }

        return "tracking/repo-note.md";
    }

    private static void ValidateManagedDocFields(string type, DocEditorInput edit)
    {
        if (string.IsNullOrWhiteSpace(edit.Title))
        {
            throw new InvalidOperationException("Doc title is required.");
        }

        if (string.Equals(type, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(edit.Domain))
            {
                throw new InvalidOperationException("Architecture docs require a domain.");
            }

            if (string.IsNullOrWhiteSpace(edit.Status))
            {
                throw new InvalidOperationException("Architecture docs require a status.");
            }

            if (string.IsNullOrWhiteSpace(edit.Owner))
            {
                throw new InvalidOperationException("Architecture docs require an owner.");
            }

            if (string.IsNullOrWhiteSpace(edit.Satisfies))
            {
                throw new InvalidOperationException("Architecture docs require at least one requirement in satisfies.");
            }
        }
        else if (string.Equals(type, "verification", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(edit.Domain))
            {
                throw new InvalidOperationException("Verification docs require a domain.");
            }

            if (string.IsNullOrWhiteSpace(edit.Status))
            {
                throw new InvalidOperationException("Verification docs require a status.");
            }

            if (string.IsNullOrWhiteSpace(edit.Owner))
            {
                throw new InvalidOperationException("Verification docs require an owner.");
            }

            if (string.IsNullOrWhiteSpace(edit.Verifies))
            {
                throw new InvalidOperationException("Verification docs require at least one requirement in verifies.");
            }
        }
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
