using System.Globalization;

namespace Workbench;

public static class WorkboardService
{
    public sealed record WorkboardResult(string Path, IDictionary<string, int> Counts);
    private const string WorkboardStart = "<!-- workbench:workboard:start -->";
    private const string WorkboardEnd = "<!-- workbench:workboard:end -->";

    public static WorkboardResult Regenerate(string repoRoot, WorkbenchConfig config)
    {
        var list = WorkItemService.ListItems(repoRoot, config, includeDone: false);
        var sections = new Dictionary<string, List<WorkItem>>(StringComparer.OrdinalIgnoreCase)
        {
            ["in-progress"] = new(),
            ["ready"] = new(),
            ["blocked"] = new(),
            ["draft"] = new()
        };

        foreach (var item in list.Items)
        {
            if (sections.TryGetValue(item.Status, out var bucket))
            {
                bucket.Add(item);
            }
        }

        foreach (var bucket in sections.Values)
        {
            bucket.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        }

        var workboardPath = Path.Combine(repoRoot, config.Paths.WorkboardFile);
        var workboardDir = Path.GetDirectoryName(workboardPath) ?? repoRoot;
        var defaultRepo = TryResolveRepo(repoRoot, config);

        var lines = new List<string>
        {
            "## Now (in-progress)",
            string.Empty
        };
        lines.AddRange(FormatSection(sections["in-progress"], repoRoot, workboardDir, defaultRepo));
        lines.Add(string.Empty);
        lines.Add("## Next (ready)");
        lines.Add(string.Empty);
        lines.AddRange(FormatSection(sections["ready"], repoRoot, workboardDir, defaultRepo));
        lines.Add(string.Empty);
        lines.Add("## Blocked");
        lines.Add(string.Empty);
        lines.AddRange(FormatSection(sections["blocked"], repoRoot, workboardDir, defaultRepo));
        lines.Add(string.Empty);
        lines.AddRange(FormatCollapsibleSection("Draft (backlog)", sections["draft"], repoRoot, workboardDir, defaultRepo));
        lines.Add(string.Empty);

        var content = string.Join("\n", lines).TrimEnd();
        Directory.CreateDirectory(workboardDir);
        if (File.Exists(workboardPath))
        {
            var existing = File.ReadAllText(workboardPath);
            if (TryReplaceSection(existing, WorkboardStart, WorkboardEnd, content, out var updated))
            {
                File.WriteAllText(workboardPath, updated);
            }
            else
            {
                File.WriteAllText(workboardPath, content + "\n");
            }
        }
        else
        {
            File.WriteAllText(workboardPath, content + "\n");
        }

        var counts = sections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count, StringComparer.OrdinalIgnoreCase);
        return new WorkboardResult(workboardPath, counts);
    }

    private static bool TryReplaceSection(
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
        return true;
    }

    private static IEnumerable<string> FormatSection(
        List<WorkItem> items,
        string repoRoot,
        string workboardDir,
        GithubRepoRef? defaultRepo)
    {
        if (items.Count == 0)
        {
            yield return "_None._";
            yield break;
        }

        yield return "| Item | Priority | Type | Status | Owner | Issues | Related |";
        yield return "| --- | --- | --- | --- | --- | --- | --- |";
        foreach (var item in items.OrderBy(entry => GetPriorityRank(entry.Priority)).ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(workboardDir, item.Path).Replace('\\', '/');
            var itemTitle = $"{item.Id} - {item.Title}";
            var itemLink = BuildMarkdownLink(itemTitle, relative);
            var status = FormatWorkItemStatus(item.Status);
            var owner = string.IsNullOrWhiteSpace(item.Owner) ? "-" : EscapeTableCell(item.Owner);
            var priority = FormatPriority(item.Priority);
            var type = FormatWorkItemType(item.Type);
            var issues = FormatIssueLinks(item.Related.Issues, defaultRepo);
            var related = FormatRelatedLinks(item, repoRoot, workboardDir);

            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"| {itemLink} | {priority} | {type} | {status} | {owner} | {issues} | {related} |");
        }
    }

    private static IEnumerable<string> FormatCollapsibleSection(
        string title,
        List<WorkItem> items,
        string repoRoot,
        string workboardDir,
        GithubRepoRef? defaultRepo)
    {
        yield return "<details>";
        yield return $"<summary>{EscapeInlineText(title)} ({items.Count})</summary>";
        yield return string.Empty;
        foreach (var line in FormatSection(items, repoRoot, workboardDir, defaultRepo))
        {
            yield return line;
        }
        yield return string.Empty;
        yield return "</details>";
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

    private static string FormatRelatedLinks(WorkItem item, string repoRoot, string workboardDir)
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

            var relative = Path.GetRelativePath(workboardDir, targetPath).Replace('\\', '/');
            var title = Path.GetFileNameWithoutExtension(normalized);
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
        return relative.Replace('\\', '/');
    }

    private static string BuildMarkdownLink(string text, string href)
    {
        return $"[{EscapeTableCell(text)}]({href})";
    }

    private static string EscapeInlineText(string value)
    {
        return value.Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string EscapeTableCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
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
            _ => EscapeTableCell(status)
        };
    }

    private static string FormatPriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return "-";
        }

        return priority.ToLowerInvariant() switch
        {
            "critical" => "🔴 critical",
            "high" => "🟠 high",
            "medium" => "🟡 medium",
            "low" => "🟢 low",
            _ => EscapeTableCell(priority)
        };
    }

    private static string FormatWorkItemType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "❔ unknown";
        }

        return type.ToLowerInvariant() switch
        {
            "task" => "🧩 task",
            "bug" => "🐛 bug",
            "spike" => "🧪 spike",
            _ => EscapeTableCell(type)
        };
    }

    private static int GetPriorityRank(string? priority)
    {
        return priority?.ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
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
