using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Workbench;

public abstract class RepoPageModel : PageModel
{
    protected RepoPageModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
    {
        Workspace = workspace;
        ProfileStore = profileStore;
        Profile = profileStore.Load();
    }

    protected WorkbenchWorkspace Workspace { get; }

    protected WorkbenchUserProfileStore ProfileStore { get; }

    protected WorkbenchUserProfile Profile { get; }

    public string RepoRoot => Workspace.RepoRoot;

    public string ProfilePath => ProfileStore.ProfilePath;

    public WorkbenchUserProfile CurrentProfile => Profile;

    public string ProfileName => string.IsNullOrWhiteSpace(Profile.DisplayName)
        ? GetProfileFallbackName()
        : Profile.DisplayName;

    protected void ApplyChrome(string title)
    {
        ViewData["Title"] = title;
        ViewData["RepoRoot"] = RepoRoot;
        ViewData["ProfileName"] = ProfileName;
        ViewData["ProfileSummary"] = Profile.Summary;
    }

    private string GetProfileFallbackName()
    {
        if (!string.IsNullOrWhiteSpace(Profile.Handle))
        {
            return Profile.Handle;
        }

        return "Local profile not set";
    }
}
