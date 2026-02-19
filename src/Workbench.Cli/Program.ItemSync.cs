// Work item sync logic shared by CLI commands.
// Handles GitHub issue fetch/update and branch creation while respecting dry-run and terminal status rules.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static async Task<ItemSyncData> RunItemSyncAsync(
        string repoRoot,
        WorkbenchConfig config,
        string[] ids,
        string[] issueInputs,
        bool importIssues,
        string? prefer,
        bool dryRun,
        bool syncIssues = true)
    {
        GithubRepoRef? defaultRepo = null;
        if (syncIssues)
        {
            defaultRepo = GithubService.ResolveRepo(repoRoot, config);
        }

        var preferredSource = ResolvePreferredSyncSource(config, prefer);
        var preferGithub = string.Equals(preferredSource, "github", StringComparison.OrdinalIgnoreCase);
        var failOnConflict = string.Equals(preferredSource, "fail", StringComparison.OrdinalIgnoreCase);
        var items = new List<WorkItem>();
        if (ids.Length > 0)
        {
            foreach (var id in ids)
            {
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
                items.Add(item);
            }
        }
        else
        {
            items.AddRange(WorkItemService.ListItems(repoRoot, config, includeDone: true).Items);
        }

        var issueMap = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        if (syncIssues)
        {
            foreach (var item in items)
            {
                foreach (var entry in item.Related.Issues)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }
                    var issueRef = GithubService.ParseIssueReference(entry, defaultRepo!);
                    var key = $"{issueRef.Repo.Display}#{issueRef.Number.ToString(CultureInfo.InvariantCulture)}";
                    if (!issueMap.ContainsKey(key))
                    {
                        issueMap[key] = item;
                    }
                }
            }
        }

        var issueCache = new Dictionary<string, GithubIssue>(StringComparer.OrdinalIgnoreCase);
        var missingIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        async Task<(GithubIssue? Issue, GithubIssueRef Ref)> TryFetchIssueAsync(string issueLink)
        {
            GithubIssueRef issueRef = GithubService.ParseIssueReference(issueLink, defaultRepo!);
            var key = $"{issueRef.Repo.Display}#{issueRef.Number.ToString(CultureInfo.InvariantCulture)}";
            if (missingIssues.Contains(key))
            {
                return (null, issueRef);
            }
            if (!issueCache.TryGetValue(key, out var issue))
            {
                try
                {
                    issue = await GithubService.FetchIssueAsync(repoRoot, config, issueRef).ConfigureAwait(false);
                    issueCache[key] = issue;
                }
                catch (Exception ex)
                {
                    // Cache failures to avoid repeated network calls for missing/unauthorized issues.
                    var reason = runtimeDebugEnabled ? ex.ToString() : ex.GetType().Name;
                    warnings.Add($"Issue fetch failed: {key} ({reason})");
                    missingIssues.Add(key);
                    return (null, issueRef);
                }
            }
            return (issue, issueRef);
        }

        var imported = new List<ItemSyncImportEntry>();
        IList<GithubIssue> issuesToImport;
        if (!syncIssues)
        {
            issuesToImport = Array.Empty<GithubIssue>();
        }
        else if (issueInputs.Length > 0)
        {
            var selected = new List<GithubIssue>();
            foreach (var input in issueInputs)
            {
                var (issue, _) = await TryFetchIssueAsync(input).ConfigureAwait(false);
                if (issue is null)
                {
                    continue;
                }
                selected.Add(issue);
            }
            issuesToImport = selected;
        }
        else if (ids.Length > 0)
        {
            issuesToImport = Array.Empty<GithubIssue>();
        }
        else
        {
            issuesToImport = importIssues
                ? await GithubService.ListIssuesAsync(repoRoot, config, defaultRepo!)
                    .ConfigureAwait(false) : Array.Empty<GithubIssue>();
        }

        foreach (var issue in issuesToImport)
        {
            var key = $"{issue.Repo.Display}#{issue.Number.ToString(CultureInfo.InvariantCulture)}";
            if (issueMap.ContainsKey(key))
            {
                continue;
            }

            var issuePayload = new GithubIssuePayload(
                issue.Repo.Display,
                issue.Number,
                issue.Url,
                issue.Title,
                issue.State,
                issue.Labels,
                issue.PullRequests);

            if (dryRun)
            {
                imported.Add(new ItemSyncImportEntry(issuePayload, null));
                continue;
            }

            var type = ResolveIssueType(issue, null);
            var status = ResolveIssueStatus(issue, null);
            var item = WorkItemService.CreateItemFromGithubIssue(repoRoot, config, issue, type, status, null, null);
            imported.Add(new ItemSyncImportEntry(issuePayload, ItemToPayload(item)));
            items.Add(item);
            issueMap[key] = item;
        }

        var issuesUpdated = new List<ItemSyncIssueUpdateEntry>();
        var itemsUpdated = new List<ItemSyncItemUpdateEntry>();
        var conflicts = new List<ItemSyncConflictEntry>();
        if (syncIssues)
        {
            foreach (var item in items)
            {
                var issueLink = item.Related.Issues.FirstOrDefault(link => !string.IsNullOrWhiteSpace(link));
                if (string.IsNullOrWhiteSpace(issueLink))
                {
                    continue;
                }

                var (issue, issueRef) = await TryFetchIssueAsync(issueLink).ConfigureAwait(false);
                if (issue is null)
                {
                    continue;
                }

                var summary = ExtractSection(item.Body, "Summary");
                var itemNeedsUpdate = !StringsEqual(item.Title, issue.Title);
                if (!itemNeedsUpdate && !string.IsNullOrWhiteSpace(issue.Body))
                {
                    itemNeedsUpdate = !summary.Contains(issue.Body, StringComparison.Ordinal);
                }

                var desiredTitle = item.Title;
                var desiredBody = PullRequestBuilder.BuildBody(item, defaultRepo!, config.Git.DefaultBaseBranch);
                var issueNeedsUpdate = !StringsEqual(issue.Title, desiredTitle) || !StringsEqual(issue.Body, desiredBody);

                if (failOnConflict && issueNeedsUpdate && itemNeedsUpdate)
                {
                    conflicts.Add(new ItemSyncConflictEntry(
                        item.Id,
                        issue.Url,
                        "Local and GitHub issue content diverged. Re-run with `--prefer local` or `--prefer github`."));
                    continue;
                }

                if (preferGithub)
                {
                    if (!itemNeedsUpdate)
                    {
                        continue;
                    }

                    if (!dryRun)
                    {
                        WorkItemService.UpdateItemFromGithubIssue(item.Path, issue, apply: true);
                    }
                    itemsUpdated.Add(new ItemSyncItemUpdateEntry(item.Id, issue.Url));
                }
                else
                {
                    if (IsTerminalStatus(item.Status))
                    {
                        // Terminal items do not push updates back to GitHub.
                        continue;
                    }

                    if (!issueNeedsUpdate)
                    {
                        continue;
                    }

                    if (!dryRun)
                    {
                        await GithubService.UpdateIssueAsync(repoRoot, config, issueRef, desiredTitle, desiredBody).ConfigureAwait(false);
                        WorkItemService.UpdateGithubSynced(item.Path, DateTime.UtcNow);
                    }
                    issuesUpdated.Add(new ItemSyncIssueUpdateEntry(item.Id, issue.Url));
                }
            }
        }

        var issuesCreated = new List<ItemSyncIssueEntry>();
        if (syncIssues)
        {
            foreach (var item in items)
            {
                if (IsTerminalStatus(item.Status))
                {
                    continue;
                }
                if (item.Related.Issues.Count > 0)
                {
                    continue;
                }

                var body = PullRequestBuilder.BuildBody(item, defaultRepo!, config.Git.DefaultBaseBranch);
                if (dryRun)
                {
                    issuesCreated.Add(new ItemSyncIssueEntry(item.Id, string.Empty));
                    continue;
                }

                var issueUrl = await GithubService.CreateIssueAsync(repoRoot, config, defaultRepo!, item.Title, body, item.Tags).ConfigureAwait(false);
                WorkItemService.AddRelatedLink(item.Path, "issues", issueUrl);
                WorkItemService.UpdateGithubSynced(item.Path, DateTime.UtcNow);
                issuesCreated.Add(new ItemSyncIssueEntry(item.Id, issueUrl));
            }
        }

        var branchesCreated = new List<ItemSyncBranchEntry>();
        foreach (var item in items)
        {
            if (IsTerminalStatus(item.Status))
            {
                continue;
            }

            var listedBranch = item.Related.Branches.FirstOrDefault(branch => !string.IsNullOrWhiteSpace(branch));
            if (string.IsNullOrWhiteSpace(listedBranch))
            {
                continue;
            }

            var branchName = listedBranch;
            var branchExists = GitService.BranchExists(repoRoot, branchName);
            if (!branchExists)
            {
                if (!dryRun)
                {
                    GitService.CreateBranch(repoRoot, branchName);
                    GitService.Push(repoRoot, branchName);
                }
                branchesCreated.Add(new ItemSyncBranchEntry(item.Id, branchName));
            }
        }

        return new ItemSyncData(imported, issuesCreated, issuesUpdated, itemsUpdated, branchesCreated, conflicts, warnings, dryRun);
    }
}
