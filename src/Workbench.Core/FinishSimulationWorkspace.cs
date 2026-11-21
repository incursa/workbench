namespace Workbench.Core;

/// <summary>
/// Isolated repository mirror used to execute a prospective finish without
/// mutating the source repository.
/// </summary>
internal sealed class FinishSimulationWorkspace : IDisposable
{
    private static readonly IReadOnlySet<string> excludedDirectoryNames = new HashSet<string>(
        [".git", ".tools", "bin", "obj"],
        StringComparer.OrdinalIgnoreCase);

    private readonly string workspaceRoot;

    private FinishSimulationWorkspace(string workspaceRoot)
    {
        this.workspaceRoot = workspaceRoot;
    }

    public string Root => workspaceRoot;

    public static FinishSimulationWorkspace Create(
        string repoRoot,
        WorkbenchConfig config,
        string workItemPath)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot);
        var workspace = Directory.CreateTempSubdirectory("workbench-finish-");
        var privateFiles = BuildPrivateFileSet(normalizedRoot, config, workItemPath);

        try
        {
            MirrorDirectory(normalizedRoot, workspace.FullName, normalizedRoot, privateFiles);
            return new FinishSimulationWorkspace(workspace.FullName);
        }
        catch
        {
            workspace.Delete(recursive: true);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
#pragma warning disable ERP022
        catch
        {
            // Best-effort cleanup of an isolated temporary workspace.
        }
#pragma warning restore ERP022
    }

    private static HashSet<string> BuildPrivateFileSet(
        string repoRoot,
        WorkbenchConfig config,
        string workItemPath)
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeRelative(repoRoot, workItemPath),
            "README.md",
            NormalizeRelative(repoRoot, Path.Combine(repoRoot, config.Paths.DocsRoot, "README.md")),
            NormalizeRelative(repoRoot, Path.Combine(repoRoot, config.Paths.SpecsRoot, "README.md")),
            NormalizeRelative(repoRoot, Path.Combine(repoRoot, config.Paths.ArchitectureDir, "README.md")),
            NormalizeRelative(repoRoot, Path.Combine(repoRoot, config.Paths.WorkRoot, "README.md"))
        };
    }

    private static void MirrorDirectory(
        string source,
        string destination,
        string repoRoot,
        IReadOnlySet<string> privateFiles)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var destinationPath = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo directory)
            {
                if (excludedDirectoryNames.Contains(directory.Name))
                {
                    continue;
                }

                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    CreateDirectoryLink(destinationPath, directory.LinkTarget);
                    continue;
                }

                MirrorDirectory(directory.FullName, destinationPath, repoRoot, privateFiles);
                continue;
            }

            var file = (FileInfo)entry;
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                CreateFileLink(destinationPath, file.LinkTarget);
                continue;
            }

            var relative = NormalizeRelative(repoRoot, file.FullName);
            if (privateFiles.Contains(relative))
            {
                File.Copy(file.FullName, destinationPath, overwrite: false);
                continue;
            }

            try
            {
                File.CreateSymbolicLink(destinationPath, file.FullName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                File.Copy(file.FullName, destinationPath, overwrite: false);
            }
        }
    }

    private static void CreateDirectoryLink(string path, string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            Directory.CreateSymbolicLink(path, target);
        }
    }

    private static void CreateFileLink(string path, string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            File.CreateSymbolicLink(path, target);
        }
    }

    private static string NormalizeRelative(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, Path.GetFullPath(path)).Replace('\\', '/');
    }
}
