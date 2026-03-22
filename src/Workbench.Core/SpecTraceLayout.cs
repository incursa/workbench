namespace Workbench.Core;

/// <summary>
/// Shared layout helpers for the canonical SpecTrace repository tree.
/// </summary>
public static class SpecTraceLayout
{
    public const string SpecsRoot = "specs";
    public const string ArchitectureRoot = "architecture";
    public const string WorkItemsRoot = "work/items";
    public const string GeneratedRoot = "specs/generated";
    public const string TemplatesRoot = "specs/templates";
    public const string SchemasRoot = "specs/schemas";

    public static string GetDefaultDomain(string repoRoot)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(repoRoot));
        var normalized = ArtifactIdPolicy.NormalizeToken(name);
        return string.IsNullOrWhiteSpace(normalized) ? "WORKBENCH" : normalized;
    }

    public static string GetSpecificationDirectory(string repoRoot)
    {
        return Path.Combine(repoRoot, SpecsRoot);
    }

    public static string GetArchitectureDirectory(string repoRoot, string domain)
    {
        return Path.Combine(repoRoot, ArchitectureRoot, ArtifactIdPolicy.NormalizeToken(domain));
    }

    public static string GetWorkItemDirectory(string repoRoot, string domain)
    {
        return Path.Combine(repoRoot, WorkItemsRoot, ArtifactIdPolicy.NormalizeToken(domain));
    }

    public static string GetSpecificationPath(string repoRoot, string artifactId)
    {
        return Path.Combine(GetSpecificationDirectory(repoRoot), $"{artifactId.Trim()}.md");
    }

    public static string GetSpecificationPath(string repoRoot, string domain, string artifactId)
    {
        _ = domain;
        return GetSpecificationPath(repoRoot, artifactId);
    }

    public static string GetArchitecturePath(string repoRoot, string domain, string artifactId, string title)
    {
        return Path.Combine(GetArchitectureDirectory(repoRoot, domain), $"{GetReadableFileName(title, artifactId)}.md");
    }

    public static string GetWorkItemPath(string repoRoot, string domain, string artifactId, string title)
    {
        return Path.Combine(GetWorkItemDirectory(repoRoot, domain), $"{GetReadableFileName(title, artifactId)}.md");
    }

    public static bool IsCanonicalPath(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        return normalized.StartsWith($"{SpecsRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCanonicalSpecificationPath(string repoRelativePath)
    {
        return IsSpecificationRootFile(repoRelativePath);
    }

    public static bool IsCanonicalArchitecturePath(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        return normalized.StartsWith($"{ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCanonicalWorkItemPath(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        return normalized.StartsWith($"{WorkItemsRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSpecificationRootFile(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        if (!normalized.StartsWith($"{SpecsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = normalized[(SpecsRoot.Length + 1)..];
        if (remainder.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        return !Path.GetFileName(normalized).Equals("README.md", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDirectChildPath(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullPath);
        if (parent is null)
        {
            return false;
        }

        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedParent, fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetCanonicalSection(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        if (normalized.StartsWith($"{SpecsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "specs";
        }

        if (normalized.StartsWith($"{ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture";
        }

        if (normalized.StartsWith($"{WorkItemsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "work-items";
        }

        if (normalized.StartsWith($"{GeneratedRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "generated";
        }

        if (normalized.StartsWith($"{TemplatesRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "templates";
        }

        if (normalized.StartsWith($"{SchemasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "schemas";
        }

        return null;
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string GetReadableFileName(string title, string artifactId)
    {
        var slug = WorkItemService.Slugify(title);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        var fallback = ArtifactIdPolicy.NormalizeToken(artifactId);
        return string.IsNullOrWhiteSpace(fallback) ? "artifact" : fallback.ToLowerInvariant();
    }
}
