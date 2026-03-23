using Microsoft.AspNetCore.Mvc;

namespace Workbench.Pages;

public class DashboardModel : RepoPageModel
{
    public DashboardModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }
}
