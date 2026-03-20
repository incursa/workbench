using Microsoft.AspNetCore.Mvc;

namespace Workbench.Pages;

public class SettingsModel : RepoPageModel
{
    private readonly WorkbenchUserProfileStore profileStore;

    public SettingsModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
        this.profileStore = profileStore;
    }

    [BindProperty]
    public string? DisplayName { get; set; }

    [BindProperty]
    public string? Handle { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? DefaultOwner { get; set; }

    public string? Message { get; private set; }

    public string EffectiveAuthorDisplay => ResolveEffectiveAuthor();

    public void OnGet()
    {
        ApplyChrome("Settings");
        LoadProfile();
    }

    public IActionResult OnPost()
    {
        ApplyChrome("Settings");

        var profile = new WorkbenchUserProfile
        {
            DisplayName = Normalize(DisplayName),
            Handle = Normalize(Handle),
            Email = Normalize(Email),
            DefaultOwner = Normalize(DefaultOwner)
        };

        profileStore.Save(profile);
        Message = $"Saved local profile at {profileStore.ProfilePath}.";
        LoadProfile(profile);
        return Page();
    }

    private void LoadProfile(WorkbenchUserProfile? profile = null)
    {
        profile ??= Profile;
        DisplayName = profile.DisplayName;
        Handle = profile.Handle;
        Email = profile.Email;
        DefaultOwner = profile.DefaultOwner;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string ResolveEffectiveAuthor()
    {
        if (!string.IsNullOrWhiteSpace(DefaultOwner))
        {
            return DefaultOwner!;
        }

        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return DisplayName!;
        }

        if (!string.IsNullOrWhiteSpace(Handle))
        {
            return Handle!;
        }

        return "unset";
    }
}
