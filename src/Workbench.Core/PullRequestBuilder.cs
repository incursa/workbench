namespace Workbench;

public static class PullRequestBuilder
{
    public static string BuildBody(WorkItem item)
    {
        var summary = ExtractSection(item.Body, "Summary");
        var criteria = ExtractSection(item.Body, "Acceptance criteria");
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add("## Summary");
            lines.Add(summary.Trim());
            lines.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(criteria))
        {
            lines.Add("## Acceptance criteria");
            lines.Add(criteria.Trim());
            lines.Add(string.Empty);
        }

        var related = BuildRelated(item);
        if (related.Count > 0)
        {
            lines.Add("## Related");
            lines.AddRange(related);
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines).TrimEnd();
    }

    public static string BuildBody(WorkItem item, GithubRepoRef repo, string baseBranch)
    {
        var summary = ExtractSection(item.Body, "Summary");
        var criteria = ExtractSection(item.Body, "Acceptance criteria");
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add("## Summary");
            lines.Add(summary.Trim());
            lines.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(criteria))
        {
            lines.Add("## Acceptance criteria");
            lines.Add(criteria.Trim());
            lines.Add(string.Empty);
        }

        var related = BuildRelated(item, repo, baseBranch);
        if (related.Count > 0)
        {
            lines.Add("## Related");
            lines.AddRange(related);
            lines.Add(string.Empty);
        }

        return string.Join("\n", lines).TrimEnd();
    }

    private static string ExtractSection(string body, string heading)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals($"## {heading}", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }
        if (start == -1)
        {
            return string.Empty;
        }

        var collected = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }
            collected.Add(line);
        }
        return string.Join("\n", collected).Trim();
    }

    private static List<string> BuildRelated(WorkItem item)
    {
        var list = new List<string>();
        void AddLinks(IEnumerable<string> links, string label)
        {
            var entries = links.Where(link => !string.IsNullOrWhiteSpace(link)).ToList();
            if (entries.Count == 0)
            {
                return;
            }
            list.Add($"- {label}:");
            foreach (var entry in entries)
            {
                list.Add($"  - {entry}");
            }
        }

        AddLinks(item.Related.Specs, "Specs");
        AddLinks(item.Related.Adrs, "ADRs");
        AddLinks(item.Related.Files, "Files");
        AddLinks(item.Related.Prs, "PRs");
        AddLinks(item.Related.Issues, "Issues");
        return list;
    }

    private static List<string> BuildRelated(WorkItem item, GithubRepoRef repo, string baseBranch)
    {
        var list = new List<string>();
        void AddLinks(IEnumerable<string> links, string label, bool linkToRepo = false)
        {
            var entries = links.Where(link => !string.IsNullOrWhiteSpace(link)).ToList();
            if (entries.Count == 0)
            {
                return;
            }
            list.Add($"- {label}:");
            foreach (var entry in entries)
            {
                var rendered = linkToRepo ? ToRepoMarkdownLink(repo, baseBranch, entry) : entry;
                list.Add($"  - {rendered}");
            }
        }

        AddLinks(item.Related.Specs, "Specs", linkToRepo: true);
        AddLinks(item.Related.Adrs, "ADRs", linkToRepo: true);
        AddLinks(item.Related.Files, "Files", linkToRepo: true);
        AddLinks(item.Related.Prs, "PRs");
        return list;
    }

    private static string ToRepoMarkdownLink(GithubRepoRef repo, string baseBranch, string link)
    {
        var trimmed = link.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal) && trimmed.Length > 1)
        {
            trimmed = trimmed[1..^1].Trim();
        }
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains("](", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return trimmed;
        }
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var label = GetLinkLabel(trimmed);
            return $"[{label}]({trimmed})";
        }

        var normalized = trimmed.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return trimmed;
        }

        var url = $"https://{repo.Host}/{repo.Owner}/{repo.Repo}/blob/{baseBranch}/{normalized}";
        var labelFromPath = GetLinkLabel(normalized);
        return $"[{labelFromPath}]({url})";
    }

    private static string GetLinkLabel(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var last = uri.Segments.LastOrDefault() ?? value;
            return Path.GetFileNameWithoutExtension(last.Trim('/'));
        }

        var trimmed = value.Replace('\\', '/').TrimEnd('/');
        var file = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(file))
        {
            return trimmed;
        }
        var withoutExtension = Path.GetFileNameWithoutExtension(file);
        return string.IsNullOrWhiteSpace(withoutExtension) ? file : withoutExtension;
    }
}
