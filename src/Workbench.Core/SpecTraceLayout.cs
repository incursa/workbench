namespace Workbench.Core;

/// <summary>
/// Shared layout helpers for the canonical SpecTrace repository tree.
/// </summary>
public static class SpecTraceLayout
{
    public const string SpecsRoot = "specs";
    public const string RequirementsRoot = "specs/requirements";
    public const string ArchitectureRoot = "specs/architecture";
    public const string VerificationRoot = "specs/verification";
    public const string WorkItemsRoot = "specs/work-items";
    public const string GeneratedRoot = "specs/generated";
    public const string TemplatesRoot = "specs/templates";
    public const string SchemasRoot = "specs/schemas";

    public static string GetDefaultDomain(string repoRoot)
    {
        var canonicalRoots = new[]
        {
            Path.Combine(repoRoot, WorkItemsRoot),
            Path.Combine(repoRoot, ArchitectureRoot),
            Path.Combine(repoRoot, VerificationRoot),
            Path.Combine(repoRoot, RequirementsRoot)
        };

        foreach (var root in canonicalRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var firstDomain = Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            if (!string.IsNullOrWhiteSpace(firstDomain))
            {
                return ArtifactIdPolicy.NormalizeToken(firstDomain);
            }
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(repoRoot));
        var normalized = ArtifactIdPolicy.NormalizeToken(name);
        return string.IsNullOrWhiteSpace(normalized) ? "WORKBENCH" : normalized;
    }

    public static string GetSpecificationDirectory(string repoRoot)
    {
        return Path.Combine(repoRoot, RequirementsRoot);
    }

    public static string GetArchitectureDirectory(string repoRoot, string domain)
    {
        return Path.Combine(repoRoot, ArchitectureRoot, ArtifactIdPolicy.NormalizeToken(domain));
    }

    public static string GetWorkItemDirectory(string repoRoot, string domain)
    {
        return Path.Combine(repoRoot, WorkItemsRoot, ArtifactIdPolicy.NormalizeToken(domain));
    }

    public static string GetVerificationDirectory(string repoRoot, string domain)
    {
        return Path.Combine(repoRoot, VerificationRoot, ArtifactIdPolicy.NormalizeToken(domain));
    }

    public static string GetSpecificationPath(string repoRoot, string artifactId)
    {
        return Path.Combine(GetSpecificationDirectory(repoRoot), GetDefaultDomain(repoRoot), $"{artifactId.Trim()}.md");
    }

    public static string GetSpecificationPath(string repoRoot, string domain, string artifactId)
    {
        return Path.Combine(GetSpecificationDirectory(repoRoot), ArtifactIdPolicy.NormalizeToken(domain), $"{artifactId.Trim()}.md");
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
        return normalized.StartsWith($"{RequirementsRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{VerificationRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{WorkItemsRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{GeneratedRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{TemplatesRoot}/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{SchemasRoot}/", StringComparison.OrdinalIgnoreCase);
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

    public static bool IsCanonicalVerificationPath(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        return normalized.StartsWith($"{VerificationRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCanonicalWorkItemPath(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        return normalized.StartsWith($"{WorkItemsRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSpecificationRootFile(string repoRelativePath)
    {
        var normalized = NormalizePath(repoRelativePath);
        if (!normalized.StartsWith($"{RequirementsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = normalized[(RequirementsRoot.Length + 1)..];
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        return segments[1].StartsWith("SPEC-", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(normalized).Equals("_index.md", StringComparison.OrdinalIgnoreCase);
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
        if (normalized.StartsWith($"{RequirementsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "requirements";
        }

        if (normalized.StartsWith($"{ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture";
        }

        if (normalized.StartsWith($"{VerificationRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "verification";
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
