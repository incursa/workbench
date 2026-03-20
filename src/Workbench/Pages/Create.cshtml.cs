using Microsoft.AspNetCore.Mvc;

namespace Workbench.Pages;

public class CreateModel : RepoPageModel
{
    public CreateModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    [BindProperty]
    public WorkItemCreateInput Create { get; set; } = new();

    public static IReadOnlyList<string> StatusOptions => WorkbenchWorkspace.StatusOptions;

    public static IReadOnlyList<string> TypeOptions => WorkbenchWorkspace.TypeOptions;

    public static IReadOnlyList<string> PriorityOptions => ["", "low", "medium", "high", "critical"];

    public void OnGet()
    {
        ApplyChrome("Create work item");
        LoadDefaults();
    }

    public IActionResult OnPost()
    {
        ApplyChrome("Create work item");

        try
        {
            var created = Workspace.CreateItem(Create);
            return RedirectToPage("/Index", new
            {
                selectedId = created.Id,
                statusFilter = "all",
                query = string.Empty,
                includeDone = false
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadDefaults();
            return Page();
        }
    }

    private void LoadDefaults()
    {
        if (string.IsNullOrWhiteSpace(Create.Owner))
        {
            Create.Owner = Profile.DefaultOwner ?? Profile.EffectiveAuthor;
        }
    }

    private static string FormatError(Exception ex)
    {
        return $"{ex.GetType().Name}: {ex.Message}";
    }
}
