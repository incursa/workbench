using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench.Pages;

public class DocsModel : RepoPageModel
{
    private static readonly IReadOnlyList<string> documentTypes = ["all", "specification", "architecture", "work_item", "doc", "guide"];

    public DocsModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    public static IReadOnlyList<string> TypeOptions => documentTypes;

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedPath { get; set; }

    public IReadOnlyList<RepoDocSummary> Docs { get; private set; } = Array.Empty<RepoDocSummary>();

    public RepoTreeBranch Tree { get; private set; } = new("Repository", string.Empty, 0, Array.Empty<RepoTreeBranch>(), Array.Empty<RepoTreeEntry>());

    public RepoDocDetail? SelectedDoc { get; private set; }

    public string SelectedDocHtml => SelectedDoc is null
        ? string.Empty
        : RepoContentRenderer.RenderMarkdown(SelectedDoc.Body);

    public void OnGet()
    {
        ApplyChrome("Docs");
        LoadPage();
    }

    private void LoadPage()
    {
        if (string.IsNullOrWhiteSpace(TypeFilter))
        {
            TypeFilter = "all";
        }

        Docs = Workspace.ListDocs(TypeFilter, Query);
        Tree = WorkbenchWorkspace.BuildDocTree(
            Docs,
            doc => Url.Page("/Docs", new { selectedPath = doc.ArtifactId ?? doc.Path, typeFilter = TypeFilter, query = Query }) ?? doc.Path,
            SelectedPath);
        SelectedDoc = ResolveSelectedDoc();
        if (SelectedDoc is null && Docs.Count > 0)
        {
            SelectedDoc = Workspace.GetDoc(Docs[0].Path);
            SelectedPath = SelectedDoc?.Summary.Path;
        }

        if (SelectedDoc is not null && string.IsNullOrWhiteSpace(SelectedPath))
        {
            SelectedPath = SelectedDoc.Summary.Path;
        }
    }

    private RepoDocDetail? ResolveSelectedDoc()
    {
        if (Docs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedPath))
        {
            var direct = Workspace.GetDoc(SelectedPath);
            if (direct is not null)
            {
                return direct;
            }

            var selected = Docs.FirstOrDefault(doc => doc.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return Workspace.GetDoc(selected.Path);
            }

            selected = Docs.FirstOrDefault(doc =>
                !string.IsNullOrWhiteSpace(doc.ArtifactId) &&
                doc.ArtifactId.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return Workspace.GetDoc(selected.Path);
            }
        }

        return Workspace.GetDoc(Docs[0].Path);
    }
}
