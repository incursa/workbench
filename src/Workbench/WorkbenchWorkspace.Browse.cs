using System.Collections;
using System.Globalization;
using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    public IReadOnlyList<RepoDocSummary> ListDocs(string? typeFilter, string? query)
    {
        var docsRoot = Path.Combine(RepoRoot, Config.Paths.DocsRoot);
        var requirementsRoot = Path.Combine(RepoRoot, SpecTraceLayout.RequirementsRoot);
        var architectureRoot = Path.Combine(RepoRoot, SpecTraceLayout.ArchitectureRoot);
        var verificationRoot = Path.Combine(RepoRoot, SpecTraceLayout.VerificationRoot);
        var generatedRoot = Path.Combine(RepoRoot, SpecTraceLayout.GeneratedRoot);
        var runbooksRoot = Path.Combine(RepoRoot, "runbooks");
        var trackingRoot = Path.Combine(RepoRoot, "tracking");
        if (!Directory.Exists(docsRoot) &&
            !Directory.Exists(runbooksRoot) &&
            !Directory.Exists(trackingRoot) &&
            !Directory.Exists(requirementsRoot) &&
            !Directory.Exists(architectureRoot) &&
            !Directory.Exists(verificationRoot) &&
            !Directory.Exists(generatedRoot))
        {
            return Array.Empty<RepoDocSummary>();
        }

        var docs = new List<RepoDocSummary>();
        foreach (var root in new[] { docsRoot, runbooksRoot, trackingRoot, requirementsRoot, architectureRoot, verificationRoot, generatedRoot })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var relative = NormalizePath(Path.GetRelativePath(RepoRoot, file));
                if (IsWorkItemArtifactDoc(relative) || IsDocTemplate(relative))
                {
                    continue;
                }

                var summary = LoadDocSummary(file, relative);
                if (summary is null)
                {
                    continue;
                }

                if (!DocTypeMatchesFilter(summary.Type, typeFilter))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(query) && !MatchesQuery(summary, query))
                {
                    continue;
                }

                docs.Add(summary);
            }
        }

        foreach (var source in CanonicalArtifactDiscovery.EnumerateCanonicalSources(RepoRoot, Config)
                     .Where(source => string.Equals(source.Format, "json", StringComparison.OrdinalIgnoreCase)))
        {
            RepoDocSummary? summary = null;
#pragma warning disable ERP022
            try
            {
                var document = CanonicalArtifactJsonLoader.LoadDocument(RepoRoot, source.SourcePath);
                if (!string.Equals(document.Artifact.ArtifactType, "work_item", StringComparison.OrdinalIgnoreCase))
                {
                    summary = BuildDocSummary(
                        source.DisplayRepoRelativePath,
                        (IDictionary<string, object?>)document.Data,
                        document.Artifact.Context ?? document.Artifact.Purpose ?? document.SourceText);
                }
            }
            catch
            {
                summary = null;
            }
#pragma warning restore ERP022

            if (summary is null)
            {
                continue;
            }

            if (!DocTypeMatchesFilter(summary.Type, typeFilter))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query) && !MatchesQuery(summary, query))
            {
                continue;
            }

            docs.Add(summary);
        }

        return docs
            .OrderBy(doc => doc.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public RepoDocDetail? GetDoc(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!DocService.TryResolveDocPath(RepoRoot, Config, path, out var resolvedPath))
        {
            return null;
        }

        Dictionary<string, object?> data;
        string body;
        if (resolvedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var document = CanonicalArtifactJsonLoader.LoadDocument(RepoRoot, resolvedPath);
            data = new Dictionary<string, object?>(document.Data, StringComparer.OrdinalIgnoreCase);
            body = document.SourceText;
        }
        else
        {
            var content = File.ReadAllText(resolvedPath);
            data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            body = content;
            if (FrontMatter.TryParse(content, out var frontMatter, out _))
            {
                data = new Dictionary<string, object?>(frontMatter!.Data, StringComparer.OrdinalIgnoreCase);
                body = frontMatter.Body;
            }
        }

        var relative = NormalizePath(Path.GetRelativePath(RepoRoot, resolvedPath));
        var summary = BuildDocSummary(relative, data, body);
        return new RepoDocDetail(summary, body, data);
    }

    public IReadOnlyList<RepoDocSummary> GetRecentDocs(int count)
    {
        return ListDocs(typeFilter: null, query: null)
            .OrderByDescending(doc => File.GetLastWriteTimeUtc(Path.Combine(RepoRoot, doc.Path)))
            .Take(count)
            .ToList();
    }

    public static RepoTreeBranch BuildDocTree(
        IReadOnlyList<RepoDocSummary> docs,
        Func<RepoDocSummary, string> hrefFactory,
        string? selectedPath)
    {
        var entries = docs.Select(doc => new RepoTreeEntry(
            doc.Path,
            doc.Title,
            doc.Type,
            string.IsNullOrWhiteSpace(doc.ArtifactId) ? doc.Status : $"{doc.ArtifactId} · {doc.Status}",
            string.IsNullOrWhiteSpace(doc.Excerpt) ? null : doc.Excerpt,
            hrefFactory(doc),
            IsSelected: !string.IsNullOrWhiteSpace(selectedPath) &&
                (doc.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase) ||
                 (!string.IsNullOrWhiteSpace(doc.ArtifactId) &&
                  doc.ArtifactId.Equals(selectedPath, StringComparison.OrdinalIgnoreCase)))))
            .ToList();

        return RepoTreeBuilder.BuildRoot(entries);
    }

    public IReadOnlyList<RepoFileSummary> ListFiles(string? typeFilter, string? query)
    {
        if (!Directory.Exists(RepoRoot))
        {
            return Array.Empty<RepoFileSummary>();
        }

        var files = new List<RepoFileSummary>();
        foreach (var file in EnumerateRepoFiles(RepoRoot))
        {
            var relative = NormalizePath(Path.GetRelativePath(RepoRoot, file));
            var summary = BuildFileSummary(file, relative);
            if (summary is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(typeFilter) &&
                !string.Equals(typeFilter, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(summary.FileType, typeFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query) && !MatchesQuery(summary, query))
            {
                continue;
            }

            files.Add(summary);
        }

        return files
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static RepoTreeBranch BuildFileTree(
        IReadOnlyList<RepoFileSummary> files,
        Func<RepoFileSummary, string> hrefFactory,
        string? selectedPath)
    {
        var entries = files.Select(file => new RepoTreeEntry(
            file.Path,
            file.Name,
            file.FileType,
            $"{file.SizeBytes} bytes",
            string.IsNullOrWhiteSpace(file.Excerpt) ? null : file.Excerpt,
            hrefFactory(file),
            IsSelected: !string.IsNullOrWhiteSpace(selectedPath) &&
                file.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return RepoTreeBuilder.BuildRoot(entries);
    }

    public RepoFileDetail? GetFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolvedPath = ResolveDocPath(path);
        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        var summary = BuildFileSummary(
            resolvedPath,
            NormalizePath(Path.GetRelativePath(RepoRoot, resolvedPath)));
        if (summary is null)
        {
            return null;
        }

        if (string.Equals(summary.FileType, "binary", StringComparison.OrdinalIgnoreCase))
        {
            return new RepoFileDetail(summary, string.Empty, IsMarkdown: false, IsBinary: true);
        }

        var body = File.ReadAllText(resolvedPath);
        return new RepoFileDetail(
            summary,
            body,
            IsMarkdown: string.Equals(summary.FileType, "markdown", StringComparison.OrdinalIgnoreCase),
            IsBinary: false);
    }

    private static RepoDocSummary? LoadDocSummary(string path, string relative)
    {
        var content = File.ReadAllText(path);
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var body = content;

        if (FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            data = new Dictionary<string, object?>(frontMatter!.Data, StringComparer.OrdinalIgnoreCase);
            body = frontMatter.Body;
        }

        return BuildDocSummary(relative, data, body);
    }

    private static RepoDocSummary BuildDocSummary(string relative, IDictionary<string, object?> data, string body)
    {
        var workbench = GetNestedMap(data, "workbench");
        var type = GetString(data, "artifact_type") ?? GetString(workbench, "type") ?? InferDocType(relative);
        var status = GetString(data, "status") ?? GetString(workbench, "status") ?? "unknown";
        var title = GetString(data, "title") ?? ExtractTitle(body) ?? Path.GetFileNameWithoutExtension(relative);
        var section = GetDocSection(relative);
        var relatedArtifacts = GetStringList(data, "related_artifacts");
        if (relatedArtifacts.Count == 0)
        {
            relatedArtifacts = GetStringList(data, "related");
        }
        if (relatedArtifacts.Count == 0)
        {
            relatedArtifacts = GetStringList(workbench, "workItems");
        }

        var workItems = FilterWorkItemArtifactIds(relatedArtifacts);
        if (workItems.Count == 0)
        {
            workItems = GetStringList(workbench, "workItems");
        }
        var tags = GetStringList(data, "tags");
        var satisfies = GetStringList(data, "satisfies");
        var verifies = GetStringList(data, "verifies");
        var excerpt = ExtractExcerpt(body);
        var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
        var domain = GetString(data, "domain");
        var capability = GetString(data, "capability");

        return new RepoDocSummary(relative, artifactId, domain, capability, title, type, status, section, excerpt, workItems, relatedArtifacts, tags, satisfies, verifies);
    }

    private static RepoFileSummary? BuildFileSummary(string path, string relative)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return null;
        }

        var extension = Path.GetExtension(path);
        var fileType = DetectFileType(path, extension, info.Length);
        var excerpt = string.Empty;

        if (!string.Equals(fileType, "binary", StringComparison.OrdinalIgnoreCase))
        {
            excerpt = ExtractFileExcerpt(path);
        }

        return new RepoFileSummary(
            relative,
            Path.GetFileName(path),
            extension,
            fileType,
            info.Length,
            info.LastWriteTimeUtc,
            excerpt);
    }

    private static bool MatchesQuery(RepoDocSummary doc, string query)
    {
        return doc.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(doc.ArtifactId) && doc.ArtifactId.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(doc.Domain) && doc.Domain.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(doc.Capability) && doc.Capability.Contains(query, StringComparison.OrdinalIgnoreCase))
            || doc.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || doc.Type.Contains(query, StringComparison.OrdinalIgnoreCase)
            || doc.Status.Contains(query, StringComparison.OrdinalIgnoreCase)
            || doc.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
            || doc.Excerpt.Contains(query, StringComparison.OrdinalIgnoreCase)
            || doc.WorkItems.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
            || doc.RelatedArtifacts.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
            || doc.Tags.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
            || doc.Satisfies.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
            || doc.Verifies.Any(item => item.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesQuery(RepoFileSummary file, string query)
    {
        return file.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
            || file.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || file.Extension.Contains(query, StringComparison.OrdinalIgnoreCase)
            || file.FileType.Contains(query, StringComparison.OrdinalIgnoreCase)
            || file.Excerpt.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractExcerpt(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal);
        var paragraphs = normalized
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("# ", StringComparison.Ordinal))
            .ToList();

        if (paragraphs.Count == 0)
        {
            return string.Empty;
        }

        var excerpt = string.Join(" ", paragraphs.Take(3));
        if (excerpt.Length <= 180)
        {
            return excerpt;
        }

        return excerpt[..177] + "...";
    }

    private static string ExtractTitle(string body)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }

        return string.Empty;
    }

    private static string ExtractFileExcerpt(string path)
    {
#pragma warning disable ERP022
        try
        {
            var body = File.ReadAllText(path);
            var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal);
            var lines = normalized
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("# ", StringComparison.Ordinal))
                .ToList();

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var excerpt = string.Join(" ", lines.Take(3));
            if (excerpt.Length <= 180)
            {
                return excerpt;
            }

            return excerpt[..177] + "...";
        }
        catch
        {
            return string.Empty;
        }
#pragma warning restore ERP022
    }

    private static string GetDocSection(string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized switch
        {
            var path when path.StartsWith("overview/", StringComparison.OrdinalIgnoreCase) => "Overview",
            var path when path.StartsWith("runbooks/", StringComparison.OrdinalIgnoreCase) => "Runbooks",
            var path when path.StartsWith("tracking/", StringComparison.OrdinalIgnoreCase) => "Tracking",
            var path when path.StartsWith($"{SpecTraceLayout.RequirementsRoot}/", StringComparison.OrdinalIgnoreCase) => "Requirements",
            var path when path.StartsWith($"{SpecTraceLayout.ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase) => "Architecture",
            var path when path.StartsWith($"{SpecTraceLayout.VerificationRoot}/", StringComparison.OrdinalIgnoreCase) => "Verification",
            var path when path.StartsWith($"{SpecTraceLayout.GeneratedRoot}/", StringComparison.OrdinalIgnoreCase) => "Generated",
            _ => "Repo"
        };
    }

    private static string InferDocType(string relativePath)
    {
        var normalized = NormalizePath(relativePath).ToLowerInvariant();
        if (normalized.StartsWith($"{SpecTraceLayout.ArchitectureRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture";
        }

        if (normalized.StartsWith($"{SpecTraceLayout.RequirementsRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "specification";
        }

        if (normalized.StartsWith($"{SpecTraceLayout.VerificationRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "verification";
        }

        if (normalized.StartsWith($"{SpecTraceLayout.GeneratedRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return "doc";
        }

        if (normalized.StartsWith("runbooks/", StringComparison.OrdinalIgnoreCase))
        {
            return "doc";
        }

        if (normalized.StartsWith("overview/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("tracking/", StringComparison.OrdinalIgnoreCase))
        {
            return "doc";
        }

        return "doc";
    }

    private static bool DocTypeMatchesFilter(string docType, string? typeFilter)
    {
        if (string.IsNullOrWhiteSpace(typeFilter) ||
            string.Equals(typeFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(docType, typeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (string.Equals(typeFilter, "spec", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(docType, "specification", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(typeFilter, "specification", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(docType, "spec", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(typeFilter, "architecture", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(docType, "architecture", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(typeFilter, "verification", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(docType, "verification", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> GetNestedMap(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<string, object?> typed)
        {
            return new Dictionary<string, object?>(typed, StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<object, object> legacy)
        {
            return legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static List<string> GetStringList(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return new List<string>();
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        return new List<string>();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static List<string> FilterWorkItemArtifactIds(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.StartsWith("WI-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static bool IsWorkItemArtifactDoc(string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.Contains("/specs/work-items/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocTemplate(string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.Contains("/specs/templates/", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectFileType(string path, string extension, long sizeBytes)
    {
        if (RepoContentRenderer.IsMarkdownPath(path))
        {
            return "markdown";
        }

        if (IsTextExtension(extension))
        {
            return "text";
        }

        if (sizeBytes > 0 && sizeBytes <= 512 * 1024 && LooksLikeText(path))
        {
            return "text";
        }

        return "binary";
    }

    private static bool IsTextExtension(string extension)
    {
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".toml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".mdx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sh", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sql", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeText(string path)
    {
#pragma warning disable ERP022
        try
        {
            var buffer = new byte[4096];
            using var stream = File.OpenRead(path);
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                return true;
            }

            var controlCharacters = 0;
            for (var i = 0; i < read; i++)
            {
                var current = buffer[i];
                if (current == 0)
                {
                    return false;
                }

                if (current < 0x09 || current is > 0x0D and < 0x20)
                {
                    controlCharacters++;
                }
            }

            return controlCharacters <= read / 10;
        }
        catch
        {
            return false;
        }
#pragma warning restore ERP022
    }

    private string ResolveDocPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(RepoRoot, path);
    }

    private static IEnumerable<string> EnumerateRepoFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;

#pragma warning disable ERP022
            try
            {
                directories = Directory.EnumerateDirectories(current);
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }
#pragma warning restore ERP022

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                if (IsIgnoredDirectory(name))
                {
                    continue;
                }

                stack.Push(directory);
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static bool IsIgnoredDirectory(string name)
    {
        return name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }
}
