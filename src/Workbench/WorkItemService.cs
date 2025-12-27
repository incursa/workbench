using System.Text.RegularExpressions;
using System.Linq;

namespace Workbench;

public static class WorkItemService
{
    public sealed record WorkItemResult(string Id, string Slug, string Path);

    public sealed record WorkItemListResult(IList<WorkItem> Items);

    public static WorkItemResult CreateItem(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? status,
        string? priority,
        string? owner)
    {
        var templatesDir = Path.Combine(repoRoot, config.Paths.TemplatesDir);
        var templatePath = Path.Combine(templatesDir, $"work-item.{type}.md");
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException($"Template not found: {templatePath}");
        }

        var templateContent = File.ReadAllText(templatePath);
        if (!FrontMatter.TryParse(templateContent, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Template front matter error: {error}");
        }

        var id = AllocateId(repoRoot, config, type);
        var slug = Slugify(title);
        var created = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        frontMatter!.Data["id"] = id;
        frontMatter.Data["type"] = type;
        frontMatter.Data["created"] = created;
        frontMatter.Data["title"] = title;
        frontMatter.Data["updated"] = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            frontMatter.Data["status"] = status;
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            frontMatter.Data["priority"] = priority;
        }

        if (owner is not null)
        {
            frontMatter.Data["owner"] = owner;
        }

        var related = GetRelatedMap(frontMatter.Data);
        if (related is not null)
        {
            EnsureList(related, "files");
        }

        frontMatter = NormalizeFrontMatter(frontMatter);

        var fileName = $"{id}-{slug}.md";
        var itemPath = Path.Combine(repoRoot, config.Paths.ItemsDir, fileName);
        if (File.Exists(itemPath))
        {
            throw new InvalidOperationException($"Work item already exists: {itemPath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(itemPath) ?? repoRoot);
        var content = frontMatter.Serialize();
        content = content.Replace("<title>", title).Replace("0000-00-00", created);
        content = content.Replace("BUG-0000", id).Replace("TASK-0000", id).Replace("SPIKE-0000", id);
        File.WriteAllText(itemPath, content);

        return new WorkItemResult(id, slug, itemPath);
    }

    public static WorkItemListResult ListItems(string repoRoot, WorkbenchConfig config, bool includeDone)
    {
        var items = new List<WorkItem>();
        foreach (var path in EnumerateItems(repoRoot, config, includeDone))
        {
            var item = LoadItem(path);
            if (item is not null)
            {
                items.Add(item);
            }
        }
        return new WorkItemListResult(items);
    }

    public static WorkItem? LoadItem(string path)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            return null;
        }

        var data = frontMatter!.Data;
        var id = GetString(data, "id") ?? "";
        var type = GetString(data, "type") ?? "";
        var status = GetString(data, "status") ?? "";
        var title = GetString(data, "title") ?? DeriveTitleFromPath(path, id);
        var priority = GetString(data, "priority");
        var owner = GetString(data, "owner");
        var created = GetString(data, "created") ?? "";
        var updated = GetString(data, "updated");
        var tags = GetStringList(data, "tags");
        var related = GetRelated(data);
        var slug = DeriveSlugFromPath(path, id);

        return new WorkItem(
            id,
            type,
            status,
            title,
            priority,
            owner,
            created,
            updated,
            tags,
            related,
            slug,
            path,
            frontMatter.Body);
    }

    public static WorkItem UpdateStatus(string path, string status, string? note)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        frontMatter!.Data["status"] = status;
        frontMatter.Data["updated"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var body = frontMatter.Body;
        if (!string.IsNullOrWhiteSpace(note))
        {
            body = AppendNote(body, note);
        }
        frontMatter = new FrontMatter(frontMatter.Data, body);
        File.WriteAllText(path, frontMatter.Serialize());
        return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem Close(string path, bool move, WorkbenchConfig config, string repoRoot)
    {
        var updated = UpdateStatus(path, "done", null);
        if (!move)
        {
            return updated;
        }

        var fileName = Path.GetFileName(path);
        var dest = Path.Combine(repoRoot, config.Paths.DoneDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? repoRoot);
        File.Move(path, dest, overwrite: false);
        return LoadItem(dest) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem Move(string path, string destination, string repoRoot)
    {
        var dest = destination;
        if (!Path.IsPathRooted(dest))
        {
            dest = Path.Combine(repoRoot, destination);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? repoRoot);
        File.Move(path, dest, overwrite: false);
        return LoadItem(dest) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem Rename(string path, string newTitle, WorkbenchConfig config, string repoRoot)
    {
        var item = LoadItem(path) ?? throw new InvalidOperationException("Work item not found.");
        var slug = Slugify(newTitle);
        var newFileName = $"{item.Id}-{slug}.md";
        var currentDir = Path.GetDirectoryName(path) ?? repoRoot;
        var newPath = Path.Combine(currentDir, newFileName);

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }
        frontMatter!.Data["title"] = newTitle;
        File.WriteAllText(path, frontMatter.Serialize());

        File.Move(path, newPath, overwrite: false);
        return LoadItem(newPath) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static void AddPrLink(string path, string prUrl)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var prs = EnsureList(related, "prs");
        if (!prs.OfType<string>().Any(link => link.Equals(prUrl, StringComparison.OrdinalIgnoreCase)))
        {
            prs.Add(prUrl);
        }

        File.WriteAllText(path, frontMatter.Serialize());
    }

    public static bool AddRelatedLink(string path, string key, string link, bool apply = true)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var list = EnsureList(related, key);
        if (!list.OfType<string>().Any(entry => entry.Equals(link, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(link);
            if (apply)
            {
                File.WriteAllText(path, frontMatter.Serialize());
            }
            return true;
        }
        return false;
    }

    public static bool RemoveRelatedLink(string path, string key, string link, bool apply = true)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var list = EnsureList(related, key);
        var before = list.Count;
        list.RemoveAll(entry => entry is string s && s.Equals(link, StringComparison.OrdinalIgnoreCase));
        if (list.Count != before)
        {
            if (apply)
            {
                File.WriteAllText(path, frontMatter.Serialize());
            }
            return true;
        }
        return false;
    }

    public static string GetItemPathById(string repoRoot, WorkbenchConfig config, string id)
    {
        foreach (var dir in new[] { config.Paths.ItemsDir, config.Paths.DoneDir })
        {
            var full = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }
            var match = Directory.EnumerateFiles(full, $"{id}-*.md", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }
        throw new FileNotFoundException($"Work item not found: {id}");
    }

    private static FrontMatter NormalizeFrontMatter(FrontMatter frontMatter)
    {
        var normalized = frontMatter.Serialize();
        if (!FrontMatter.TryParse(normalized, out var updated, out _))
        {
            return frontMatter;
        }
        return updated!;
    }

    private static string AllocateId(string repoRoot, WorkbenchConfig config, string type)
    {
        var prefix = config.GetPrefix(type);
        var width = config.Ids.Width;
        var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var max = 0;

        foreach (var dir in new[] { config.Paths.ItemsDir, config.Paths.DoneDir })
        {
            var path = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(path))
            {
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var match = pattern.Match(name);
                if (!match.Success)
                {
                    continue;
                }
                if (int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var value))
                {
                    max = Math.Max(max, value);
                }
            }
        }

        var next = max + 1;
        return $"{prefix}-{next.ToString($"D{width}", CultureInfo.InvariantCulture)}";
    }

    public static string Slugify(string title)
    {
        var lowered = title.ToLowerInvariant();
        var cleaned = Regex.Replace(lowered, @"[^a-z0-9-\s]", "", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var dashed = Regex.Replace(cleaned, @"\s+", "-", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var collapsed = Regex.Replace(dashed, @"-+", "-", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        return collapsed.Trim('-');
    }

    private static IEnumerable<string> EnumerateItems(string repoRoot, WorkbenchConfig config, bool includeDone)
    {
        var dirs = new List<string> { config.Paths.ItemsDir };
        if (includeDone)
        {
            dirs.Add(config.Paths.DoneDir);
        }
        foreach (var dir in dirs)
        {
            var path = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(path))
            {
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }

    private static string DeriveTitleFromPath(string path, string id)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(id))
        {
            return name.Replace("-", " ");
        }
        if (name.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase))
        {
            return name[(id.Length + 1)..].Replace("-", " ");
        }
        return name.Replace("-", " ");
    }

    private static string DeriveSlugFromPath(string path, string id)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase))
        {
            return name[(id.Length + 1)..];
        }
        return name;
    }

    private static string AppendNote(string body, string note)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        var notesIndex = lines.FindIndex(line => line.Trim().Equals("## Notes", StringComparison.OrdinalIgnoreCase));
        if (notesIndex == -1)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }
            lines.Add("## Notes");
            lines.Add(string.Empty);
            lines.Add($"- {note}");
            return string.Join("\n", lines);
        }

        var insertIndex = notesIndex + 1;
        while (insertIndex < lines.Count && string.IsNullOrWhiteSpace(lines[insertIndex]))
        {
            insertIndex++;
        }
        lines.Insert(insertIndex, $"- {note}");
        return string.Join("\n", lines);
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
        if (value is IEnumerable<object> list)
        {
            return list.Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0).ToList();
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

    private static List<object?> EnsureList(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            var empty = new List<object?>();
            data[key] = empty;
            return empty;
        }
        if (value is List<object?> list)
        {
            return list;
        }
        if (value is IEnumerable<object> legacyList)
        {
            var converted = legacyList.Select(item => (object?)item).ToList();
            data[key] = converted;
            return converted;
        }
        var reset = new List<object?>();
        data[key] = reset;
        return reset;
    }

    private static RelatedLinks GetRelated(IDictionary<string, object?> data)
    {
        var related = GetRelatedMap(data);
        if (related is null)
        {
            return new RelatedLinks(new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());
        }

        List<string> Extract(string key)
        {
            if (!related.TryGetValue(key, out var value) || value is null)
            {
                return new List<string>();
            }
            if (value is IEnumerable<object> list)
            {
                return list.Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0).ToList();
            }
            if (value is IEnumerable<object?> nullableList)
            {
                return nullableList.Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0).ToList();
            }
            return new List<string>();
        }

        return new RelatedLinks(
            Extract("specs"),
            Extract("adrs"),
            Extract("files"),
            Extract("prs"),
            Extract("issues"));
    }
}
