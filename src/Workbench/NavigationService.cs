using System.Collections;
using System.Text;

namespace Workbench;

public static class NavigationService
{
    public sealed record NavigationSyncResult(
        int DocsUpdated,
        int ItemsUpdated,
        int IndexFilesUpdated,
        IList<string> MissingDocs,
        IList<string> MissingItems,
        IList<string> Warnings);

    private sealed record DocEntry(
        string Title,
        string Type,
        string Status,
        string RepoRelativePath,
        string Section,
        IList<string> WorkItems,
        string? GithubLink);

    private sealed record WorkItemEntry(
        WorkItem Item,
        string RepoRelativePath,
        string? GithubLink);

    public static NavigationSyncResult SyncNavigation(
        string repoRoot,
        WorkbenchConfig config,
        bool includeDone,
        bool syncIssues,
        bool dryRun)
    {
        var docSync = DocService.SyncLinks(repoRoot, config, includeAllDocs: true, syncIssues, includeDone, dryRun);
        var normalizedItems = WorkItemService.NormalizeRelatedLinks(repoRoot, config, includeDone, dryRun);
        var warnings = new List<string>();
        var docEntries = LoadDocEntries(repoRoot, config, warnings);
        var docReadmePath = Path.Combine(repoRoot, config.Paths.DocsRoot, "README.md");
        var workReadmePath = Path.Combine(repoRoot, config.Paths.WorkRoot, "README.md");

        var docIndex = BuildDocsIndex(repoRoot, config, docReadmePath, docEntries);
        var workIndex = BuildWorkIndex(repoRoot, config, workReadmePath, docEntries, includeDone);

        var indexUpdated = 0;
        indexUpdated += UpdateIndexSection(docReadmePath, "workbench:docs-index", docIndex, dryRun);
        indexUpdated += UpdateIndexSection(workReadmePath, "workbench:work-index", workIndex, dryRun);

        return new NavigationSyncResult(
            docSync.DocsUpdated,
            docSync.ItemsUpdated + normalizedItems,
            indexUpdated,
            docSync.MissingDocs,
            docSync.MissingItems,
            warnings);
    }

    private static List<DocEntry> LoadDocEntries(string repoRoot, WorkbenchConfig config, List<string> warnings)
    {
        var entries = new List<DocEntry>();
        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        if (!Directory.Exists(docsRoot))
        {
            return entries;
        }

        foreach (var path in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
        {
            var relative = NormalizePath(Path.GetRelativePath(repoRoot, path));
            if (IsDocsIndexFile(config, relative) || IsDocTemplate(config, relative))
            {
                continue;
            }

            var content = File.ReadAllText(path);
            var body = content;
            Dictionary<string, object?>? data = null;
            if (HasFrontMatter(content))
            {
                if (FrontMatter.TryParse(content, out var frontMatter, out var error))
                {
                    data = new Dictionary<string, object?>(frontMatter!.Data, StringComparer.OrdinalIgnoreCase);
                    body = frontMatter.Body;
                }
                else
                {
                    warnings.Add($"{relative}: {error}");
                }
            }

            data ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var workbench = GetNestedMap(data, "workbench");
            var type = GetString(workbench, "type") ?? InferDocType(relative, config);
            var status = GetString(data, "status") ?? "unknown";
            var title = ExtractTitle(body) ?? Path.GetFileNameWithoutExtension(path);
            var workItems = GetStringList(workbench, "workItems");
            var section = GetDocSection(relative, config);
            var githubLink = BuildGithubFileLink(config, relative);

            entries.Add(new DocEntry(
                title,
                type,
                status,
                relative,
                section,
                workItems,
                githubLink));
        }

        return entries;
    }

    private static string BuildDocsIndex(
        string repoRoot,
        WorkbenchConfig config,
        string docsReadmePath,
        List<DocEntry> docs)
    {
        if (docs.Count == 0)
        {
            return "_No docs found._";
        }

        var itemsById = BuildWorkItemIndex(repoRoot, config);
        var readmeDir = Path.GetDirectoryName(docsReadmePath) ?? repoRoot;
        var grouped = docs
            .OrderBy(entry => entry.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .GroupBy(entry => entry.Section, StringComparer.OrdinalIgnoreCase);

        var builder = new StringBuilder();
        foreach (var group in grouped)
        {
            builder.AppendLine($"### {group.Key}");
            builder.AppendLine("| Doc | Type | Status | GitHub | Work Items |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var entry in group)
            {
                var relativeLink = NormalizePath(Path.GetRelativePath(readmeDir, Path.Combine(repoRoot, entry.RepoRelativePath)));
                var docLink = BuildMarkdownLink(entry.Title, relativeLink);
                var githubLink = entry.GithubLink is null ? "-" : BuildMarkdownLink("view", entry.GithubLink);
                var workItems = FormatWorkItemLinks(entry.WorkItems, itemsById, readmeDir);

                builder.AppendLine($"| {docLink} | {EscapeTableCell(entry.Type)} | {EscapeTableCell(entry.Status)} | {githubLink} | {workItems} |");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildWorkIndex(
        string repoRoot,
        WorkbenchConfig config,
        string workReadmePath,
        List<DocEntry> docs,
        bool includeDone)
    {
        var builder = new StringBuilder();
        var activeItems = LoadWorkItemEntries(repoRoot, config.Paths.ItemsDir, config);
        AppendWorkItemTable(builder, "Active items", activeItems, workReadmePath, docs, repoRoot, config);

        if (includeDone)
        {
            var doneItems = LoadWorkItemEntries(repoRoot, config.Paths.DoneDir, config);
            builder.AppendLine();
            AppendWorkItemTable(builder, "Done items", doneItems, workReadmePath, docs, repoRoot, config);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendWorkItemTable(
        StringBuilder builder,
        string title,
        List<WorkItemEntry> items,
        string workReadmePath,
        List<DocEntry> docs,
        string repoRoot,
        WorkbenchConfig config)
    {
        builder.AppendLine($"### {title}");
        if (items.Count == 0)
        {
            builder.AppendLine("_None._");
            return;
        }

        var readmeDir = Path.GetDirectoryName(workReadmePath) ?? repoRoot;
        var docsByPath = docs.ToDictionary(entry => entry.RepoRelativePath, StringComparer.OrdinalIgnoreCase);
        var defaultRepo = TryResolveRepo(repoRoot, config);

        builder.AppendLine("| Item | Status | GitHub | Issues | Related |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var entry in items
                     .OrderBy(item => GetStatusRank(item.Item.Status))
                     .ThenBy(item => item.Item.Id, StringComparer.OrdinalIgnoreCase))
        {
            var relativeLink = NormalizePath(Path.GetRelativePath(readmeDir, Path.Combine(repoRoot, entry.RepoRelativePath)));
            var itemTitle = $"{entry.Item.Id} - {entry.Item.Title}";
            var itemLink = BuildMarkdownLink(itemTitle, relativeLink);
            var githubLink = entry.GithubLink is null ? "-" : BuildMarkdownLink("view", entry.GithubLink);
            var issueLinks = FormatIssueLinks(entry.Item.Related.Issues, defaultRepo);
            var relatedLinks = FormatRelatedLinks(entry.Item, docsByPath, readmeDir, repoRoot);

            builder.AppendLine($"| {itemLink} | {EscapeTableCell(entry.Item.Status)} | {githubLink} | {issueLinks} | {relatedLinks} |");
        }
    }

    private static List<WorkItemEntry> LoadWorkItemEntries(string repoRoot, string dir, WorkbenchConfig config)
    {
        var items = new List<WorkItemEntry>();
        var path = Path.Combine(repoRoot, dir);
        if (!Directory.Exists(path))
        {
            return items;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
        {
            var item = WorkItemService.LoadItem(file);
            if (item is null)
            {
                continue;
            }

            var relative = NormalizePath(Path.GetRelativePath(repoRoot, file));
            var githubLink = BuildGithubFileLink(config, relative);
            items.Add(new WorkItemEntry(item, relative, githubLink));
        }

        return items;
    }

    private static Dictionary<string, WorkItem> BuildWorkItemIndex(string repoRoot, WorkbenchConfig config)
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

    private static string FormatWorkItemLinks(
        IList<string> workItems,
        Dictionary<string, WorkItem> itemsById,
        string readmeDir)
    {
        if (workItems.Count == 0)
        {
            return "-";
        }

        var links = new List<string>();
        foreach (var workItemId in workItems)
        {
            if (itemsById.TryGetValue(workItemId, out var item))
            {
                var relative = NormalizePath(Path.GetRelativePath(readmeDir, item.Path));
                links.Add(BuildMarkdownLink(item.Id, relative));
            }
            else
            {
                links.Add(EscapeTableCell(workItemId));
            }
        }

        return string.Join(", ", links);
    }

    private static string FormatIssueLinks(IList<string> issues, GithubRepoRef? defaultRepo)
    {
        if (issues.Count == 0)
        {
            return "-";
        }

        var links = new List<string>();
        foreach (var issue in issues)
        {
            var trimmed = issue?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (defaultRepo is not null)
            {
                try
                {
                    var issueRef = GithubService.ParseIssueReference(trimmed, defaultRepo);
                    var label = issueRef.Repo.Owner.Equals(defaultRepo.Owner, StringComparison.OrdinalIgnoreCase) &&
                        issueRef.Repo.Repo.Equals(defaultRepo.Repo, StringComparison.OrdinalIgnoreCase)
                        ? $"#{issueRef.Number}"
                        : $"{issueRef.Repo.Owner}/{issueRef.Repo.Repo}#{issueRef.Number}";
                    var url = $"https://{issueRef.Repo.Host}/{issueRef.Repo.Owner}/{issueRef.Repo.Repo}/issues/{issueRef.Number}";
                    links.Add(BuildMarkdownLink(label, url));
                    continue;
                }
                catch (InvalidOperationException)
                {
                    // Fall back to raw issue formatting when parsing fails.
                }
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var segment = uri.Segments.Length == 0 ? trimmed : uri.Segments[^1].Trim('/');
                links.Add(BuildMarkdownLink(segment, trimmed));
            }
            else
            {
                links.Add(EscapeTableCell(trimmed));
            }
        }

        return links.Count == 0 ? "-" : string.Join(", ", links);
    }

    private static string FormatRelatedLinks(
        WorkItem item,
        Dictionary<string, DocEntry> docsByPath,
        string readmeDir,
        string repoRoot)
    {
        var links = new List<string>();
        foreach (var entry in item.Related.Specs.Concat(item.Related.Adrs).Concat(item.Related.Files))
        {
            var normalized = NormalizeLinkPath(repoRoot, entry);
            if (normalized is null)
            {
                continue;
            }

            var targetPath = Path.Combine(repoRoot, normalized);
            if (!File.Exists(targetPath))
            {
                links.Add(EscapeTableCell(entry));
                continue;
            }

            var relative = NormalizePath(Path.GetRelativePath(readmeDir, targetPath));
            var title = docsByPath.TryGetValue(NormalizePath(normalized), out var doc)
                ? doc.Title
                : Path.GetFileNameWithoutExtension(normalized);
            links.Add(BuildMarkdownLink(title, relative));
        }

        return links.Count == 0 ? "-" : string.Join(", ", links);
    }

    private static string? NormalizeLinkPath(string repoRoot, string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var trimmed = link.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = trimmed.TrimStart('/');
        }

        var combined = Path.IsPathRooted(trimmed)
            ? trimmed
            : Path.Combine(repoRoot, trimmed);
        var relative = Path.GetRelativePath(repoRoot, combined);
        return NormalizePath(relative);
    }

    private static int UpdateIndexSection(string filePath, string markerName, string content, bool dryRun)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        var startMarker = $"<!-- {markerName}:start -->";
        var endMarker = $"<!-- {markerName}:end -->";
        var fileContent = File.ReadAllText(filePath);
        var updated = ReplaceSection(fileContent, startMarker, endMarker, content, out var newContent);
        if (!updated)
        {
            return 0;
        }

        if (!dryRun)
        {
            File.WriteAllText(filePath, newContent);
        }
        return 1;
    }

    private static bool ReplaceSection(
        string content,
        string startMarker,
        string endMarker,
        string replacement,
        out string updated)
    {
        updated = content;
        var startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = content.IndexOf(endMarker, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
        {
            return false;
        }

        var before = content[..(startIndex + startMarker.Length)];
        var after = content[endIndex..];
        var normalized = replacement.TrimEnd();
        updated = $"{before}\n\n{normalized}\n{after}";
        return !string.Equals(content, updated, StringComparison.Ordinal);
    }

    private static bool IsDocsIndexFile(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.Equals($"{config.Paths.DocsRoot}/README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocTemplate(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.StartsWith($"{config.Paths.DocsRoot}/templates/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTitle(string body)
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
        return null;
    }

    private static string GetDocSection(string relativePath, WorkbenchConfig config)
    {
        var normalized = NormalizePath(relativePath);
        var prefix = $"{config.Paths.DocsRoot.TrimEnd('/')}/";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Docs";
        }

        var remainder = normalized[prefix.Length..];
        var slashIndex = remainder.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex < 0)
        {
            return "Root";
        }

        return remainder[..slashIndex];
    }

    private static string InferDocType(string relativePath, WorkbenchConfig config)
    {
        var normalized = NormalizePath(relativePath).ToLowerInvariant();
        var docsRoot = config.Paths.DocsRoot.TrimEnd('/').ToLowerInvariant();
        if (normalized.Contains($"{docsRoot}/10-product/"))
        {
            return "spec";
        }
        if (normalized.Contains($"{docsRoot}/40-decisions/"))
        {
            return "adr";
        }
        if (normalized.Contains($"{docsRoot}/50-runbooks/"))
        {
            return "runbook";
        }
        if (normalized.Contains($"{docsRoot}/20-architecture/"))
        {
            return "guide";
        }
        if (normalized.Contains($"{docsRoot}/00-overview/"))
        {
            return "doc";
        }
        return "doc";
    }

    private static string? BuildGithubFileLink(WorkbenchConfig config, string repoRelativePath)
    {
        if (string.IsNullOrWhiteSpace(config.Github.Owner) || string.IsNullOrWhiteSpace(config.Github.Repository))
        {
            return null;
        }

        var host = string.IsNullOrWhiteSpace(config.Github.Host) ? "github.com" : config.Github.Host;
        var branch = string.IsNullOrWhiteSpace(config.Git.DefaultBaseBranch) ? "main" : config.Git.DefaultBaseBranch;
        var path = NormalizePath(repoRelativePath);
        return $"https://{host}/{config.Github.Owner}/{config.Github.Repository}/blob/{branch}/{path}";
    }

    private static bool HasFrontMatter(string content)
    {
        return content.StartsWith("---\n", StringComparison.Ordinal)
            || content.StartsWith("---\r\n", StringComparison.Ordinal);
    }

    private static string EscapeTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string BuildMarkdownLink(string text, string href)
    {
        return $"[{EscapeTableCell(text)}]({href})";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static Dictionary<string, object?> GetNestedMap(Dictionary<string, object?> data, string key)
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

    private static string? GetString(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value.ToString();
    }

    private static List<string> GetStringList(Dictionary<string, object?> data, string key)
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

    private static int GetStatusRank(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "in-progress" => 0,
            "ready" => 1,
            "blocked" => 2,
            "draft" => 3,
            "done" => 4,
            "dropped" => 5,
            _ => 6
        };
    }

    private static GithubRepoRef? TryResolveRepo(string repoRoot, WorkbenchConfig config)
    {
        try
        {
            return GithubService.ResolveRepo(repoRoot, config);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
