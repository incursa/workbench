namespace Workbench.Core;

#pragma warning disable MA0048

internal static class CanonicalArtifactDiscovery
{
    public static IReadOnlyList<CanonicalArtifactSource> EnumerateCanonicalSources(string repoRoot, WorkbenchConfig config)
    {
        var sources = new List<CanonicalArtifactSource>();
        foreach (var root in GetCanonicalRoots(repoRoot, config))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var cueFiles = Directory
                .EnumerateFiles(root, "*.cue", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cueBasePaths = cueFiles
                .Select(path => Path.ChangeExtension(Path.GetFullPath(path), null))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var cueFile in cueFiles)
            {
                sources.Add(CreateSource(repoRoot, cueFile, "cue"));
            }

            foreach (var markdownFile in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (ShouldSkipCanonicalMarkdown(markdownFile))
                {
                    continue;
                }

                var basePath = Path.ChangeExtension(Path.GetFullPath(markdownFile), null);
                if (cueBasePaths.Contains(basePath))
                {
                    continue;
                }

                sources.Add(CreateSource(repoRoot, markdownFile, "markdown"));
            }
        }

        return sources
            .OrderBy(source => source.SourceRepoRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CanonicalArtifactSource CreateSource(string repoRoot, string sourcePath, string format)
    {
        var displayPath = sourcePath;
        if (string.Equals(format, "cue", StringComparison.OrdinalIgnoreCase))
        {
            var markdownPath = Path.ChangeExtension(sourcePath, ".md");
            if (File.Exists(markdownPath))
            {
                displayPath = markdownPath;
            }
        }

        return new CanonicalArtifactSource(
            sourcePath,
            NormalizeRepoRelative(repoRoot, sourcePath),
            displayPath,
            NormalizeRepoRelative(repoRoot, displayPath),
            format);
    }

    private static IEnumerable<string> GetCanonicalRoots(string repoRoot, WorkbenchConfig config)
    {
        var specsRoot = ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.SpecsRoot) ? SpecTraceLayout.SpecsRoot : config.Paths.SpecsRoot);
        yield return Path.Combine(specsRoot, "requirements");
        yield return ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.ArchitectureDir) ? SpecTraceLayout.ArchitectureRoot : config.Paths.ArchitectureDir);
        yield return ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.WorkItemsSpecsDir) ? SpecTraceLayout.WorkItemsRoot : config.Paths.WorkItemsSpecsDir);
        yield return Path.Combine(specsRoot, "verification");
    }

    private static bool ShouldSkipCanonicalMarkdown(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRepoPath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string NormalizeRepoRelative(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace('\\', '/').TrimStart('/');
    }
}

internal sealed record CanonicalArtifactSource(
    string SourcePath,
    string SourceRepoRelativePath,
    string DisplayPath,
    string DisplayRepoRelativePath,
    string Format);

#pragma warning restore MA0048
