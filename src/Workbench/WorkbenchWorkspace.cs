using System.Text;
using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    private static readonly string[] statusOrder = ["planned", "in_progress", "blocked", "complete", "cancelled", "superseded"];
    private static readonly string[] allowedStatuses = ["planned", "in_progress", "blocked", "complete", "cancelled", "superseded"];
    private static readonly string[] allowedTypes = ["work_item"];

    public WorkbenchWorkspace(string repoRoot, WorkbenchConfig config)
    {
        RepoRoot = repoRoot;
        Config = config;
    }

    public string RepoRoot { get; }

    public WorkbenchConfig Config { get; }

    public static IReadOnlyList<string> StatusOptions => allowedStatuses;

    public static IReadOnlyList<string> TypeOptions => allowedTypes;

    public IReadOnlyList<WorkItem> ListItems(bool includeDone, string? statusFilter, string? query)
    {
        var items = WorkItemService.ListItems(RepoRoot, Config, includeDone).Items;

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            !string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            items = items
                .Where(item => string.Equals(item.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim();
            items = items
                .Where(item => MatchesQuery(item, normalized))
                .ToList();
        }

        return items
            .OrderBy(item => GetStatusRank(item.Status))
            .ThenBy(item => GetPriorityRank(item.Priority))
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public WorkItem? GetItem(string id, bool includeDone)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return ListItems(includeDone, null, null)
            .FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public WorkItemEditorInput CreateEditorInput(WorkItem item)
    {
        return new WorkItemEditorInput
        {
            Path = item.Path,
            Title = item.Title,
            Status = item.Status,
            Priority = item.Priority,
            Owner = item.Owner,
            Summary = ExtractSection(item.Body, "Summary"),
            AcceptanceCriteria = ExtractSection(item.Body, "Acceptance criteria"),
            AppendNote = string.Empty,
            RenameFile = true
        };
    }

    public WorkItem CreateItem(WorkItemCreateInput input)
    {
        var created = WorkItemService.CreateItem(
            RepoRoot,
            Config,
            input.Type,
            input.Title,
            input.Status,
            input.Priority,
            input.Owner);

        return WorkItemService.LoadItem(created.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public WorkItem SaveItem(WorkItemEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Path))
        {
            throw new InvalidOperationException("Work item path is required.");
        }

        var result = WorkItemService.EditItem(
            input.Path,
            input.Title,
            input.Summary,
            ParseAcceptanceCriteria(input.AcceptanceCriteria),
            input.AppendNote,
            input.RenameFile,
            Config,
            RepoRoot,
            status: input.Status,
            priority: input.Priority,
            owner: input.Owner);

        return result.Item;
    }

    public async Task<NavigationService.NavigationSyncResult> SyncNavigationAsync(bool includeDone, bool dryRun)
    {
        return await NavigationService.SyncNavigationAsync(
            RepoRoot,
            Config,
            includeDone,
            syncIssues: true,
            force: true,
            dryRun,
            syncDocs: true).ConfigureAwait(false);
    }

    public async Task<int> SyncIssueLinksAsync(bool dryRun)
    {
        var items = WorkItemService.ListItems(RepoRoot, Config, includeDone: true).Items;
        return await WorkItemService.SyncIssueLinksAsync(RepoRoot, Config, items, dryRun).ConfigureAwait(false);
    }

    public ValidationResult Validate(bool strict)
    {
        var result = ValidationService.ValidateRepo(
            RepoRoot,
            Config,
            new ValidationOptions(Array.Empty<string>(), Array.Empty<string>(), false));

        if (strict && result.Warnings.Count > 0 && result.Errors.Count == 0)
        {
            result.Errors.Add("Warnings were promoted to errors by strict mode.");
        }

        return result;
    }

    private static bool MatchesQuery(WorkItem item, string query)
    {
        return item.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(item.Owner) && item.Owner.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(item.Priority) && item.Priority.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.Body.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Related.Specs.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.DesignLinks.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.VerificationLinks.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.RelatedArtifacts.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.Related.Files.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.Related.Prs.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase))
            || item.Related.Issues.Any(link => link.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetStatusRank(string status)
    {
        var index = Array.FindIndex(statusOrder, entry => string.Equals(entry, status, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? statusOrder.Length : index;
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

    private static IReadOnlyList<string>? ParseAcceptanceCriteria(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.StartsWith("-", StringComparison.Ordinal) ? line[1..].TrimStart() : line)
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static string ExtractSection(string body, string sectionName)
    {
        var normalizedBody = body.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalizedBody.Split('\n');
        var heading = $"## {sectionName}";
        var startIndex = Array.FindIndex(lines, line => string.Equals(line.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        if (startIndex < 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("## ", StringComparison.Ordinal) && builder.Length > 0)
            {
                break;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal) && builder.Length == 0)
            {
                break;
            }

            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }
}
