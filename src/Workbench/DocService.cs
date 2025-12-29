using System.Collections;
using System.Linq;

namespace Workbench;

public static class DocService
{
    public sealed record DocCreateResult(string Path, string Type, IList<string> WorkItems);

    public sealed record DocSyncResult(
        int DocsUpdated,
        int ItemsUpdated,
        IList<string> MissingDocs,
        IList<string> MissingItems);

    private static readonly HashSet<string> allowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "spec",
        "adr",
        "doc",
        "runbook",
        "guide"
    };

    public static DocCreateResult CreateDoc(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? path,
        IList<string> workItems,
        IList<string> codeRefs,
        bool force)
    {
        if (!allowedTypes.Contains(type))
        {
            throw new InvalidOperationException($"Invalid doc type '{type}'.");
        }

        var docPath = ResolveDocPath(repoRoot, config, type, title, path);
        if (File.Exists(docPath) && !force)
        {
            throw new InvalidOperationException($"Doc already exists: {docPath}");
        }

        var relative = "/" + Path.GetRelativePath(repoRoot, docPath).Replace('\\', '/');
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workbench"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["workItems"] = workItems.Cast<object?>().ToList(),
                ["codeRefs"] = codeRefs.Cast<object?>().ToList()
            }
        };

        var body = BuildBody(type, title);
        var frontMatter = new FrontMatter(data, body);
        Directory.CreateDirectory(Path.GetDirectoryName(docPath) ?? repoRoot);
        File.WriteAllText(docPath, frontMatter.Serialize());

        foreach (var workItemId in workItems)
        {
            var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
            WorkItemService.AddRelatedLink(itemPath, DocTypeToRelatedKey(type), relative);
        }

        return new DocCreateResult(docPath, type, workItems);
    }

    public static async Task<DocSyncResult> SyncLinksAsync(string repoRoot, WorkbenchConfig config, bool includeAllDocs, bool syncIssues, bool includeDone, bool dryRun)
    {
        var itemsUpdated = 0;
        var docsUpdated = 0;
        var missingDocs = new List<string>();
        var missingItems = new List<string>();
        var itemsById = LoadItems(repoRoot, config);
        var referencedDocs = BuildReferencedDocSet(repoRoot, itemsById.Values);

        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        if (!Directory.Exists(docsRoot))
        {
            return new DocSyncResult(0, 0, missingDocs, missingItems);
        }

        foreach (var docPath in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            if (!TryLoadOrCreateFrontMatter(
                    docPath,
                    includeAllDocs,
                    referencedDocs,
                    out var frontMatter,
                    out var createdFrontMatter))
            {
                continue;
            }

            var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var docChanged);
            var docType = GetString(workbench, "type") ?? InferDocType(docPath);
            var workItems = EnsureStringList(workbench, "workItems", out var listChanged);
            var relative = "/" + Path.GetRelativePath(repoRoot, docPath).Replace('\\', '/');

            foreach (var workItemId in workItems)
            {
                if (!itemsById.TryGetValue(workItemId, out var item))
                {
                    missingItems.Add($"{docPath}: {workItemId}");
                    continue;
                }
                if (NeedsBacklink(item, docType, relative) &&
                    WorkItemService.AddRelatedLink(item.Path, DocTypeToRelatedKey(docType), relative, apply: !dryRun))
                {
                    itemsUpdated++;
                }
            }

            if ((createdFrontMatter || docChanged || listChanged) && !dryRun)
            {
                await File.WriteAllTextAsync(docPath, frontMatter!.Serialize()).ConfigureAwait(false);
                docsUpdated++;
            }
        }

        foreach (var item in itemsById.Values)
        {
            if (!includeDone && IsTerminalStatus(item.Status))
            {
                continue;
            }
            docsUpdated += SyncDocLinksForItem(repoRoot, item, missingDocs, dryRun);
        }

        if (syncIssues)
        {
            itemsUpdated += await WorkItemService.SyncIssueLinksAsync(repoRoot, config, itemsById.Values, dryRun).ConfigureAwait(false);
        }

        return new DocSyncResult(docsUpdated, itemsUpdated, missingDocs, missingItems);
    }

    public static bool TryUpdateDocWorkItemLink(
        string repoRoot,
        WorkbenchConfig config,
        string link,
        string workItemId,
        bool add,
        bool apply = true)
    {
        var docPath = ResolveDocPath(repoRoot, link);
        if (!File.Exists(docPath) || !docPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var docsRoot = Path.GetFullPath(Path.Combine(repoRoot, config.Paths.DocsRoot));
        var fullDocPath = Path.GetFullPath(docPath);
        if (!fullDocPath.StartsWith(docsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullDocPath, docsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryLoadOrCreateFrontMatter(
                docPath,
                includeAllDocs: true,
                referencedDocs: null,
                out var frontMatter,
                out var createdFrontMatter))
        {
            return false;
        }

        var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var docChanged);
        var workItems = EnsureStringList(workbench, "workItems", out var listChanged);
        var updated = false;

        if (add)
        {
            if (!workItems.Contains(workItemId, StringComparer.OrdinalIgnoreCase))
            {
                workItems.Add(workItemId);
                listChanged = true;
            }
        }
        else
        {
            var before = workItems.Count;
            workItems.RemoveAll(entry => entry.Equals(workItemId, StringComparison.OrdinalIgnoreCase));
            if (workItems.Count != before)
            {
                listChanged = true;
            }
        }

        if (createdFrontMatter || docChanged || listChanged)
        {
            if (apply)
            {
                File.WriteAllText(docPath, frontMatter!.Serialize());
            }
            updated = true;
        }

        return updated;
    }

    private static int SyncDocLinksForItem(string repoRoot, WorkItem item, List<string> missingDocs, bool dryRun)
    {
        var updates = 0;
        updates += SyncDocList(repoRoot, item, item.Related.Specs, "spec", missingDocs, dryRun);
        updates += SyncDocList(repoRoot, item, item.Related.Adrs, "adr", missingDocs, dryRun);
        return updates;
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "dropped", StringComparison.OrdinalIgnoreCase);
    }

    private static int SyncDocList(
        string repoRoot,
        WorkItem item,
        IList<string> docs,
        string docType,
        IList<string> missingDocs,
        bool dryRun)
    {
        var updated = 0;
        foreach (var link in docs)
        {
            var docPath = ResolveDocPath(repoRoot, link);
            if (!File.Exists(docPath))
            {
                missingDocs.Add($"{item.Id}: {link}");
                continue;
            }
            if (!TryLoadOrCreateFrontMatter(
                    docPath,
                    includeAllDocs: true,
                    referencedDocs: null,
                    out var frontMatter,
                    out var createdFrontMatter))
            {
                continue;
            }

            var workbench = EnsureWorkbench(frontMatter!, docType, out var docChanged);
            var workItems = EnsureStringList(workbench, "workItems", out var listChanged);
            var currentType = GetString(workbench, "type") ?? docType;
            if (!currentType.Equals(docType, StringComparison.OrdinalIgnoreCase))
            {
                workbench["type"] = docType;
                docChanged = true;
            }

            if (!workItems.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            {
                workItems.Add(item.Id);
                listChanged = true;
            }

            if ((createdFrontMatter || docChanged || listChanged) && !dryRun)
            {
                File.WriteAllText(docPath, frontMatter!.Serialize());
                updated++;
            }
        }

        return updated;
    }

    private static bool NeedsBacklink(WorkItem item, string docType, string docPath)
    {
        var key = DocTypeToRelatedKey(docType);
        var list = key switch
        {
            "specs" => item.Related.Specs,
            "adrs" => item.Related.Adrs,
            "files" => item.Related.Files,
            _ => item.Related.Specs
        };
        return !list.Any(link => link.Equals(docPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string DocTypeToRelatedKey(string docType)
    {
        if (docType.Equals("spec", StringComparison.OrdinalIgnoreCase))
        {
            return "specs";
        }
        if (docType.Equals("adr", StringComparison.OrdinalIgnoreCase))
        {
            return "adrs";
        }
        return "files";
    }

    private static Dictionary<string, WorkItem> LoadItems(string repoRoot, WorkbenchConfig config)
    {
        var items = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var includeDone in new[] { false, true })
        {
            var list = WorkItemService.ListItems(repoRoot, config, includeDone);
            foreach (var item in list.Items)
            {
                items[item.Id] = item;
            }
        }
        return items;
    }

    private static bool TryLoadOrCreateFrontMatter(
        string path,
        bool includeAllDocs,
        HashSet<string>? referencedDocs,
        out FrontMatter? frontMatter,
        out bool created)
    {
        frontMatter = null;
        created = false;
        var content = File.ReadAllText(path);
        if (FrontMatter.TryParse(content, out frontMatter, out _))
        {
            return true;
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
            trimmed.StartsWith("---\r\n", StringComparison.Ordinal))
        {
            return false;
        }

        if (!includeAllDocs)
        {
            var relative = "/" + path.Replace('\\', '/');
            if (referencedDocs is null || !referencedDocs.Contains(relative))
            {
                return false;
            }
        }

        frontMatter = new FrontMatter(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            content.TrimStart('\r', '\n'));
        created = true;
        return true;
    }

    private static Dictionary<string, object?> EnsureWorkbench(FrontMatter frontMatter, string docType, out bool changed)
    {
        changed = false;
        var workbench = new Dictionary<string, object?>(StringComparer.Ordinal);
        var data = frontMatter.Data;
        if (!data.TryGetValue("workbench", out var workbenchObj) || workbenchObj is null)
        {
            data["workbench"] = workbench;
            changed = true;
        }
        else if (workbenchObj is Dictionary<string, object?> typed)
        {
            workbench = typed;
        }
        else if (workbenchObj is Dictionary<object, object> legacy)
        {
            workbench = legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            data["workbench"] = workbench;
            changed = true;
        }

        var resolvedType = docType;
        var existingType = GetString(workbench, "type");
        if (string.IsNullOrWhiteSpace(existingType))
        {
            workbench["type"] = resolvedType;
            changed = true;
        }

        return workbench;
    }

    private static List<string> EnsureStringList(Dictionary<string, object?> data, string key, out bool changed)
    {
        changed = false;
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            var list = new List<string>();
            data[key] = list;
            changed = true;
            return list;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var list = enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
            data[key] = list;
            return list;
        }
        var reset = new List<string>();
        data[key] = reset;
        changed = true;
        return reset;
    }

    private static string? GetString(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value.ToString();
    }

    private static string ResolveDocPath(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var target = Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
            if (!target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                target += ".md";
            }
            return target;
        }

        var slug = WorkItemService.Slugify(title);
        var dir = type.ToLowerInvariant() switch
        {
            "spec" => Path.Combine(repoRoot, config.Paths.DocsRoot, "10-product"),
            "adr" => Path.Combine(repoRoot, config.Paths.DocsRoot, "40-decisions"),
            "doc" => Path.Combine(repoRoot, config.Paths.DocsRoot, "00-overview"),
            "runbook" => Path.Combine(repoRoot, config.Paths.DocsRoot, "50-runbooks"),
            "guide" => Path.Combine(repoRoot, config.Paths.DocsRoot, "20-architecture"),
            _ => Path.Combine(repoRoot, config.Paths.DocsRoot)
        };
        if (type.Equals("adr", StringComparison.OrdinalIgnoreCase))
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return Path.Combine(dir, $"{date}-{slug}.md");
        }
        return Path.Combine(dir, $"{slug}.md");
    }

    private static string ResolveDocPath(string repoRoot, string link)
    {
        var trimmed = link.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }
        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return Path.Combine(repoRoot, trimmed.TrimStart('/'));
        }
        return Path.Combine(repoRoot, trimmed);
    }

    private static HashSet<string> BuildReferencedDocSet(string repoRoot, IEnumerable<WorkItem> items)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#pragma warning disable S3267
        foreach (var item in items)
#pragma warning restore S3267
        {
            AddLinks(set, repoRoot, item.Related.Specs);
            AddLinks(set, repoRoot, item.Related.Adrs);
            AddLinks(set, repoRoot, item.Related.Files);
        }
        return set;
    }

    private static void AddLinks(HashSet<string> set, string repoRoot, IEnumerable<string> links)
    {
        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }
            var full = ResolveDocPath(repoRoot, link);
            var normalized = "/" + full.Replace('\\', '/');
            set.Add(normalized);
        }
    }

    private static string InferDocType(string docPath)
    {
        var normalized = docPath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("/10-product/"))
        {
            return "spec";
        }
        if (normalized.Contains("/40-decisions/"))
        {
            return "adr";
        }
        if (normalized.Contains("/50-runbooks/"))
        {
            return "runbook";
        }
        if (normalized.Contains("/20-architecture/"))
        {
            return "guide";
        }
        if (normalized.Contains("/00-overview/"))
        {
            return "doc";
        }
        return "doc";
    }

    private static string BuildBody(string type, string title)
    {
        var header = $"# {title}\n\n";
        if (type.Equals("adr", StringComparison.OrdinalIgnoreCase))
        {
            return header +
                   "## Status\n\n" +
                   "## Context\n\n" +
                   "## Decision\n\n" +
                   "## Consequences\n";
        }
        if (type.Equals("spec", StringComparison.OrdinalIgnoreCase))
        {
            return header +
                   "## Summary\n\n" +
                   "## Goals\n\n" +
                   "## Non-goals\n\n" +
                   "## Requirements\n\n";
        }
        if (type.Equals("runbook", StringComparison.OrdinalIgnoreCase))
        {
            return header +
                   "## Purpose\n\n" +
                   "## Steps\n\n" +
                   "## Rollback\n";
        }
        return header + "## Notes\n";
    }
}
