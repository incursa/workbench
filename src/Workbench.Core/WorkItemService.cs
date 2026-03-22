// Work item creation, parsing, and mutation logic.
// Invariants: front matter schema must remain consistent; ID allocation is sequential and repo-scoped.
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Workbench.Core;

public static class WorkItemService
{
    private const int MaxSlugLength = 80;
    private const string CanonicalAddressesPlaceholder = "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>";
    private const string CanonicalDesignLinksPlaceholder = "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>";
    private const string CanonicalRelatedArtifactsPlaceholder = "- SPEC-<DOMAIN>[-<GROUPING>...]";

    /// <summary>
    /// Result payload returned by work item creation operations.
    /// </summary>
    /// <param name="Id">Assigned work item ID.</param>
    /// <param name="Slug">Generated slug for the title.</param>
    /// <param name="Path">Absolute path to the created item.</param>
    public sealed record WorkItemResult(string Id, string Slug, string Path);

    /// <summary>
    /// Result payload returned when listing work items.
    /// </summary>
    /// <param name="Items">List of loaded work items.</param>
    public sealed record WorkItemListResult(IList<WorkItem> Items);

    /// <summary>
    /// Result payload returned by structured work item edits.
    /// </summary>
    /// <param name="Item">Updated work item.</param>
    /// <param name="OriginalPath">Original absolute path before any rename.</param>
    /// <param name="PathChanged">True when the file path changed.</param>
    /// <param name="TitleUpdated">True when the title/front matter heading changed.</param>
    /// <param name="SummaryUpdated">True when the Summary section changed.</param>
    /// <param name="AcceptanceCriteriaUpdated">True when the Acceptance criteria section changed.</param>
    /// <param name="NotesAppended">True when a note was appended.</param>
    public sealed record WorkItemEditResult(
        WorkItem Item,
        string OriginalPath,
        bool PathChanged,
        bool TitleUpdated,
        bool SummaryUpdated,
        bool AcceptanceCriteriaUpdated,
        bool NotesAppended);

    public static WorkItemResult CreateItem(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? status,
        string? priority,
        string? owner)
    {
        if (ShouldUseCanonicalWorkItems(repoRoot) || SpecTraceMarkdown.GetCanonicalArtifactType(type) is "work_item")
        {
            var normalizedTitle = title.Trim();
            var domain = GetDefaultCanonicalDomain(repoRoot);
            var artifactId = AllocateCanonicalId(repoRoot, config, domain);
            var canonicalSlug = Slugify(normalizedTitle);
            var canonicalCreated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var canonicalStatus = NormalizeCanonicalStatus(status);
            var canonicalOwner = string.IsNullOrWhiteSpace(owner) ? "platform" : owner.Trim();
            var canonicalItemPath = SpecTraceLayout.GetWorkItemPath(repoRoot, domain, artifactId, normalizedTitle);

            if (File.Exists(canonicalItemPath))
            {
                throw new InvalidOperationException($"Work item already exists: {canonicalItemPath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(canonicalItemPath) ?? repoRoot);
            var addresses = CanonicalAddressesPlaceholder;
            var designLinks = CanonicalDesignLinksPlaceholder;
            var relatedArtifacts = CanonicalRelatedArtifactsPlaceholder;
            var body = SpecTraceMarkdown.BuildWorkItemBody(
                normalizedTitle,
                addresses,
                designLinks,
                artifactId: artifactId,
                relatedArtifacts: relatedArtifacts);
            body = AppendCompatibilityMetadata(
                body,
                BuildCompatibilityMetadata(
                    type,
                    priority,
                    canonicalCreated,
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>())));

            var data = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["artifact_id"] = artifactId,
                ["artifact_type"] = "work_item",
                ["title"] = normalizedTitle,
                ["domain"] = domain,
                ["status"] = canonicalStatus,
                ["owner"] = canonicalOwner,
                ["addresses"] = new List<string> { addresses },
                ["design_links"] = new List<string> { designLinks },
                ["related_artifacts"] = new List<string> { relatedArtifacts }
            };

            File.WriteAllText(canonicalItemPath, new FrontMatter(data, body).Serialize());
            return new WorkItemResult(artifactId, canonicalSlug, canonicalItemPath);
        }

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
            EnsureList(related, "branches");
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

    public static WorkItem CreateItemFromGithubIssue(
        string repoRoot,
        WorkbenchConfig config,
        GithubIssue issue,
        string type,
        string status,
        string? priority,
        string? owner)
    {
        if (ShouldUseCanonicalWorkItems(repoRoot) || SpecTraceMarkdown.GetCanonicalArtifactType(type) is "work_item")
        {
            var canonicalCreated = CreateItem(repoRoot, config, type, issue.Title, status, priority, owner);
            _ = UpdateItemFromGithubIssue(canonicalCreated.Path, issue, apply: true);
            return LoadItem(canonicalCreated.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        }

        var created = CreateItem(repoRoot, config, type, issue.Title, status, priority, owner);
        var content = File.ReadAllText(created.Path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
        data["githubSynced"] = FormatGithubSynced(DateTime.UtcNow);
        if (issue.Labels.Count > 0)
        {
            data["tags"] = issue.Labels.ToList();
        }

        var related = GetRelatedMap(data);
        if (related is not null)
        {
            var issues = EnsureList(related, "issues");
            AddUniqueLink(issues, issue.Url);

            var prs = EnsureList(related, "prs");
            foreach (var pr in issue.PullRequests)
            {
                AddUniqueLink(prs, pr);
            }

            EnsureList(related, "branches");
        }

        var summary = BuildIssueSummary(issue);
        var body = ReplaceSection(frontMatter.Body, "Summary", summary);
        frontMatter = new FrontMatter(data, body);
        File.WriteAllText(created.Path, frontMatter.Serialize());

        return LoadItem(created.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem ApplyDraft(string path, WorkItemDraft draft)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
        if (IsCanonicalWorkItemFrontMatter(data))
        {
            var canonicalSummary = draft.Summary?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalSummary))
            {
                throw new InvalidOperationException("Draft summary is empty.");
            }

            var canonicalBody = ReplaceSection(frontMatter.Body, "Summary", canonicalSummary);
            canonicalBody = ReplaceSection(canonicalBody, "Verification Plan", FormatAcceptanceCriteria(draft.AcceptanceCriteria));

            var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    draft.Type,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            if (draft.Tags is { Count: > 0 })
            {
                canonicalMetadata = canonicalMetadata with
                {
                    Tags = draft.Tags
                        .Select(tag => tag.Trim())
                        .Where(tag => tag.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
            }
            else
            {
                canonicalMetadata = canonicalMetadata with
                {
                    Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
            }

            canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
            frontMatter = new FrontMatter(data, canonicalBody);
            File.WriteAllText(path, frontMatter.Serialize());
            return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
        }

        if (draft.Tags is { Count: > 0 })
        {
            data["tags"] = draft.Tags
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var summary = draft.Summary?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new InvalidOperationException("Draft summary is empty.");
        }

        var criteria = FormatAcceptanceCriteria(draft.AcceptanceCriteria);
        var body = frontMatter.Body;
        body = ReplaceSection(body, "Summary", summary);
        body = ReplaceSection(body, "Acceptance criteria", criteria);
        frontMatter = new FrontMatter(data, body);
        File.WriteAllText(path, frontMatter.Serialize());

        return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem ApplyEditDraft(string path, WorkItemDraft draft)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
        if (IsCanonicalWorkItemFrontMatter(data))
        {
            var canonicalTitle = draft.Title?.Trim();
            if (!string.IsNullOrWhiteSpace(canonicalTitle))
            {
                data["title"] = canonicalTitle;
            }

            var canonicalSummary = draft.Summary?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalSummary))
            {
                throw new InvalidOperationException("Draft summary is empty.");
            }

            var canonicalBody = frontMatter.Body;
            if (!string.IsNullOrWhiteSpace(canonicalTitle))
            {
                canonicalBody = ReplaceTitleHeading(canonicalBody, GetString(data, "artifact_id") ?? string.Empty, canonicalTitle);
            }

            canonicalBody = ReplaceSection(canonicalBody, "Summary", canonicalSummary);
            canonicalBody = ReplaceSection(canonicalBody, "Verification Plan", FormatAcceptanceCriteria(draft.AcceptanceCriteria));

            var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    draft.Type,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            if (draft.Tags is { Count: > 0 })
            {
                canonicalMetadata = canonicalMetadata with
                {
                    Tags = draft.Tags
                        .Select(tag => tag.Trim())
                        .Where(tag => tag.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
            }
            else
            {
                canonicalMetadata = canonicalMetadata with
                {
                    Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };
            }

            canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
            frontMatter = new FrontMatter(data, canonicalBody);
            File.WriteAllText(path, frontMatter.Serialize());
            return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
        }

        var title = draft.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            data["title"] = title;
        }

        var summary = draft.Summary?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new InvalidOperationException("Draft summary is empty.");
        }

        var criteria = FormatAcceptanceCriteria(draft.AcceptanceCriteria);
        var body = frontMatter.Body;
        if (!string.IsNullOrWhiteSpace(title))
        {
            body = ReplaceTitleHeading(body, GetString(data, "id") ?? string.Empty, title);
        }
        body = ReplaceSection(body, "Summary", summary);
        body = ReplaceSection(body, "Acceptance criteria", criteria);
        frontMatter = new FrontMatter(data, body);
        File.WriteAllText(path, frontMatter.Serialize());

        return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItem UpdateItemFromGithubIssue(string path, GithubIssue issue, bool apply)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
        if (IsCanonicalWorkItemFrontMatter(data))
        {
            data["title"] = issue.Title;
            var canonicalBody = ReplaceTitleHeading(frontMatter.Body, GetString(data, "artifact_id") ?? string.Empty, issue.Title);
            canonicalBody = ReplaceSection(canonicalBody, "Summary", BuildIssueSummary(issue));

            var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    NormalizeCompatibilityType(GetString(data, "type")),
                    GetString(data, "priority"),
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalTags = issue.Labels
                .Select(label => label.Trim())
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (canonicalTags.Count > 0)
            {
                canonicalMetadata = canonicalMetadata with { Tags = canonicalTags };
            }

            var canonicalIssues = canonicalMetadata.Related.Issues
                .Concat(new[] { issue.Url })
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var canonicalPrs = canonicalMetadata.Related.Prs
                .Concat(issue.PullRequests)
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            canonicalMetadata = canonicalMetadata with
            {
                Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                GithubSynced = FormatGithubSynced(DateTime.UtcNow),
                Related = canonicalMetadata.Related with
                {
                    Issues = canonicalIssues,
                    Prs = canonicalPrs,
                    Branches = canonicalMetadata.Related.Branches.ToList()
                }
            };

            canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
            frontMatter = new FrontMatter(data, canonicalBody);

            if (apply)
            {
                File.WriteAllText(path, frontMatter.Serialize());
            }

            return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
        }

        data["title"] = issue.Title;
        data["githubSynced"] = FormatGithubSynced(DateTime.UtcNow);
        if (issue.Labels.Count > 0)
        {
            data["tags"] = issue.Labels.ToList();
        }

        var related = GetRelatedMap(data);
        if (related is null)
        {
            related = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            data["related"] = related;
        }

        var issues = EnsureList(related, "issues");
        AddUniqueLink(issues, issue.Url);

        var prs = EnsureList(related, "prs");
        foreach (var pr in issue.PullRequests)
        {
            AddUniqueLink(prs, pr);
        }

        EnsureList(related, "branches");

        var summary = BuildIssueSummary(issue);
        var body = frontMatter.Body;
        body = ReplaceTitleHeading(body, GetString(data, "id") ?? string.Empty, issue.Title);
        body = ReplaceSection(body, "Summary", summary);
        frontMatter = new FrontMatter(data, body);

        if (apply)
        {
            File.WriteAllText(path, frontMatter.Serialize());
        }

        return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
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

    public static async Task<int> SyncIssueLinksAsync(string repoRoot, WorkbenchConfig config, IEnumerable<WorkItem> items, bool dryRun)
    {
        var itemsWithIssues = items.Where(item => item.Related.Issues.Count > 0).ToList();
        if (itemsWithIssues.Count == 0)
        {
            return 0;
        }

        var defaultRepo = GithubService.ResolveRepo(repoRoot, config);
        var issueRefs = new Dictionary<string, GithubIssueRef>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in itemsWithIssues)
        {
            foreach (var entry in item.Related.Issues.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var issueRef = GithubService.ParseIssueReference(entry, defaultRepo);
                var key = $"{issueRef.Repo.Display}#{issueRef.Number.ToString(CultureInfo.InvariantCulture)}";
                if (!issueRefs.ContainsKey(key))
                {
                    issueRefs[key] = issueRef;
                }
            }
        }

        var issueCache = new ConcurrentDictionary<string, GithubIssue>(StringComparer.OrdinalIgnoreCase);
        if (issueRefs.Count > 0)
        {
            var semaphore = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount, 2, 8));
            var tasks = issueRefs.Select(async entry =>
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var issue = await GithubService.FetchIssueAsync(repoRoot, config, entry.Value).ConfigureAwait(false);
                    issueCache[entry.Key] = issue;
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        var updatedCount = 0;

        foreach (var item in itemsWithIssues)
        {
            var content = await File.ReadAllTextAsync(item.Path).ConfigureAwait(false);
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                throw new InvalidOperationException($"Front matter error: {error}");
            }

            var data = frontMatter!.Data;
            var related = GetRelatedMap(data);
            if (related is null)
            {
                related = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                data["related"] = related;
            }

            var issues = EnsureList(related, "issues");
            var prs = EnsureList(related, "prs");
            var changed = false;

            foreach (var entry in item.Related.Issues.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var issueRef = GithubService.ParseIssueReference(entry, defaultRepo);
                var key = $"{issueRef.Repo.Display}#{issueRef.Number.ToString(CultureInfo.InvariantCulture)}";
                if (!issueCache.TryGetValue(key, out var issue))
                {
                    issue = await GithubService.FetchIssueAsync(repoRoot, config, issueRef).ConfigureAwait(false);
                    issueCache[key] = issue;
                }

                if (AddUniqueLink(issues, issue.Url))
                {
                    changed = true;
                }

                foreach (var pr in issue.PullRequests)
                {
                    if (AddUniqueLink(prs, pr))
                    {
                        changed = true;
                    }
                }
            }

            if (changed && !dryRun)
            {
                data["githubSynced"] = FormatGithubSynced(DateTime.UtcNow);
                await File.WriteAllTextAsync(item.Path, frontMatter.Serialize()).ConfigureAwait(false);
                updatedCount++;
            }
        }

        return updatedCount;
    }

    public static int NormalizeRelatedLinks(string repoRoot, WorkbenchConfig config, bool includeDone, bool dryRun)
    {
        var updated = 0;
        var items = ListItems(repoRoot, config, includeDone).Items;
        foreach (var item in items)
        {
            var content = File.ReadAllText(item.Path);
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                throw new InvalidOperationException($"Front matter error: {error}");
            }

            var data = frontMatter!.Data;
            if (IsCanonicalWorkItemFrontMatter(data))
            {
                var canonicalBody = frontMatter.Body;
                var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                    ? parsedMetadata
                    : BuildCompatibilityMetadata(
                        null,
                        null,
                        DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        null,
                        new List<string>(),
                        null,
                        new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

                var canonicalChanged = false;
                var canonicalRelated = canonicalMetadata.Related;
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "specs");
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "adrs");
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "files");
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "prs");
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "issues");
                canonicalChanged |= NormalizeCompatibilityRelatedList(ref canonicalRelated, "branches");

                if (canonicalChanged && !dryRun)
                {
                    canonicalMetadata = canonicalMetadata with
                    {
                        Related = canonicalRelated,
                        Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    };
                    canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
                    frontMatter = new FrontMatter(data, canonicalBody);
                    File.WriteAllText(item.Path, frontMatter.Serialize());
                    updated++;
                }

                continue;
            }

            var related = GetRelatedMap(data);
            if (related is null)
            {
                continue;
            }

            var changed = false;
            changed |= NormalizeList(related, "specs");
            changed |= NormalizeList(related, "adrs");
            changed |= NormalizeList(related, "files");
            changed |= NormalizeList(related, "prs");
            changed |= NormalizeList(related, "issues");
            changed |= NormalizeList(related, "branches");

            if (changed && !dryRun)
            {
                File.WriteAllText(item.Path, frontMatter.Serialize());
                updated++;
            }
        }

        return updated;
    }

    public static int NormalizeItems(string repoRoot, WorkbenchConfig config, bool includeDone, bool dryRun)
    {
        var updated = 0;
        var items = ListItems(repoRoot, config, includeDone).Items;
        foreach (var item in items)
        {
            var content = File.ReadAllText(item.Path);
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                throw new InvalidOperationException($"Front matter error: {error}");
            }

            var data = frontMatter!.Data;
            if (IsCanonicalWorkItemFrontMatter(data))
            {
                var canonicalChanged = false;

                if (string.IsNullOrWhiteSpace(GetString(data, "artifact_type")))
                {
                    data["artifact_type"] = "work_item";
                    canonicalChanged = true;
                }

                if (string.IsNullOrWhiteSpace(GetString(data, "title")))
                {
                    data["title"] = item.Title;
                    canonicalChanged = true;
                }

                var normalizedStatus = NormalizeCanonicalStatus(GetString(data, "status"));
                if (!string.Equals(GetString(data, "status"), normalizedStatus, StringComparison.Ordinal))
                {
                    data["status"] = normalizedStatus;
                    canonicalChanged = true;
                }

                var currentDomain = GetString(data, "domain");
                var normalizedDomain = string.IsNullOrWhiteSpace(currentDomain)
                    ? GetDefaultCanonicalDomain(repoRoot)
                    : ArtifactIdPolicy.NormalizeToken(currentDomain);
                if (!string.Equals(currentDomain, normalizedDomain, StringComparison.Ordinal))
                {
                    data["domain"] = normalizedDomain;
                    canonicalChanged = true;
                }

                var currentOwner = GetString(data, "owner");
                if (string.IsNullOrWhiteSpace(currentOwner))
                {
                    data["owner"] = "platform";
                    canonicalChanged = true;
                }

                canonicalChanged |= NormalizeCanonicalList(data, "addresses", "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
                canonicalChanged |= NormalizeCanonicalList(data, "design_links", "ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
                canonicalChanged |= NormalizeCanonicalList(data, "related_artifacts", "SPEC-<DOMAIN>[-<GROUPING>...]");

                var canonicalNormalizedBody = NormalizeBody(item.ArtifactId, item.Title, frontMatter.Body, out var canonicalBodyChanged);
                if (canonicalBodyChanged)
                {
                    frontMatter = new FrontMatter(data, canonicalNormalizedBody);
                    canonicalChanged = true;
                }

                if (canonicalChanged && !canonicalBodyChanged)
                {
                    frontMatter = new FrontMatter(data, frontMatter.Body);
                }

                if (canonicalChanged && !dryRun)
                {
                    File.WriteAllText(item.Path, frontMatter.Serialize());
                    updated++;
                }

                continue;
            }

            var changed = NormalizeTags(data);
            if (string.IsNullOrWhiteSpace(GetString(data, "title")))
            {
                data["title"] = item.Title;
                changed = true;
            }
            if (!data.ContainsKey("githubSynced"))
            {
                data["githubSynced"] = null;
                changed = true;
            }
            changed |= EnsureRelatedLists(data);
            var normalizedBody = NormalizeBody(item.Id, item.Title, frontMatter.Body, out var bodyChanged);
            if (bodyChanged)
            {
                frontMatter = new FrontMatter(data, normalizedBody);
                changed = true;
            }
            var related = GetRelatedMap(data);
            if (related is not null)
            {
                changed |= NormalizeList(related, "specs");
                changed |= NormalizeList(related, "adrs");
                changed |= NormalizeList(related, "files");
                changed |= NormalizeList(related, "prs");
                changed |= NormalizeList(related, "issues");
                changed |= NormalizeList(related, "branches");
            }

            if (changed && !dryRun)
            {
                File.WriteAllText(item.Path, frontMatter.Serialize());
                updated++;
            }
        }

        return updated;
    }

    private static string NormalizeBody(string id, string title, string body, out bool changed)
    {
        var normalizedInput = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalizedInput.Split('\n').ToList();

        var headingIndex = lines.FindIndex(line => line.StartsWith("# ", StringComparison.Ordinal));
        var startIndex = headingIndex >= 0 ? headingIndex + 1 : 0;

        var leading = new List<string>();
        var sections = new List<BodySection>();
        BodySection? current = null;
        var sawSection = false;

        for (var i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    sections.Add(current);
                }

                current = new BodySection(line[3..].Trim(), new List<string>());
                sawSection = true;
                continue;
            }

            if (sawSection)
            {
                current?.Lines.Add(line);
            }
            else
            {
                leading.Add(line);
            }
        }

        if (current is not null)
        {
            sections.Add(current);
        }

        var canonicalOrder = new[]
        {
            "Summary",
            "Requirements Addressed",
            "Design Inputs",
            "Planned Changes",
            "Out of Scope",
            "Verification Plan",
            "Completion Notes",
            "Trace Links"
        };

        var canonicalSections = canonicalOrder.ToDictionary(
            heading => heading,
            _ => new List<List<string>>(),
            StringComparer.OrdinalIgnoreCase);
        var extras = new List<BodySection>();

        void AddCanonicalBlock(string heading, IEnumerable<string> block)
        {
            var normalizedBlock = TrimBlock(block);
            if (normalizedBlock.Count == 0)
            {
                return;
            }

            canonicalSections[heading].Add(normalizedBlock);
        }

        foreach (var section in sections)
        {
            if (TryMapCanonicalHeading(section.Heading, out var canonicalHeading))
            {
                AddCanonicalBlock(canonicalHeading, section.Lines);
                continue;
            }

            extras.Add(new BodySection(section.Heading, TrimBlock(section.Lines)));
        }

        var leadingBlock = TrimBlock(leading);
        if (leadingBlock.Count > 0)
        {
            AddCanonicalBlock("Summary", leadingBlock);
        }

        var output = new List<string>();
        var heading = string.IsNullOrWhiteSpace(title)
            ? id
            : $"{id} - {title.Trim()}";
        output.Add($"# {heading}");
        output.Add(string.Empty);

        foreach (var sectionHeading in canonicalOrder)
        {
            AppendSection(output, sectionHeading, BuildCanonicalSectionContent(sectionHeading, canonicalSections[sectionHeading]));
        }

        foreach (var section in extras)
        {
            AppendSection(output, section.Heading, section.Lines.Count > 0 ? section.Lines : new[] { "-" });
        }

        TrimTrailingBlankLines(output);
        var result = string.Join("\n", output);
        changed = !string.Equals(result, normalizedInput.TrimEnd(), StringComparison.Ordinal);
        return result;
    }

    private static void AppendSection(List<string> output, string heading, IEnumerable<string> content)
    {
        if (output.Count > 0 && !string.IsNullOrWhiteSpace(output[^1]))
        {
            output.Add(string.Empty);
        }

        output.Add($"## {heading}");
        output.Add(string.Empty);
        output.AddRange(content);
    }

    private static List<string> BuildCanonicalSectionContent(string heading, List<List<string>> blocks)
    {
        if (blocks.Count == 0)
        {
            return heading switch
            {
                "Trace Links" => new List<string>
                {
                "Addresses:",
                "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
                "Uses Design:",
                "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"
            },
                _ => new List<string> { "-" }
            };
        }

        var content = new List<string>();
        foreach (var block in blocks)
        {
            if (content.Count > 0 && !string.IsNullOrWhiteSpace(content[^1]))
            {
                content.Add(string.Empty);
            }

            content.AddRange(block);
        }

        return content;
    }

    private static List<string> TrimBlock(IEnumerable<string> lines)
    {
        var block = lines.ToList();
        while (block.Count > 0 && string.IsNullOrWhiteSpace(block[0]))
        {
            block.RemoveAt(0);
        }

        while (block.Count > 0 && string.IsNullOrWhiteSpace(block[^1]))
        {
            block.RemoveAt(block.Count - 1);
        }

        return block;
    }

    private static void TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private static bool TryMapCanonicalHeading(string heading, out string canonicalHeading)
    {
        var normalized = heading.Trim();
        if (normalized.Equals("Summary", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Summary";
            return true;
        }

        if (normalized.Equals("Requirements Addressed", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Requirements Addressed";
            return true;
        }

        if (normalized.Equals("Design Inputs", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Design Inputs";
            return true;
        }

        if (normalized.Equals("Planned Changes", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Planned Changes";
            return true;
        }

        if (normalized.Equals("Out of Scope", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Out of Scope";
            return true;
        }

        if (normalized.Equals("Verification Plan", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Verification Plan";
            return true;
        }

        if (normalized.Equals("Completion Notes", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Completion Notes";
            return true;
        }

        if (normalized.Equals("Trace Links", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Trace Links";
            return true;
        }

        if (normalized.Equals("Context", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Design Inputs";
            return true;
        }

        if (normalized.Equals("Traceability", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Trace Links";
            return true;
        }

        if (normalized.Equals("Implementation notes", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Implementation Notes", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Planned Changes";
            return true;
        }

        if (normalized.Equals("Acceptance criteria", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Acceptance Criteria", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Success criteria", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Success Criteria", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Verification Plan";
            return true;
        }

        if (normalized.Equals("Notes", StringComparison.OrdinalIgnoreCase))
        {
            canonicalHeading = "Completion Notes";
            return true;
        }

        canonicalHeading = string.Empty;
        return false;
    }

    public static WorkItem? LoadItem(string path)
    {
        if (Path.GetFileName(path).Equals("README.md", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            return null;
        }

        var data = frontMatter!.Data;
        if (IsCanonicalWorkItemFrontMatter(data))
        {
            var canonicalArtifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId") ?? string.Empty;
            var canonicalStatus = GetString(data, "status") ?? string.Empty;
            var canonicalTitle = GetString(data, "title") ?? DeriveTitleFromPath(path, canonicalArtifactId);
            var canonicalOwner = GetString(data, "owner");
            var canonicalDomain = GetString(data, "domain") ?? string.Empty;
            var canonicalAddresses = GetStringList(data, "addresses");
            var canonicalDesignLinks = GetStringList(data, "design_links");
            var canonicalVerificationLinks = GetStringList(data, "verification_links");
            var canonicalRelatedArtifacts = GetStringList(data, "related_artifacts");

            if (string.IsNullOrWhiteSpace(canonicalArtifactId) ||
                string.IsNullOrWhiteSpace(canonicalStatus) ||
                string.IsNullOrWhiteSpace(canonicalTitle) ||
                string.IsNullOrWhiteSpace(canonicalDomain))
            {
                return null;
            }

            var canonicalCompatibility = TryParseCompatibilityMetadata(frontMatter.Body, out var parsedCompatibility)
                ? parsedCompatibility
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalCreated = string.IsNullOrWhiteSpace(canonicalCompatibility.Created)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : canonicalCompatibility.Created;
            var canonicalUpdated = canonicalCompatibility.Updated;
            var canonicalTags = canonicalCompatibility.Tags.Count > 0 ? canonicalCompatibility.Tags : GetStringList(data, "tags");
            var canonicalRelated = FromRelatedLinksPayload(canonicalCompatibility.Related);
            var canonicalSlug = DeriveSlugFromPath(path, canonicalArtifactId);

            return new WorkItem(
                canonicalArtifactId,
                canonicalCompatibility.Type,
                canonicalStatus,
                canonicalTitle,
                canonicalCompatibility.Priority,
                canonicalOwner,
                canonicalCreated,
                canonicalUpdated,
                canonicalTags,
                canonicalRelated,
                canonicalSlug,
                path,
                frontMatter.Body)
            {
                ArtifactId = canonicalArtifactId,
                ArtifactType = "work_item",
                ArtifactStatus = canonicalStatus,
                Domain = canonicalDomain,
                Addresses = canonicalAddresses,
                DesignLinks = canonicalDesignLinks,
                VerificationLinks = canonicalVerificationLinks,
                RelatedArtifacts = canonicalRelatedArtifacts,
                GithubSynced = canonicalCompatibility.GithubSynced
            };
        }

        var id = GetString(data, "id") ?? "";
        var type = GetString(data, "type") ?? "";
        var status = GetString(data, "status") ?? "";
        var title = GetString(data, "title") ?? DeriveTitleFromPath(path, id);
        var priority = GetString(data, "priority");
        var owner = GetString(data, "owner");
        var created = GetString(data, "created") ?? "";
        var updated = GetString(data, "updated");

        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(type) ||
            string.IsNullOrWhiteSpace(status) ||
            string.IsNullOrWhiteSpace(created))
        {
            return null;
        }

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
            frontMatter.Body)
        {
            ArtifactId = id,
            ArtifactType = "work_item",
            ArtifactStatus = status,
            Domain = GetString(data, "domain") ?? string.Empty,
            Addresses = new List<string>(),
            DesignLinks = new List<string>(),
            VerificationLinks = new List<string>(),
            RelatedArtifacts = GetStringList(data, "related_artifacts"),
            GithubSynced = GetString(data, "githubSynced")
        };
    }

    public static WorkItem UpdateStatus(string path, string status, string? note)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            frontMatter.Data["status"] = NormalizeCanonicalStatus(status);
            var canonicalBody = frontMatter.Body;
            if (!string.IsNullOrWhiteSpace(note))
            {
                canonicalBody = AppendNote(canonicalBody, note, "Completion Notes");
            }

            var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            canonicalMetadata = canonicalMetadata with
            {
                Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
            frontMatter = new FrontMatter(frontMatter.Data, canonicalBody);
            File.WriteAllText(path, frontMatter.Serialize());
            return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
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
        var content = File.ReadAllText(path);
        if (FrontMatter.TryParse(content, out var frontMatter, out _) &&
            IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            return UpdateStatus(path, "complete", null);
        }

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

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            frontMatter.Data["title"] = newTitle;
            var canonicalBody = ReplaceTitleHeading(frontMatter.Body, item.ArtifactId, newTitle);
            var canonicalMetadata = TryParseCompatibilityMetadata(canonicalBody, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    item.Type,
                    item.Priority,
                    item.Created,
                    item.Updated,
                    item.Tags,
                    item.GithubSynced,
                    item.Related);
            canonicalMetadata = canonicalMetadata with
            {
                Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            canonicalBody = AppendCompatibilityMetadata(canonicalBody, canonicalMetadata);
            frontMatter = new FrontMatter(frontMatter.Data, canonicalBody);
            File.WriteAllText(path, frontMatter.Serialize());

            var canonicalCurrentDir = Path.GetDirectoryName(path) ?? repoRoot;
            var canonicalNewPath = Path.Combine(canonicalCurrentDir, $"{Slugify(newTitle)}.md");
            if (!string.Equals(path, canonicalNewPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(path, canonicalNewPath, overwrite: false);
                return LoadItem(canonicalNewPath) ?? throw new InvalidOperationException("Failed to reload work item.");
            }

            return LoadItem(path) ?? throw new InvalidOperationException("Failed to reload work item.");
        }

        var slug = Slugify(newTitle);
        var newFileName = $"{item.Id}-{slug}.md";
        var currentDir = Path.GetDirectoryName(path) ?? repoRoot;
        var newPath = Path.Combine(currentDir, newFileName);
        frontMatter!.Data["title"] = newTitle;
        frontMatter.Data["updated"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var body = ReplaceTitleHeading(frontMatter.Body, item.Id, newTitle);
        frontMatter = new FrontMatter(frontMatter.Data, body);
        File.WriteAllText(path, frontMatter.Serialize());

        File.Move(path, newPath, overwrite: false);
        return LoadItem(newPath) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static WorkItemEditResult EditItem(
        string path,
        string? title,
        string? summary,
        IEnumerable<string>? acceptanceCriteria,
        string? appendNote,
        bool renameFile,
        WorkbenchConfig config,
        string repoRoot,
        string? status = null,
        string? priority = null,
        string? owner = null)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var originalPath = path;
        var data = frontMatter!.Data;
        var itemId = GetString(data, "id") ?? string.Empty;
        var body = frontMatter.Body;

        if (IsCanonicalWorkItemFrontMatter(data))
        {
            var canonicalArtifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId") ?? string.Empty;
            var canonicalTitle = GetString(data, "title") ?? string.Empty;
            var canonicalOwner = GetString(data, "owner");
            var canonicalMetadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    GetString(data, "priority"),
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalTitleUpdated = false;
            var canonicalSummaryUpdated = false;
            var canonicalAcceptanceCriteriaUpdated = false;
            var canonicalNotesAppended = false;
            var canonicalMetadataUpdated = false;

            var canonicalNormalizedTitle = title?.Trim();
            if (!string.IsNullOrWhiteSpace(canonicalNormalizedTitle) &&
                !string.Equals(canonicalTitle, canonicalNormalizedTitle, StringComparison.Ordinal))
            {
                data["title"] = canonicalNormalizedTitle;
                body = ReplaceTitleHeading(body, canonicalArtifactId, canonicalNormalizedTitle);
                canonicalTitleUpdated = true;
            }

            var canonicalNormalizedStatus = status?.Trim();
            if (!string.IsNullOrWhiteSpace(canonicalNormalizedStatus))
            {
                var canonicalStatus = NormalizeCanonicalStatus(canonicalNormalizedStatus);
                var currentStatus = GetString(data, "status") ?? string.Empty;
                if (!string.Equals(currentStatus, canonicalStatus, StringComparison.Ordinal))
                {
                    data["status"] = canonicalStatus;
                    canonicalMetadataUpdated = true;
                }
            }

            if (priority is not null)
            {
                var normalizedPriority = priority.Trim();
                if (!string.Equals(canonicalMetadata.Priority ?? string.Empty, normalizedPriority, StringComparison.Ordinal))
                {
                    canonicalMetadata = canonicalMetadata with { Priority = string.IsNullOrWhiteSpace(normalizedPriority) ? null : normalizedPriority };
                    canonicalMetadataUpdated = true;
                }
            }

            if (owner is not null)
            {
                var normalizedOwner = owner.Trim();
                if (!string.IsNullOrWhiteSpace(normalizedOwner) &&
                    !string.Equals(canonicalOwner, normalizedOwner, StringComparison.Ordinal))
                {
                    data["owner"] = normalizedOwner;
                    canonicalMetadataUpdated = true;
                }
            }

            if (summary is not null)
            {
                var normalizedSummary = summary.Trim();
                if (string.IsNullOrWhiteSpace(normalizedSummary))
                {
                    throw new InvalidOperationException("Summary cannot be empty.");
                }

                body = ReplaceSection(body, "Summary", normalizedSummary);
                canonicalSummaryUpdated = true;
            }

            if (acceptanceCriteria is not null)
            {
                body = ReplaceSection(body, "Verification Plan", FormatAcceptanceCriteria(acceptanceCriteria));
                canonicalAcceptanceCriteriaUpdated = true;
            }

            if (!string.IsNullOrWhiteSpace(appendNote))
            {
                body = AppendNote(body, appendNote.Trim(), "Completion Notes");
                canonicalNotesAppended = true;
            }

            if (!canonicalTitleUpdated && !canonicalSummaryUpdated && !canonicalAcceptanceCriteriaUpdated && !canonicalNotesAppended && !canonicalMetadataUpdated)
            {
                throw new InvalidOperationException("No work item edits were requested.");
            }

            canonicalMetadata = canonicalMetadata with
            {
                Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            body = AppendCompatibilityMetadata(body, canonicalMetadata);
            frontMatter = new FrontMatter(data, body);
            File.WriteAllText(path, frontMatter.Serialize());

            var canonicalFinalPath = path;
            var canonicalPathChanged = false;
            if (renameFile && canonicalTitleUpdated && !string.IsNullOrWhiteSpace(canonicalNormalizedTitle))
            {
                var canonicalCurrentDir = Path.GetDirectoryName(path) ?? repoRoot;
                var canonicalNewPath = Path.Combine(canonicalCurrentDir, $"{Slugify(canonicalNormalizedTitle)}.md");
                if (!string.Equals(path, canonicalNewPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(path, canonicalNewPath, overwrite: false);
                    canonicalFinalPath = canonicalNewPath;
                    canonicalPathChanged = true;
                }
            }

            var canonicalReloadedItem = LoadItem(canonicalFinalPath) ?? throw new InvalidOperationException("Failed to reload work item.");
            return new WorkItemEditResult(
                canonicalReloadedItem,
                originalPath,
                canonicalPathChanged,
                canonicalTitleUpdated,
                canonicalSummaryUpdated,
                canonicalAcceptanceCriteriaUpdated,
                canonicalNotesAppended);
        }

        var titleUpdated = false;
        var summaryUpdated = false;
        var acceptanceCriteriaUpdated = false;
        var notesAppended = false;
        var metadataUpdated = false;

        var normalizedTitle = title?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            var currentTitle = GetString(data, "title") ?? string.Empty;
            if (!string.Equals(currentTitle, normalizedTitle, StringComparison.Ordinal))
            {
                data["title"] = normalizedTitle;
                body = ReplaceTitleHeading(body, itemId, normalizedTitle);
                titleUpdated = true;
            }
        }

        var normalizedStatus = status?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var currentStatus = GetString(data, "status") ?? string.Empty;
            if (!string.Equals(currentStatus, normalizedStatus, StringComparison.Ordinal))
            {
                data["status"] = normalizedStatus;
                metadataUpdated = true;
            }
        }

        if (priority is not null)
        {
            var normalizedPriority = priority.Trim();
            var currentPriority = GetString(data, "priority");
            if (string.IsNullOrWhiteSpace(normalizedPriority))
            {
                if (currentPriority is not null)
                {
                    data.Remove("priority");
                    metadataUpdated = true;
                }
            }
            else if (!string.Equals(currentPriority, normalizedPriority, StringComparison.Ordinal))
            {
                data["priority"] = normalizedPriority;
                metadataUpdated = true;
            }
        }

        if (owner is not null)
        {
            var normalizedOwner = owner.Trim();
            var currentOwner = GetString(data, "owner");
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                if (currentOwner is not null)
                {
                    data.Remove("owner");
                    metadataUpdated = true;
                }
            }
            else if (!string.Equals(currentOwner, normalizedOwner, StringComparison.Ordinal))
            {
                data["owner"] = normalizedOwner;
                metadataUpdated = true;
            }
        }

        if (summary is not null)
        {
            var normalizedSummary = summary.Trim();
            if (string.IsNullOrWhiteSpace(normalizedSummary))
            {
                throw new InvalidOperationException("Summary cannot be empty.");
            }

            body = ReplaceSection(body, "Summary", normalizedSummary);
            summaryUpdated = true;
        }

        if (acceptanceCriteria is not null)
        {
            body = ReplaceSection(body, "Acceptance criteria", FormatAcceptanceCriteria(acceptanceCriteria));
            acceptanceCriteriaUpdated = true;
        }

        if (!string.IsNullOrWhiteSpace(appendNote))
        {
            body = AppendNote(body, appendNote.Trim());
            notesAppended = true;
        }

        if (!titleUpdated && !summaryUpdated && !acceptanceCriteriaUpdated && !notesAppended && !metadataUpdated)
        {
            throw new InvalidOperationException("No work item edits were requested.");
        }

        data["updated"] = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        frontMatter = new FrontMatter(data, body);
        File.WriteAllText(path, frontMatter.Serialize());

        var finalPath = path;
        var pathChanged = false;
        if (renameFile && titleUpdated && !string.IsNullOrWhiteSpace(normalizedTitle))
        {
            var slug = Slugify(normalizedTitle);
            var newFileName = $"{itemId}-{slug}.md";
            var currentDir = Path.GetDirectoryName(path) ?? repoRoot;
            var newPath = Path.Combine(currentDir, newFileName);
            if (!string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(path, newPath, overwrite: false);
                finalPath = newPath;
                pathChanged = true;
            }
        }

        var item = LoadItem(finalPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        return new WorkItemEditResult(
            item,
            originalPath,
            pathChanged,
            titleUpdated,
            summaryUpdated,
            acceptanceCriteriaUpdated,
            notesAppended);
    }

    public static void AddPrLink(string path, string prUrl)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            var body = frontMatter.Body;
            var metadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalPrs = GetRelatedList(metadata.Related, "prs");
            if (!canonicalPrs.Any(link => link.Equals(prUrl, StringComparison.OrdinalIgnoreCase)))
            {
                canonicalPrs.Add(prUrl);
            }

            metadata = metadata with
            {
                Related = SetRelatedList(metadata.Related, "prs", canonicalPrs)
            };
            body = AppendCompatibilityMetadata(body, metadata);
            frontMatter = new FrontMatter(frontMatter.Data, body);
            File.WriteAllText(path, frontMatter.Serialize());
            return;
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

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            var body = frontMatter.Body;
            var metadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalList = GetRelatedList(metadata.Related, key);
            var canonicalNormalizedLink = NormalizeRelatedLink(link);
            var canonicalAdded = false;
            for (var index = 0; index < canonicalList.Count; index++)
            {
                var normalizedEntry = NormalizeRelatedLink(canonicalList[index]);
                if (normalizedEntry.Equals(canonicalNormalizedLink, StringComparison.OrdinalIgnoreCase))
                {
                    if (!canonicalList[index].Equals(canonicalNormalizedLink, StringComparison.OrdinalIgnoreCase))
                    {
                        canonicalList[index] = canonicalNormalizedLink;
                        canonicalAdded = true;
                    }

                    if (apply && canonicalAdded)
                    {
                        metadata = metadata with { Related = SetRelatedList(metadata.Related, key, canonicalList) };
                        body = AppendCompatibilityMetadata(body, metadata);
                        frontMatter = new FrontMatter(frontMatter.Data, body);
                        File.WriteAllText(path, frontMatter.Serialize());
                    }

                    return canonicalAdded;
                }
            }

            canonicalList.Add(canonicalNormalizedLink);
            if (apply)
            {
                metadata = metadata with { Related = SetRelatedList(metadata.Related, key, canonicalList) };
                body = AppendCompatibilityMetadata(body, metadata);
                frontMatter = new FrontMatter(frontMatter.Data, body);
                File.WriteAllText(path, frontMatter.Serialize());
            }
            return true;
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var list = EnsureList(related, key);
        var normalizedLink = NormalizeRelatedLink(link);
        for (var index = 0; index < list.Count; index++)
        {
            var entry = list[index]?.ToString();
            if (entry is null)
            {
                continue;
            }

            var normalizedEntry = NormalizeRelatedLink(entry);
            if (normalizedEntry.Equals(normalizedLink, StringComparison.OrdinalIgnoreCase))
            {
                if (!entry.Equals(normalizedLink, StringComparison.OrdinalIgnoreCase))
                {
                    list[index] = normalizedLink;
                    if (apply)
                    {
                        File.WriteAllText(path, frontMatter.Serialize());
                    }
                    return true;
                }
                return false;
            }
        }

        list.Add(normalizedLink);
        if (apply)
        {
            File.WriteAllText(path, frontMatter.Serialize());
        }
        return true;
    }

    public static bool RemoveRelatedLink(string path, string key, string link, bool apply = true)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            var body = frontMatter.Body;
            var metadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalList = GetRelatedList(metadata.Related, key);
            var canonicalNormalizedLink = NormalizeRelatedLink(link);
            var canonicalBefore = canonicalList.Count;
            canonicalList.RemoveAll(entry =>
            {
                var canonicalValue = entry?.ToString();
                return canonicalValue is not null &&
                       NormalizeRelatedLink(canonicalValue).Equals(canonicalNormalizedLink, StringComparison.OrdinalIgnoreCase);
            });

            if (canonicalList.Count != canonicalBefore)
            {
                if (apply)
                {
                    metadata = metadata with { Related = SetRelatedList(metadata.Related, key, canonicalList) };
                    body = AppendCompatibilityMetadata(body, metadata);
                    frontMatter = new FrontMatter(frontMatter.Data, body);
                    File.WriteAllText(path, frontMatter.Serialize());
                }
                return true;
            }

            return false;
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var list = EnsureList(related, key);
        var normalizedLink = NormalizeRelatedLink(link);
        var before = list.Count;
        list.RemoveAll(entry =>
        {
            var value = entry?.ToString();
            return value is not null
                && NormalizeRelatedLink(value).Equals(normalizedLink, StringComparison.OrdinalIgnoreCase);
        });
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

    public static bool ReplaceRelatedLinks(string path, IDictionary<string, string> replacements, bool apply = true)
    {
        if (replacements.Count == 0)
        {
            return false;
        }

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            var body = frontMatter.Body;
            var metadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalChanged = false;
            var canonicalRelated = metadata.Related;
            foreach (var key in new[] { "specs", "adrs", "files" })
            {
                var list = GetRelatedList(canonicalRelated, key);
                for (var index = list.Count - 1; index >= 0; index--)
                {
                    var entry = list[index];
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    var normalizedEntry = NormalizeRelatedLink(entry);
                    if (!replacements.TryGetValue(normalizedEntry, out var replacement))
                    {
                        continue;
                    }

                    var normalizedReplacement = NormalizeRelatedLink(replacement);
                    var alreadyPresent = list.Any(candidate =>
                        NormalizeRelatedLink(candidate).Equals(normalizedReplacement, StringComparison.OrdinalIgnoreCase));

                    if (alreadyPresent)
                    {
                        list.RemoveAt(index);
                    }
                    else
                    {
                        list[index] = normalizedReplacement;
                    }

                    canonicalRelated = SetRelatedList(canonicalRelated, key, list);
                    canonicalChanged = true;
                }
            }

            if (canonicalChanged && apply)
            {
                metadata = metadata with { Related = canonicalRelated };
                body = AppendCompatibilityMetadata(body, metadata);
                frontMatter = new FrontMatter(frontMatter.Data, body);
                File.WriteAllText(path, frontMatter.Serialize());
            }

            return canonicalChanged;
        }

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var changed = false;
        foreach (var key in new[] { "specs", "adrs", "files" })
        {
            var list = EnsureList(related, key);
            for (var index = list.Count - 1; index >= 0; index--)
            {
                var entry = list[index]?.ToString();
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var normalizedEntry = NormalizeRelatedLink(entry);
                if (!replacements.TryGetValue(normalizedEntry, out var replacement))
                {
                    continue;
                }

                var normalizedReplacement = NormalizeRelatedLink(replacement);
                var alreadyPresent = list.Any(item =>
                {
                    var value = item?.ToString();
                    return value is not null && NormalizeRelatedLink(value).Equals(normalizedReplacement, StringComparison.OrdinalIgnoreCase);
                });

                if (alreadyPresent)
                {
                    list.RemoveAt(index);
                }
                else
                {
                    list[index] = normalizedReplacement;
                }
                changed = true;
            }
        }

        if (changed && apply)
        {
            File.WriteAllText(path, frontMatter.Serialize());
        }

        return changed;
    }

    public static bool UpdateGithubSynced(string path, DateTime timestamp, bool apply = true)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (IsCanonicalWorkItemFrontMatter(frontMatter!.Data))
        {
            var body = frontMatter.Body;
            var metadata = TryParseCompatibilityMetadata(body, out var parsedMetadata)
                ? parsedMetadata
                : BuildCompatibilityMetadata(
                    null,
                    null,
                    DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    null,
                    new List<string>(),
                    null,
                    new RelatedLinks(
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>(),
                        new List<string>()));

            var canonicalValue = FormatGithubSynced(timestamp);
            if (string.Equals(metadata.GithubSynced, canonicalValue, StringComparison.Ordinal))
            {
                return false;
            }

            metadata = metadata with
            {
                GithubSynced = canonicalValue,
                Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            body = AppendCompatibilityMetadata(body, metadata);

            if (apply)
            {
                frontMatter = new FrontMatter(frontMatter.Data, body);
                File.WriteAllText(path, frontMatter.Serialize());
            }

            return true;
        }

        var data = frontMatter!.Data;
        var value = FormatGithubSynced(timestamp);
        var existing = GetString(data, "githubSynced");
        if (string.Equals(existing, value, StringComparison.Ordinal))
        {
            return false;
        }
        data["githubSynced"] = value;

        if (apply)
        {
            File.WriteAllText(path, frontMatter.Serialize());
        }
        return true;
    }

    public static string GetItemPathById(string repoRoot, WorkbenchConfig config, string id)
    {
        foreach (var item in ListItems(repoRoot, config, includeDone: true).Items)
        {
            if (item.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                item.ArtifactId.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                return item.Path;
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
        var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var max = 0;

        foreach (var item in ListItems(repoRoot, config, includeDone: true).Items)
        {
            var match = pattern.Match(item.Id);
            if (!match.Success)
            {
                continue;
            }
            if (int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var value))
            {
                max = Math.Max(max, value);
            }
        }

        var next = max + 1;
        return $"{prefix}-{next.ToString($"D{width}", CultureInfo.InvariantCulture)}";
    }

    private static string AllocateCanonicalId(string repoRoot, WorkbenchConfig config, string domain)
    {
        var policy = ArtifactIdPolicy.Load(repoRoot);
        var prefix = policy.BuildArtifactIdPrefix("work_item", domain, null);
        var max = 0;

        foreach (var item in ListItems(repoRoot, config, includeDone: true).Items)
        {
            if (!item.ArtifactId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = item.ArtifactId[prefix.Length..];
            if (ArtifactIdPolicy.TryParseSequence(suffix, out var sequence))
            {
                max = Math.Max(max, sequence);
            }
        }

        var next = max + 1;
        return policy.BuildArtifactId("work_item", domain, null, next);
    }

    public static string Slugify(string title)
    {
        var lowered = title.ToLowerInvariant();
        var cleaned = Regex.Replace(lowered, @"[^a-z0-9-\s]", "", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var dashed = Regex.Replace(cleaned, @"\s+", "-", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var collapsed = Regex.Replace(dashed, @"-+", "-", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var trimmed = collapsed.Trim('-');
        if (trimmed.Length <= MaxSlugLength)
        {
            return trimmed;
        }

        var shortened = trimmed[..MaxSlugLength].Trim('-');
        return shortened;
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
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return file;
            }
        }

        var canonicalRoot = Path.Combine(repoRoot, config.Paths.SpecsRoot, "work-items");
        if (!Directory.Exists(canonicalRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(canonicalRoot, "*.md", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return file;
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

    private static string FormatGithubSynced(DateTime timestamp)
    {
        return timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string AppendNote(string body, string note, string sectionHeading = "Notes")
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        var notesIndex = lines.FindIndex(line => line.Trim().Equals($"## {sectionHeading}", StringComparison.OrdinalIgnoreCase));
        if (notesIndex == -1)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }
            lines.Add($"## {sectionHeading}");
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

    private static string ReplaceSection(string body, string heading, string content)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        var start = lines.FindIndex(line => line.Trim().Equals($"## {heading}", StringComparison.OrdinalIgnoreCase));
        var contentLines = content.Replace("\r\n", "\n").Split('\n').ToList();
        if (start == -1)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }
            lines.Add($"## {heading}");
            if (!string.IsNullOrWhiteSpace(content))
            {
                lines.Add(string.Empty);
                lines.AddRange(contentLines);
            }
            return string.Join("\n", lines);
        }

        var end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith("## ", StringComparison.Ordinal))
        {
            end++;
        }

        var updated = new List<string>();
        updated.AddRange(lines.Take(start + 1));
        if (!string.IsNullOrWhiteSpace(content))
        {
            updated.Add(string.Empty);
            updated.AddRange(contentLines);
        }
        if (end < lines.Count)
        {
            if (updated.Count > 0 && !string.IsNullOrWhiteSpace(updated[^1]))
            {
                updated.Add(string.Empty);
            }
            updated.AddRange(lines.Skip(end));
        }
        return string.Join("\n", updated);
    }

    private static string ReplaceTitleHeading(string body, string id, string title)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        var heading = string.IsNullOrWhiteSpace(id) ? $"# {title}" : $"# {id} - {title}";
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(id) && !line.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            lines[i] = heading;
            return string.Join("\n", lines);
        }

        var trimmedLines = lines.SkipWhile(string.IsNullOrWhiteSpace).ToList();
        if (trimmedLines.Count == 0)
        {
            return heading;
        }

        var updated = new List<string> { heading, string.Empty };
        updated.AddRange(trimmedLines);
        return string.Join("\n", updated);
    }

    private static string BuildIssueSummary(GithubIssue issue)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(issue.Url))
        {
            lines.Add($"Imported from GitHub issue: {issue.Url}");
        }
        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            var normalizedBody = NormalizeIssueBody(issue.Body);
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }
            lines.AddRange(normalizedBody.Replace("\r\n", "\n").Split('\n'));
        }
        return string.Join("\n", lines);
    }

    private static string NormalizeIssueBody(string body)
    {
        var normalized = body.Replace("\r\n", "\n");
        if (!normalized.Contains('\n') && normalized.Contains("\\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\\r\\n", "\n").Replace("\\n", "\n");
        }
        return normalized;
    }

    private static string FormatAcceptanceCriteria(IEnumerable<string>? criteria)
    {
        var items = criteria?
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .Select(entry => entry.StartsWith("-", StringComparison.Ordinal) ? entry : $"- {entry}")
            .ToList() ?? new List<string>();

        if (items.Count == 0)
        {
            return "-";
        }
        return string.Join("\n", items);
    }

    private static bool AddUniqueLink(List<object?> list, string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }
        if (!list.OfType<string>().Any(entry => entry.Equals(link, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(link);
            return true;
        }
        return false;
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
            var converted = legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            data["related"] = converted;
            return converted;
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
            return new RelatedLinks(
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>());
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
            Extract("issues"),
            Extract("branches"));
    }

    private static string NormalizeRelatedLink(string link)
    {
        var trimmed = link.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }
        return trimmed;
    }

    private static List<string> NormalizeRelatedValues(IEnumerable<string> values)
    {
        return values
            .Select(value => NormalizeRelatedLink(value))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool NormalizeCompatibilityRelatedList(ref RelatedLinksPayload related, string key)
    {
        var current = GetRelatedList(related, key);
        var normalized = NormalizeRelatedValues(current);
        if (ListsMatch(current, normalized))
        {
            return false;
        }

        related = SetRelatedList(related, key, normalized);
        return true;
    }

    private static bool NormalizeCanonicalList(IDictionary<string, object?> data, string key, string placeholder)
    {
        var current = GetStringList(data, key);
        var normalized = NormalizeRelatedValues(current);
        if (normalized.Count == 0)
        {
            normalized.Add(placeholder);
        }

        if (ListsMatch(current, normalized))
        {
            return false;
        }

        data[key] = normalized;
        return true;
    }

    private static bool ListsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index]?.Trim(), right[index]?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NormalizeList(Dictionary<string, object?> related, string key)
    {
        if (!related.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is not IEnumerable<object> list)
        {
            return false;
        }

        var normalized = list
            .Select(entry => entry?.ToString() ?? string.Empty)
            .Select(NormalizeRelatedLink)
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var current = list.Select(entry => entry?.ToString() ?? string.Empty).ToList();
        if (normalized.SequenceEqual(current, StringComparer.Ordinal))
        {
            return false;
        }

        related[key] = normalized.Cast<object?>().ToList();
        return true;
    }

    private static bool NormalizeTags(IDictionary<string, object?> data)
    {
        if (!data.TryGetValue("tags", out var value) || value is null)
        {
            data["tags"] = new List<string>();
            return true;
        }

        if (value is string)
        {
            data["tags"] = new List<string>();
            return true;
        }

        if (value is IEnumerable<object> enumerable)
        {
            var normalized = enumerable
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = enumerable.Select(item => item?.ToString() ?? string.Empty).ToList();
            if (!normalized.SequenceEqual(current, StringComparer.Ordinal))
            {
                data["tags"] = normalized;
                return true;
            }
            return false;
        }

        data["tags"] = new List<string>();
        return true;
    }

    private static bool EnsureRelatedLists(IDictionary<string, object?> data)
    {
        var changed = false;
        var related = GetRelatedMap(data);
        if (related is null)
        {
            related = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            data["related"] = related;
            changed = true;
        }

        changed |= EnsureListChanged(related, "specs");
        changed |= EnsureListChanged(related, "adrs");
        changed |= EnsureListChanged(related, "files");
        changed |= EnsureListChanged(related, "prs");
        changed |= EnsureListChanged(related, "issues");
        changed |= EnsureListChanged(related, "branches");

        return changed;
    }

    private static bool EnsureListChanged(Dictionary<string, object?> data, string key)
    {
        var had = data.TryGetValue(key, out var value);
        var wasList = value is List<object?>;
        var wasEnumerable = value is IEnumerable<object> && value is not List<object?>;
        _ = EnsureList(data, key);
        return !had || value is null || wasEnumerable || !wasList;
    }

    private static bool ShouldUseCanonicalWorkItems(string repoRoot)
    {
        return Directory.Exists(Path.Combine(repoRoot, SpecTraceLayout.SpecsRoot)) ||
               File.Exists(Path.Combine(repoRoot, "artifact-id-policy.json"));
    }

    private static bool IsCanonicalWorkItemFrontMatter(IDictionary<string, object?> data)
    {
        var artifactType = GetString(data, "artifact_type");
        if (string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
        return !string.IsNullOrWhiteSpace(artifactId) &&
               artifactId.StartsWith("WI-", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCanonicalStatus(string? status)
    {
        var normalized = status?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "planned";
        }

        return normalized.ToLowerInvariant() switch
        {
            "draft" or "planned" => "planned",
            "ready" => "planned",
            "in-progress" or "in_progress" => "in_progress",
            "blocked" => "blocked",
            "done" or "complete" => "complete",
            "dropped" or "cancelled" => "cancelled",
            "superseded" => "superseded",
            _ => normalized
        };
    }

    private static string NormalizeCompatibilityType(string? type)
    {
        var normalized = type?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "task";
        }

        return normalized.ToLowerInvariant() switch
        {
            "work_item" or "work-item" or "work item" => "task",
            "bug" or "task" or "spike" => normalized.ToLowerInvariant(),
            _ => normalized.ToLowerInvariant()
        };
    }

    private static string GetDefaultCanonicalDomain(string repoRoot)
    {
        return SpecTraceLayout.GetDefaultDomain(repoRoot);
    }

    private static RelatedLinksPayload ToRelatedLinksPayload(RelatedLinks related)
    {
        return new RelatedLinksPayload(
            related.Specs.ToList(),
            related.Adrs.ToList(),
            related.Files.ToList(),
            related.Prs.ToList(),
            related.Issues.ToList(),
            related.Branches.ToList());
    }

    private static RelatedLinks FromRelatedLinksPayload(RelatedLinksPayload related)
    {
        return new RelatedLinks(
            related.Specs.ToList(),
            related.Adrs.ToList(),
            related.Files.ToList(),
            related.Prs.ToList(),
            related.Issues.ToList(),
            related.Branches.ToList());
    }

    private static string BuildCompatibilityMetadataSection(WorkItemCompatibilityMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return string.Join(
            "\n",
            "```json",
            json,
            "```");
    }

    private static string AppendCompatibilityMetadata(string body, WorkItemCompatibilityMetadata metadata)
    {
        return ReplaceSection(body, "Compatibility Metadata", BuildCompatibilityMetadataSection(metadata));
    }

    private static bool TryParseCompatibilityMetadata(string body, out WorkItemCompatibilityMetadata metadata)
    {
        metadata = new WorkItemCompatibilityMetadata(
            "task",
            null,
            string.Empty,
            null,
            new List<string>(),
            null,
            new RelatedLinksPayload(
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>()));

        var section = SpecTraceMarkdown.ExtractSection(body, "Compatibility Metadata");
        if (string.IsNullOrWhiteSpace(section))
        {
            return false;
        }

        var lines = section.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var start = lines.FindIndex(line => line.TrimStart().StartsWith("```", StringComparison.Ordinal));
        var end = lines.FindLastIndex(line => line.TrimStart().StartsWith("```", StringComparison.Ordinal));
        var jsonText = start >= 0 && end > start
            ? string.Join("\n", lines[(start + 1)..end])
            : section;

        try
        {
            var parsed = JsonSerializer.Deserialize<WorkItemCompatibilityMetadata>(jsonText);
            if (parsed is null)
            {
                return false;
            }

            metadata = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static WorkItemCompatibilityMetadata BuildCompatibilityMetadata(
        string? type,
        string? priority,
        string created,
        string? updated,
        IList<string> tags,
        string? githubSynced,
        RelatedLinks related)
    {
        return new WorkItemCompatibilityMetadata(
            NormalizeCompatibilityType(type),
            string.IsNullOrWhiteSpace(priority) ? null : priority.Trim(),
            created,
            string.IsNullOrWhiteSpace(updated) ? null : updated.Trim(),
            tags.ToList(),
            githubSynced,
            ToRelatedLinksPayload(related));
    }

    private static List<string> GetRelatedList(RelatedLinksPayload related, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "specs" => related.Specs.ToList(),
            "adrs" => related.Adrs.ToList(),
            "files" => related.Files.ToList(),
            "prs" => related.Prs.ToList(),
            "issues" => related.Issues.ToList(),
            "branches" => related.Branches.ToList(),
            _ => new List<string>()
        };
    }

    private static RelatedLinksPayload SetRelatedList(RelatedLinksPayload related, string key, IList<string> values)
    {
        return key.ToLowerInvariant() switch
        {
            "specs" => related with { Specs = values.ToList() },
            "adrs" => related with { Adrs = values.ToList() },
            "files" => related with { Files = values.ToList() },
            "prs" => related with { Prs = values.ToList() },
            "issues" => related with { Issues = values.ToList() },
            "branches" => related with { Branches = values.ToList() },
            _ => related
        };
    }

    private sealed record WorkItemCompatibilityMetadata(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("priority")] string? Priority,
        [property: JsonPropertyName("created")] string Created,
        [property: JsonPropertyName("updated")] string? Updated,
        [property: JsonPropertyName("tags")] IList<string> Tags,
        [property: JsonPropertyName("githubSynced")] string? GithubSynced,
        [property: JsonPropertyName("related")] RelatedLinksPayload Related);

    private sealed record BodySection(string Heading, List<string> Lines);
}
