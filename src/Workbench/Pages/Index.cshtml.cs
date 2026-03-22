using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Workbench;
using Workbench.Core;

namespace Workbench.Pages;

public class IndexModel : RepoPageModel
{
    public IndexModel(WorkbenchWorkspace workspace, WorkbenchUserProfileStore profileStore)
        : base(workspace, profileStore)
    {
    }

    [BindProperty(SupportsGet = true)]
    public string? SelectedId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeDone { get; set; }

    [BindProperty]
    public WorkItemEditorInput Edit { get; set; } = new();

    [TempData]
    public string? BannerTitle { get; set; }

    [TempData]
    public string? BannerMessage { get; set; }

    [TempData]
    public string? BannerDetails { get; set; }

    public IReadOnlyList<WorkItem> Items { get; private set; } = Array.Empty<WorkItem>();

    public WorkItem? SelectedItem { get; private set; }

    public IReadOnlyList<string> BannerLines =>
        string.IsNullOrWhiteSpace(BannerDetails)
            ? Array.Empty<string>()
            : BannerDetails.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<string> StatusOptions => WorkbenchWorkspace.StatusOptions;

    public static IReadOnlyList<string> TypeOptions => WorkbenchWorkspace.TypeOptions;

    public static IReadOnlyList<string> PriorityOptions => ["", "low", "medium", "high", "critical"];

    public void OnGet()
    {
        LoadPage(populateEditor: true);
    }

    public IActionResult OnPostSave()
    {
        try
        {
            var saved = Workspace.SaveItem(Edit);
            SetBanner(
                "Work item saved",
                $"{saved.Id} updated locally.",
                new[]
                {
                    saved.Path,
                    $"Status: {saved.Status}",
                    $"Priority: {saved.Priority ?? "-"}",
                    $"Owner: {saved.Owner ?? "-"}"
                });
            return RedirectToPage(new
            {
                selectedId = saved.Id,
                statusFilter = StatusFilter,
                query = Query,
                includeDone = IncludeDone
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSyncNavigationAsync()
    {
        try
        {
            var result = await Workspace.SyncNavigationAsync(IncludeDone, dryRun: false).ConfigureAwait(false);
            var details = result.MissingDocs.Select(entry => $"Missing doc: {entry}")
                .Concat(result.MissingItems.Select(entry => $"Missing item: {entry}"))
                .Concat(result.Warnings.Select(entry => $"Warning: {entry}"))
                .ToArray();

            SetBanner(
                "Navigation synced",
                "Docs, work items, indexes, and workboard refreshed.",
                new[]
                {
                    $"Docs updated: {result.DocsUpdated}",
                    $"Items updated: {result.ItemsUpdated}",
                    $"Index files updated: {result.IndexFilesUpdated}",
                    $"Workboard updated: {result.WorkboardUpdated}"
                }.Concat(details));
            return RedirectToPage(new
            {
                selectedId = SelectedId,
                statusFilter = StatusFilter,
                query = Query,
                includeDone = IncludeDone
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSyncIssuesAsync()
    {
        try
        {
            var updated = await Workspace.SyncIssueLinksAsync(dryRun: false).ConfigureAwait(false);
            SetBanner(
                "Issue links synced",
                $"{updated} work items updated from GitHub issue links.",
                Array.Empty<string>());
            return RedirectToPage(new
            {
                selectedId = SelectedId,
                statusFilter = StatusFilter,
                query = Query,
                includeDone = IncludeDone
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostValidate()
    {
        try
        {
            var result = Workspace.Validate(strict: false);
            var details = result.Errors.Select(entry => $"Error: {entry}")
                .Concat(result.Warnings.Select(entry => $"Warning: {entry}"))
                .ToArray();

            SetBanner(
                "Validation complete",
                result.Errors.Count == 0 && result.Warnings.Count == 0
                    ? "No validation issues found."
                    : "Validation returned findings.",
                new[]
                {
                    $"Work items scanned: {result.WorkItemCount}",
                    $"Markdown files scanned: {result.MarkdownFileCount}",
                    $"Errors: {result.Errors.Count}",
                    $"Warnings: {result.Warnings.Count}"
                }.Concat(details));
            return RedirectToPage(new
            {
                selectedId = SelectedId,
                statusFilter = StatusFilter,
                query = Query,
                includeDone = IncludeDone
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public IActionResult OnPostRegenerateBoard()
    {
        try
        {
            var result = Workspace.RegenerateWorkboard();
            SetBanner(
                "Workboard regenerated",
                $"Updated {result.Path}.",
                result.Counts.Select(entry => $"{entry.Key}: {entry.Value}"));
            return RedirectToPage(new
            {
                selectedId = SelectedId,
                statusFilter = StatusFilter,
                query = Query,
                includeDone = IncludeDone
            });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, FormatError(ex));
            LoadPage(populateEditor: false);
            return Page();
        }
    }

    public static string StatusLabel(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "in-progress" => "In progress",
            "done" => "Done",
            "blocked" => "Blocked",
            "ready" => "Ready",
            "draft" => "Draft",
            "dropped" => "Dropped",
            _ => status
        };
    }

    public static string StatusClass(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "in-progress" => "status-progress",
            "done" => "status-done",
            "blocked" => "status-blocked",
            "ready" => "status-ready",
            "draft" => "status-draft",
            "dropped" => "status-dropped",
            _ => "status-default"
        };
    }

    public static string PriorityClass(string? priority)
    {
        return priority?.ToLowerInvariant() switch
        {
            "critical" => "priority-critical",
            "high" => "priority-high",
            "medium" => "priority-medium",
            "low" => "priority-low",
            _ => "priority-default"
        };
    }

    public string ResolveRelatedLinkHref(string link)
    {
        var trimmed = link.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var normalized = trimmed.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("work/items/", StringComparison.OrdinalIgnoreCase))
        {
            var id = Path.GetFileNameWithoutExtension(normalized);
            return Url.Page("/Index", new { selectedId = id }) ?? trimmed;
        }

        if (normalized.StartsWith("overview/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("contracts/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("decisions/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("runbooks/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("tracking/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("specs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("architecture/", StringComparison.OrdinalIgnoreCase))
        {
            return Url.Page("/Docs", new { selectedPath = normalized }) ?? trimmed;
        }

        return Url.Page("/Files", new { selectedPath = normalized }) ?? trimmed;
    }

    public static string ResolveRelatedLinkLabel(string link)
    {
        var trimmed = link.Trim().Replace('\\', '/');
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return Path.GetFileNameWithoutExtension(trimmed);
    }

    private void LoadPage(bool populateEditor)
    {
        ApplyChrome("Items");

        if (string.IsNullOrWhiteSpace(StatusFilter))
        {
            StatusFilter = "all";
        }

        Items = Workspace.ListItems(IncludeDone, StatusFilter, Query);
        var selected = ResolveSelectedItem();
        SelectedItem = selected;

        if (populateEditor)
        {
            Edit = selected is null ? new WorkItemEditorInput() : Workspace.CreateEditorInput(selected);
        }

        if (string.IsNullOrWhiteSpace(SelectedId) && selected is not null)
        {
            SelectedId = selected.Id;
        }
    }

    private WorkItem? ResolveSelectedItem()
    {
        if (Items.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(SelectedId))
        {
            var selected = Items.FirstOrDefault(item => item.Id.Equals(SelectedId, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                return selected;
            }
        }

        return Items[0];
    }

    private void SetBanner(string title, string message, IEnumerable<string> details)
    {
        BannerTitle = title;
        BannerMessage = message;
        BannerDetails = string.Join('\n', details.Where(entry => !string.IsNullOrWhiteSpace(entry)));
    }

    private static string FormatError(Exception ex)
    {
        return $"{ex.GetType().Name}: {ex.Message}";
    }
}
