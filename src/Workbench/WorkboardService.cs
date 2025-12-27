namespace Workbench;

public static class WorkboardService
{
    public sealed record WorkboardResult(string Path, IDictionary<string, int> Counts);

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

        var lines = new List<string>
        {
            "# Workboard",
            string.Empty,
            "## Now (in-progress)",
            string.Empty
        };
        lines.AddRange(FormatSection(sections["in-progress"], repoRoot));
        lines.Add(string.Empty);
        lines.Add("## Next (ready)");
        lines.Add(string.Empty);
        lines.AddRange(FormatSection(sections["ready"], repoRoot));
        lines.Add(string.Empty);
        lines.Add("## Blocked");
        lines.Add(string.Empty);
        lines.AddRange(FormatSection(sections["blocked"], repoRoot));
        lines.Add(string.Empty);
        lines.Add("## Draft");
        lines.Add(string.Empty);
        lines.AddRange(FormatSection(sections["draft"], repoRoot));
        lines.Add(string.Empty);

        var content = string.Join("\n", lines);
        var path = Path.Combine(repoRoot, config.Paths.WorkboardFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? repoRoot);
        File.WriteAllText(path, content);

        var counts = sections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count,  StringComparer.OrdinalIgnoreCase);
        return new WorkboardResult(path, counts);
    }

    private static IEnumerable<string> FormatSection(List<WorkItem> items, string repoRoot)
    {
        foreach (var item in items)
        {
            var relative = "/" + Path.GetRelativePath(repoRoot, item.Path).Replace('\\', '/');
            yield return $"- {item.Id} - {item.Title} ({relative})";
        }
    }
}
