using System.Text.Json;

namespace Workbench;

public sealed class WorkbenchUserProfileStore
{
    public WorkbenchUserProfileStore()
        : this(GetDefaultProfilePath())
    {
    }

    public WorkbenchUserProfileStore(string profilePath)
    {
        ProfilePath = profilePath;
    }

    public string ProfilePath { get; }

    public WorkbenchUserProfile Load(out string? error)
    {
        error = null;
        if (!File.Exists(ProfilePath))
        {
            return new WorkbenchUserProfile();
        }

        try
        {
            var json = File.ReadAllText(ProfilePath);
            var profile = JsonSerializer.Deserialize<WorkbenchUserProfile>(json);
            return profile ?? new WorkbenchUserProfile();
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return new WorkbenchUserProfile();
        }
    }

    public WorkbenchUserProfile Load()
    {
        return Load(out _);
    }

    public void Save(WorkbenchUserProfile profile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ProfilePath, json + "\n");
    }

    public static string GetDefaultProfilePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "Incursa", "Workbench", "profile.json");
    }
}
