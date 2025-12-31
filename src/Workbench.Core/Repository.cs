namespace Workbench;

public static class Repository
{
    public static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startPath));
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
