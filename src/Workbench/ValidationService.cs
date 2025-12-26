using System.Text.RegularExpressions;

namespace Workbench;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public int WorkItemCount { get; set; }
    public int MarkdownFileCount { get; set; }
}

public static class ValidationService
{
    public static ValidationResult ValidateRepo(string repoRoot, WorkbenchConfig config)
    {
        var result = new ValidationResult();
        var configErrors = SchemaValidationService.ValidateConfig(repoRoot);
        foreach (var error in configErrors)
        {
            result.Errors.Add(error);
        }
        var items = CollectWorkItems(repoRoot, config);
        result.WorkItemCount = items.Count;
        ValidateItems(repoRoot, items, config, result);
        result.MarkdownFileCount = ValidateMarkdownLinks(repoRoot, result);
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
                items.Add(new WorkItemRecord(file));
            }
        }
        return items;
    }

    private static void ValidateItems(string repoRoot, List<WorkItemRecord> items, WorkbenchConfig config, ValidationResult result)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idPattern = new Regex($"^[A-Z]+-\\d{{{config.Ids.Width}}}$");
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bug", "task", "spike" };
        var allowedStatus = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "draft", "ready", "in-progress", "blocked", "done", "dropped"
        };
        var allowedPriority = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "low", "medium", "high", "critical"
        };

        foreach (var item in items)
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

    private static void ValidateRelated(
        Dictionary<string, object?> data,
        string itemPath,
        string? id,
        ValidationResult result)
    {
        if (!data.TryGetValue("related", out var relatedObj) || relatedObj is not Dictionary<object, object> related)
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
        Dictionary<object, object> related,
        ValidationResult result)
    {
        if (!related.TryGetValue(key, out var listObj) || listObj is not IEnumerable<object> list)
        {
            result.Errors.Add($"{itemPath}: related.{key} missing or invalid.");
            return;
        }

        foreach (var entry in list)
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
        Dictionary<object, object> related,
        ValidationResult result)
    {
        if (!related.TryGetValue("files", out var listObj) || listObj is not IEnumerable<object> list)
        {
            result.Errors.Add($"{itemPath}: related.files missing or invalid.");
            return;
        }

        foreach (var entry in list)
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
        if (link.StartsWith("/"))
        {
            return Path.Combine(repoRoot, link.TrimStart('/'));
        }
        var baseDir = Path.GetDirectoryName(itemPath) ?? repoRoot;
        return Path.GetFullPath(Path.Combine(baseDir, link));
    }

    private static int ValidateMarkdownLinks(string repoRoot, ValidationResult result)
    {
        var count = 0;
        foreach (var file in EnumerateMarkdownFiles(repoRoot))
        {
            count++;
            var content = File.ReadAllText(file);
            foreach (var link in ExtractMarkdownLinks(content))
            {
                var target = link;
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (target.StartsWith("#") ||
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
                if (target.StartsWith("/"))
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
        var matches = Regex.Matches(content, @"\[[^\]]*\]\(([^)]+)\)");
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                yield return match.Groups[1].Value.Trim();
            }
        }
    }

    private static string? GetString(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value.ToString();
    }

    private sealed record WorkItemRecord(string Path);
}
