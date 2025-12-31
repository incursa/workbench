using System.Collections;
using System.Globalization;
using System.Text;

namespace Workbench;

public static class NavigationService
{
    public sealed record NavigationSyncResult(
        int DocsUpdated,
        int ItemsUpdated,
        int IndexFilesUpdated,
        int WorkboardUpdated,
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

    public static async Task<NavigationSyncResult> SyncNavigationAsync(
        string repoRoot,
        WorkbenchConfig config,
        bool includeDone,
        bool syncIssues,
        bool force,
        bool syncWorkboard,
        bool dryRun,
        bool syncDocs = true)
    {
        var docSync = syncDocs
            ? await DocService.SyncLinksAsync(repoRoot, config, includeAllDocs: true, syncIssues, includeDone, dryRun)
.ConfigureAwait(false) : new DocService.DocSyncResult(0, 0, new List<string>(), new List<string>());
        var normalizedItems = WorkItemService.NormalizeRelatedLinks(repoRoot, config, includeDone, dryRun);
        var warnings = new List<string>();
        var docEntries = LoadDocEntries(repoRoot, config, warnings);
        var docReadmePath = Path.Combine(repoRoot, config.Paths.DocsRoot, "README.md");
        var workReadmePath = Path.Combine(repoRoot, config.Paths.WorkRoot, "README.md");
        var rootReadmePath = Path.Combine(repoRoot, "README.md");

        var indexCreated = 0;
        indexCreated += EnsureIndexFile(docReadmePath, BuildDocsReadmeTemplate(), dryRun);
        indexCreated += EnsureIndexFile(workReadmePath, BuildWorkReadmeTemplate(), dryRun);
        indexCreated += EnsureIndexFile(rootReadmePath, BuildRootReadmeTemplate(), dryRun);

        var docIndex = BuildDocsIndex(repoRoot, config, docReadmePath, docEntries);
        var workIndex = BuildWorkIndex(repoRoot, config, workReadmePath, docEntries, includeDone);
        var rootIndex = BuildRootIndex(repoRoot, config);

        var indexUpdated = indexCreated;
        indexUpdated += UpdateIndexSection(docReadmePath, "workbench:docs-index", docIndex, force, dryRun);
        indexUpdated += UpdateIndexSection(workReadmePath, "workbench:work-index", workIndex, force, dryRun);
        indexUpdated += UpdateIndexSection(rootReadmePath, "workbench:root-index", rootIndex, force, dryRun);

        var workboardUpdated = 0;
        if (syncWorkboard)
        {
            if (!dryRun)
            {
                WorkboardService.Regenerate(repoRoot, config);
            }
            workboardUpdated = 1;
        }

        return new NavigationSyncResult(
            docSync.DocsUpdated,
            docSync.ItemsUpdated + normalizedItems,
            indexUpdated,
            workboardUpdated,
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

                var typeLabel = FormatDocType(entry.Type);
                var statusLabel = FormatDocStatus(entry.Status);
                builder.AppendLine($"| {docLink} | {typeLabel} | {statusLabel} | {githubLink} | {workItems} |");
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
            var statusLabel = FormatWorkItemStatus(entry.Item.Status);

            builder.AppendLine($"| {itemLink} | {statusLabel} | {githubLink} | {issueLinks} | {relatedLinks} |");
        }
    }

    private static string BuildRootIndex(string repoRoot, WorkbenchConfig config)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### Quick links");
        builder.AppendLine("- [Docs index](docs/README.md)");
        builder.AppendLine("- [Work index](docs/70-work/README.md)");
        builder.AppendLine();
        builder.AppendLine("### Work item stats");

        var items = WorkItemService.ListItems(repoRoot, config, includeDone: true).Items;
        var total = items.Count;
        var closed = items.Count(item => IsTerminalStatus(item.Status));
        var open = total - closed;

        builder.AppendLine("| Metric | Count |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Open | {open} |"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Closed | {closed} |"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| Total | {total} |"));
        builder.AppendLine();

        if (total > 0)
        {
            var counts = items
                .GroupBy(item => item.Status ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            builder.AppendLine("| Status | Count |");
            builder.AppendLine("| --- | --- |");
            foreach (var status in new[] { "draft", "ready", "in-progress", "blocked", "done", "dropped" })
            {
                counts.TryGetValue(status, out var count);
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {FormatWorkItemStatus(status)} | {count} |"));
            }
        }

        return builder.ToString().TrimEnd();
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
                var title = string.IsNullOrWhiteSpace(item.Title) ? item.Id : item.Title;
                var summaryLink = BuildMarkdownLink(item.Id, relative);
                links.Add($"<details><summary>{summaryLink}</summary>{EscapeTableCell(title)}</details>");
            }
            else
            {
                links.Add(EscapeTableCell(workItemId));
            }
        }

        return string.Join("<br>", links);
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

    private static int UpdateIndexSection(string filePath, string markerName, string content, bool force, bool dryRun)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        var startMarker = $"<!-- {markerName}:start -->";
        var endMarker = $"<!-- {markerName}:end -->";
        var fileContent = File.ReadAllText(filePath);
        var updated = ReplaceSection(fileContent, startMarker, endMarker, content, out var newContent);
        if (!updated && !force)
        {
            return 0;
        }

        if (!dryRun)
        {
            File.WriteAllText(filePath, newContent);
        }
        return !dryRun && (updated || force) ? 1 : 0;
    }

    private static int EnsureIndexFile(string filePath, string template, bool dryRun)
    {
        if (File.Exists(filePath))
        {
            return 0;
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory());
            File.WriteAllText(filePath, template);
        }
        return 1;
    }

    private static string BuildDocsReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Docs",
            string.Empty,
            "Documentation for product, architecture, decisions, and operational guidance.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:docs-index:start -->",
            "<!-- workbench:docs-index:end -->",
            string.Empty);
    }

    private static string BuildWorkReadmeTemplate()
    {
        return string.Join(
            "\n",
            "---",
            "workbench:",
            "  type: doc",
            "  workItems: []",
            "  codeRefs: []",
            "owner: platform",
            "status: active",
            "updated: 0000-00-00",
            "---",
            string.Empty,
            "# Work",
            string.Empty,
            "Work items and the day-to-day board for delivery.",
            string.Empty,
            "## Workboard",
            string.Empty,
            "Generated by `workbench nav sync --workboard`.",
            string.Empty,
            "<!-- workbench:workboard:start -->",
            "## Now (in-progress)",
            string.Empty,
            "_None._",
            string.Empty,
            "## Next (ready)",
            string.Empty,
            "_None._",
            string.Empty,
            "## Blocked",
            string.Empty,
            "_None._",
            string.Empty,
            "<details>",
            "<summary>Draft (backlog) (0)</summary>",
            string.Empty,
            "_None._",
            string.Empty,
            "</details>",
            "<!-- workbench:workboard:end -->",
            string.Empty,
            "## Layout",
            string.Empty,
            "- `docs/70-work/items`: active work items named `<ID>-<slug>.md`.",
            "- `docs/70-work/done`: closed items for reference and audit history.",
            "- `docs/70-work/templates`: templates used to create new work items.",
            string.Empty,
            "## Workflow",
            string.Empty,
            "1. Start from a template in `docs/70-work/templates/`.",
            "2. Create a new file in `docs/70-work/items/` with a clear ID and slug.",
            "3. Move the item to `docs/70-work/done/` when it is closed or superseded.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:work-index:start -->",
            "<!-- workbench:work-index:end -->",
            string.Empty);
    }

    private static string BuildRootReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Workbench",
            string.Empty,
            "## Navigation",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:root-index:start -->",
            "<!-- workbench:root-index:end -->",
            string.Empty);
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

    private static string FormatDocType(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            "spec" => "🧭 spec",
            "adr" => "📘 adr",
            "runbook" => "🛠 runbook",
            "guide" => "🧩 guide",
            "doc" => "📄 doc",
            _ => EscapeTableCell(docType)
        };
    }

    private static string FormatDocStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "❔ unknown";
        }

        return status.ToLowerInvariant() switch
        {
            "draft" => "🟡 draft",
            "ready" => "🟢 ready",
            "active" => "🟢 active",
            "accepted" => "✅ accepted",
            "blocked" => "🟥 blocked",
            "done" => "✅ done",
            "dropped" => "🚫 dropped",
            "template" => "🧱 template",
            _ => EscapeTableCell(status)
        };
    }

    private static string FormatWorkItemStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "❔ unknown";
        }

        return status.ToLowerInvariant() switch
        {
            "draft" => "🟡 draft",
            "ready" => "🟢 ready",
            "in-progress" => "🔵 in-progress",
            "blocked" => "🟥 blocked",
            "done" => "✅ done",
            "dropped" => "🚫 dropped",
            _ => EscapeTableCell(status)
        };
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "dropped", StringComparison.OrdinalIgnoreCase);
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
