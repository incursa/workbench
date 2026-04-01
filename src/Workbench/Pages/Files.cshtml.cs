using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench.Pages;

public class FilesModel : RepoPageModel
{
    private static readonly IReadOnlyList<string> fileTypes = ["all", "markdown", "text", "binary"];

    public FilesModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    public static IReadOnlyList<string> TypeOptions => fileTypes;

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedPath { get; set; }

    public IReadOnlyList<RepoFileSummary> Files { get; private set; } = Array.Empty<RepoFileSummary>();

    public RepoTreeBranch Tree { get; private set; } = new("Repository", string.Empty, 0, Array.Empty<RepoTreeBranch>(), Array.Empty<RepoTreeEntry>());

    public RepoFileDetail? SelectedFile { get; private set; }

    public int MarkdownCount { get; private set; }

    public int TextCount { get; private set; }

    public int BinaryCount { get; private set; }

    public static string FileTypeBadgeClass(string? fileType)
    {
        return fileType?.ToLowerInvariant() switch
        {
            "markdown" => "inc-badge--info",
            "text" => "inc-badge--success",
            "binary" => "inc-badge--warning",
            _ => "inc-badge--info"
        };
    }

    public void OnGet()
    {
        ApplyChrome("Files");
        LoadPage();
    }

    public string? SelectedFileHtml
    {
        get
        {
            if (SelectedFile is null)
            {
                return null;
            }

            if (SelectedFile.IsMarkdown)
            {
                return RepoContentRenderer.RenderMarkdown(SelectedFile.Body);
            }

            return null;
        }
    }

    private void LoadPage()
    {
        if (string.IsNullOrWhiteSpace(TypeFilter))
        {
            TypeFilter = "all";
        }

        Files = Workspace.ListFiles(TypeFilter, Query);
        Tree = WorkbenchWorkspace.BuildFileTree(
            Files,
            file => Url.Page("/Files", new { selectedPath = file.Path, typeFilter = TypeFilter, query = Query }) ?? file.Path,
            SelectedPath);
        MarkdownCount = Files.Count(file => string.Equals(file.FileType, "markdown", StringComparison.OrdinalIgnoreCase));
        TextCount = Files.Count(file => string.Equals(file.FileType, "text", StringComparison.OrdinalIgnoreCase));
        BinaryCount = Files.Count(file => string.Equals(file.FileType, "binary", StringComparison.OrdinalIgnoreCase));

        SelectedFile = ResolveSelectedFile();
        if (SelectedFile is null && Files.Count > 0)
        {
            SelectedFile = Workspace.GetFile(Files[0].Path);
            SelectedPath = SelectedFile?.Summary.Path;
        }

        if (SelectedFile is not null && string.IsNullOrWhiteSpace(SelectedPath))
        {
            SelectedPath = SelectedFile.Summary.Path;
        }
    }

    private RepoFileDetail? ResolveSelectedFile()
    {
        if (Files.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedPath))
        {
            var selected = Files.FirstOrDefault(file => file.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return Workspace.GetFile(selected.Path);
            }
        }

        return Workspace.GetFile(Files[0].Path);
    }
}
