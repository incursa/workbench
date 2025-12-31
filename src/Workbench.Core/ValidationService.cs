using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;

namespace Workbench;

public static class ValidationService
{
    public static ValidationResult ValidateRepo(string repoRoot, WorkbenchConfig config, ValidationOptions? options = null)
    {
        options ??= new ValidationOptions();
        var result = new ValidationResult();
        var configErrors = SchemaValidationService.ValidateConfig(repoRoot);
        foreach (var error in configErrors)
        {
            result.Errors.Add(error);
        }
        var items = CollectWorkItems(repoRoot, config);
        result.WorkItemCount = items.Count;
        ValidateItems(repoRoot, items, config, result);
        var itemIndex = LoadItemIndex(items);
        ValidateDocs(repoRoot, config, itemIndex, result, options);
        result.MarkdownFileCount = ValidateMarkdownLinks(repoRoot, config, result, options);
        return result;
    }

    private static List<WorkItemRecord> CollectWorkItems(string repoRoot, WorkbenchConfig config)
    {
        var items = new List<WorkItemRecord>();
        foreach (var dir in new[] { config.Paths.ItemsDir, config.Paths.DoneDir })
        {
            var full = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                items.Add(new WorkItemRecord(file));
            }
        }
        return items;
    }

    private static void ValidateItems(string repoRoot, List<WorkItemRecord> items, WorkbenchConfig config, ValidationResult result)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idPattern = new Regex($"^[A-Z]+-\\d{{{config.Ids.Width}}}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bug", "task", "spike" };
        var allowedStatus = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "draft", "ready", "in-progress", "blocked", "done", "dropped"
        };
        var allowedPriority = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "low", "medium", "high", "critical"
        };

#pragma warning disable S3267
        foreach (var item in items)
#pragma warning restore S3267
        {
            var content = File.ReadAllText(item.Path);
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                result.Errors.Add($"{item.Path}: {error}");
                continue;
            }

            var data = frontMatter!.Data;
            var schemaErrors = SchemaValidationService.ValidateFrontMatter(repoRoot, item.Path, data);
            foreach (var schemaError in schemaErrors)
            {
                result.Errors.Add(schemaError);
            }
            var id = GetString(data, "id");
            var type = GetString(data, "type");
            var status = GetString(data, "status");
            var created = GetString(data, "created");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) ||
                string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(created))
            {
                result.Errors.Add($"{item.Path}: missing required front matter fields.");
            }

            if (!string.IsNullOrWhiteSpace(id) && !idPattern.IsMatch(id))
            {
                result.Errors.Add($"{item.Path}: invalid id format.");
            }

            if (!string.IsNullOrWhiteSpace(type) && !allowedTypes.Contains(type))
            {
                result.Errors.Add($"{item.Path}: invalid type '{type}'.");
            }

            if (!string.IsNullOrWhiteSpace(status) && !allowedStatus.Contains(status))
            {
                result.Errors.Add($"{item.Path}: invalid status '{status}'.");
            }

            var priority = GetString(data, "priority");
            if (!string.IsNullOrWhiteSpace(priority) && !allowedPriority.Contains(priority))
            {
                result.Errors.Add($"{item.Path}: invalid priority '{priority}'.");
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                if (!seenIds.Add(id))
                {
                    result.Errors.Add($"{item.Path}: duplicate ID '{id}'.");
                }

                var fileName = Path.GetFileNameWithoutExtension(item.Path);
                if (!fileName.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"{item.Path}: filename does not match ID prefix.");
                }
            }

            ValidateRelated(data, item.Path, id, result);
        }
    }

    private static Dictionary<string, WorkItem> LoadItemIndex(List<WorkItemRecord> items)
    {
        var index = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in items)
        {
            var loaded = WorkItemService.LoadItem(record.Path);
            if (loaded is null || string.IsNullOrWhiteSpace(loaded.Id))
            {
                continue;
            }
            index[loaded.Id] = loaded;
        }
        return index;
    }

    private static void ValidateDocs(
        string repoRoot,
        WorkbenchConfig config,
        Dictionary<string, WorkItem> itemsById,
        ValidationResult result,
        ValidationOptions options)
    {
        var docExcludePrefixes = NormalizePrefixes(config.Validation?.DocExclude);
        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        if (!Directory.Exists(docsRoot))
        {
            return;
        }

        var docSchemaPath = Path.Combine(repoRoot, "docs", "30-contracts", "doc.schema.json");
        var docSchemaExists = File.Exists(docSchemaPath);
        if (!docSchemaExists && !options.SkipDocSchema)
        {
            result.Errors.Add($"doc schema not found at {docSchemaPath}");
        }

        foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var repoRelative = NormalizeRepoRelative(repoRoot, file);
            if (docExcludePrefixes.Count > 0 &&
                docExcludePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (!LooksLikeFrontMatter(content))
            {
                continue;
            }
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                result.Errors.Add($"{file}: {error}");
                continue;
            }

            var data = frontMatter!.Data;
            if (!TryGetWorkbenchSection(data, out var workbench))
            {
                continue;
            }

            if (docSchemaExists && !options.SkipDocSchema)
            {
                var schemaErrors = SchemaValidationService.ValidateDocFrontMatter(repoRoot, file, data);
                foreach (var schemaError in schemaErrors)
                {
                    result.Errors.Add(schemaError);
                }
            }

            var docType = GetString(workbench, "type") ?? "doc";
            var workItems = GetStringList(workbench, "workItems");
            var codeRefs = GetStringList(workbench, "codeRefs");

            var repoRelativePath = string.Concat(
                Path.AltDirectorySeparatorChar,
                Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));

            foreach (var workItemId in workItems)
            {
                if (!itemsById.TryGetValue(workItemId, out var item))
                {
                    result.Errors.Add($"{file}: unknown work item '{workItemId}'.");
                    continue;
                }

                if (!HasDocBacklink(item, repoRelativePath, docType))
                {
                    result.Errors.Add($"{file}: work item '{workItemId}' missing backlink to '{repoRelativePath}'.");
                }
            }

            foreach (var codeRef in codeRefs)
            {
                if (!ValidateCodeRef(repoRoot, codeRef, out var errorMessage))
                {
                    result.Errors.Add($"{file}: {errorMessage}");
                }
            }
        }
    }

    private static void ValidateRelated(
        IDictionary<string, object?> data,
        string itemPath,
        string? id,
        ValidationResult result)
    {
        var related = GetRelatedMap(data);
        if (related is null)
        {
            result.Errors.Add($"{itemPath}: missing related section.");
            return;
        }

        ValidateRelatedPaths(itemPath, "specs", related, result);
        ValidateRelatedPaths(itemPath, "adrs", related, result);
        ValidateRelatedFiles(itemPath, id, related, result);
    }

    private static void ValidateRelatedPaths(
        string itemPath,
        string key,
        Dictionary<string, object?> related,
        ValidationResult result)
    {
        if (!related.TryGetValue(key, out var listObj) || listObj is null)
        {
            result.Errors.Add($"{itemPath}: related.{key} missing or invalid.");
            return;
        }

        foreach (var entry in EnumerateList(listObj))
        {
            if (entry is not string path || string.IsNullOrWhiteSpace(path))
            {
                result.Errors.Add($"{itemPath}: related.{key} entry is invalid.");
                continue;
            }
            var resolved = ResolvePath(itemPath, path);
            if (resolved is null || !File.Exists(resolved))
            {
                result.Errors.Add($"{itemPath}: related.{key} missing file '{path}'.");
            }
        }
    }

    private static void ValidateRelatedFiles(
        string itemPath,
        string? id,
        Dictionary<string, object?> related,
        ValidationResult result)
    {
        if (!related.TryGetValue("files", out var listObj) || listObj is null)
        {
            result.Errors.Add($"{itemPath}: related.files missing or invalid.");
            return;
        }

        foreach (var entry in EnumerateList(listObj))
        {
            if (entry is not string path || string.IsNullOrWhiteSpace(path))
            {
                result.Errors.Add($"{itemPath}: related.files entry is invalid.");
                continue;
            }
            var resolved = ResolvePath(itemPath, path);
            if (resolved is null || !File.Exists(resolved))
            {
                result.Errors.Add($"{itemPath}: related.files missing file '{path}'.");
                continue;
            }
            if (!string.IsNullOrWhiteSpace(id))
            {
                var content = File.ReadAllText(resolved);
                if (!content.Contains(id, StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"{itemPath}: related.files target missing backlink '{id}'.");
                }
            }
        }
    }

    private static string? ResolvePath(string itemPath, string link)
    {
        var repoRoot = Repository.FindRepoRoot(Path.GetDirectoryName(itemPath) ?? ".");
        if (repoRoot is null)
        {
            return null;
        }
        if (link.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(repoRoot, link.TrimStart('/'));
        }
        var baseDir = Path.GetDirectoryName(itemPath) ?? repoRoot;
        return Path.GetFullPath(Path.Combine(baseDir, link));
    }

    private static int ValidateMarkdownLinks(string repoRoot, WorkbenchConfig config, ValidationResult result, ValidationOptions options)
    {
        var includePrefixes = NormalizePrefixes(options.LinkInclude);
        var excludePrefixes = NormalizePrefixes(options.LinkExclude);
        var configExcludes = NormalizePrefixes(config.Validation?.LinkExclude);
        if (configExcludes.Count > 0)
        {
            excludePrefixes = excludePrefixes
                .Concat(configExcludes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        var count = 0;
        foreach (var file in EnumerateMarkdownFiles(repoRoot))
        {
            var repoRelative = NormalizeRepoRelative(repoRoot, file);
            if (!ShouldValidatePath(repoRelative, includePrefixes, excludePrefixes))
            {
                continue;
            }
            count++;
            var content = File.ReadAllText(file);
            foreach (var link in ExtractMarkdownLinks(content))
            {
                var target = link;
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (target.Contains("{{", StringComparison.Ordinal) ||
                    target.Contains("}}", StringComparison.Ordinal) ||
                    target.StartsWith("#", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                target = target.Split('#')[0].Split('?')[0];
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                string resolved;
                if (target.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = Path.Combine(repoRoot, target.TrimStart('/'));
                }
                else
                {
                    var baseDir = Path.GetDirectoryName(file) ?? repoRoot;
                    resolved = Path.GetFullPath(Path.Combine(baseDir, target));
                }

                if (!File.Exists(resolved))
                {
                    result.Errors.Add($"{file}: broken local link '{link}'.");
                }
            }
        }
        return count;
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string repoRoot)
    {
        var stack = new Stack<string>();
        stack.Push(repoRoot);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var dir in Directory.EnumerateDirectories(current))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                stack.Push(dir);
            }

            foreach (var file in Directory.EnumerateFiles(current, "*.md"))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> ExtractMarkdownLinks(string content)
    {
        var matches = Regex.Matches(
            content,
            @"\[[^\]]*\]\((?<link>[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
#pragma warning disable S3267
        foreach (Match match in matches)
#pragma warning restore S3267
        {
            var linkGroup = match.Groups["link"];
            if (linkGroup.Success)
            {
                yield return linkGroup.Value.Trim();
            }
        }
    }

    private static string NormalizeRepoRelative(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        return relative.TrimStart('/');
    }

    private static List<string> NormalizePrefixes(IList<string>? prefixes)
    {
        if (prefixes is null || prefixes.Count == 0)
        {
            return new List<string>();
        }

        return prefixes
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim().TrimStart('/').Replace('\\', '/').TrimEnd('/'))
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldValidatePath(string repoRelative, List<string> includePrefixes, List<string> excludePrefixes)
    {
        if (excludePrefixes.Count > 0 &&
            excludePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (includePrefixes.Count == 0)
        {
            return true;
        }

        return includePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value.ToString();
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

    private static Dictionary<string, object?>? GetRelatedMap(IDictionary<string, object?> data)
    {
        if (!data.TryGetValue("related", out var relatedObj) || relatedObj is null)
        {
            return null;
        }
        if (relatedObj is Dictionary<string, object?> typed)
        {
            return typed;
        }
        if (relatedObj is Dictionary<object, object> legacy)
        {
            return legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }
        return null;
    }

    private static bool TryGetWorkbenchSection(IDictionary<string, object?> data, out Dictionary<string, object?> workbench)
    {
        workbench = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!data.TryGetValue("workbench", out var workbenchObj) || workbenchObj is null)
        {
            return false;
        }
        if (workbenchObj is Dictionary<string, object?> typed)
        {
            workbench = typed;
            return true;
        }
        if (workbenchObj is Dictionary<object, object> legacy)
        {
            workbench = legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            return true;
        }
        return false;
    }

    private static IEnumerable<object?> EnumerateList(object listObj)
    {
        if (listObj is string)
        {
            return Array.Empty<object?>();
        }
        if (listObj is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>();
        }
        return Array.Empty<object?>();
    }

    private static bool LooksLikeFrontMatter(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
               trimmed.StartsWith("---\r\n", StringComparison.Ordinal);
    }

    private static bool HasDocBacklink(WorkItem item, string docPath, string docType)
    {
        if (docType.Equals("spec", StringComparison.OrdinalIgnoreCase))
        {
            return item.Related.Specs.Any(link => PathsMatch(link, docPath));
        }
        if (docType.Equals("adr", StringComparison.OrdinalIgnoreCase))
        {
            return item.Related.Adrs.Any(link => PathsMatch(link, docPath));
        }

        return item.Related.Specs.Any(link => PathsMatch(link, docPath)) ||
               item.Related.Adrs.Any(link => PathsMatch(link, docPath)) ||
               item.Related.Files.Any(link => PathsMatch(link, docPath));
    }

    private static bool PathsMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateCodeRef(string repoRoot, string codeRef, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(codeRef))
        {
            error = "codeRefs entry is empty.";
            return false;
        }

        var parts = codeRef.Split('#', 2);
        var pathPart = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(pathPart))
        {
            error = $"codeRefs entry '{codeRef}' is missing a path.";
            return false;
        }

        var resolved = pathPart.StartsWith("/", StringComparison.Ordinal)
            ? Path.Combine(repoRoot, pathPart.TrimStart('/'))
            : Path.Combine(repoRoot, pathPart);

        if (!File.Exists(resolved))
        {
            error = $"codeRefs entry '{codeRef}' points to missing file '{pathPart}'.";
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        var anchor = parts[1].Trim();
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return true;
        }

        var match = Regex.Match(anchor, @"^L(?<start>\d+)(C(?<colStart>\d+))?(?:-L(?<end>\d+)(C(?<colEnd>\d+))?)?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            error = $"codeRefs entry '{codeRef}' has an invalid anchor.";
            return false;
        }

        var start = int.Parse(match.Groups["start"].Value, CultureInfo.InvariantCulture);
        var end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value, CultureInfo.InvariantCulture) : start;
        if (start <= 0 || end <= 0 || end < start)
        {
            error = $"codeRefs entry '{codeRef}' has invalid line numbers.";
            return false;
        }

        return true;
    }

    private sealed record WorkItemRecord(string Path);
}
