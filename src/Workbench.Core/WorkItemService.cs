using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Workbench;

public static class WorkItemService
{
    private const int MaxSlugLength = 80;

    public sealed record WorkItemResult(string Id, string Slug, string Path);

    public sealed record WorkItemListResult(IList<WorkItem> Items);

    public static WorkItemResult CreateItem(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? status,
        string? priority,
        string? owner)
    {
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

    public static WorkItem UpdateItemFromGithubIssue(string path, GithubIssue issue, bool apply)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
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
            var changed = NormalizeTags(data);
            changed |= EnsureRelatedLists(data);
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

    public static WorkItem? LoadItem(string path)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            return null;
        }

        var data = frontMatter!.Data;
        var id = GetString(data, "id") ?? "";
        var type = GetString(data, "type") ?? "";
        var status = GetString(data, "status") ?? "";
        var title = GetString(data, "title") ?? DeriveTitleFromPath(path, id);
        var priority = GetString(data, "priority");
        var owner = GetString(data, "owner");
        var created = GetString(data, "created") ?? "";
        var updated = GetString(data, "updated");
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
            frontMatter.Body);
    }

    public static WorkItem UpdateStatus(string path, string status, string? note)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
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
        var slug = Slugify(newTitle);
        var newFileName = $"{item.Id}-{slug}.md";
        var currentDir = Path.GetDirectoryName(path) ?? repoRoot;
        var newPath = Path.Combine(currentDir, newFileName);

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }
        frontMatter!.Data["title"] = newTitle;
        File.WriteAllText(path, frontMatter.Serialize());

        File.Move(path, newPath, overwrite: false);
        return LoadItem(newPath) ?? throw new InvalidOperationException("Failed to reload work item.");
    }

    public static void AddPrLink(string path, string prUrl)
    {
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
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

        var related = GetRelatedMap(frontMatter!.Data);
        if (related is null)
        {
            throw new InvalidOperationException("Missing related section.");
        }

        var list = EnsureList(related, key);
        var before = list.Count;
        list.RemoveAll(entry => entry is string s && s.Equals(link, StringComparison.OrdinalIgnoreCase));
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
        foreach (var dir in new[] { config.Paths.ItemsDir, config.Paths.DoneDir })
        {
            var full = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }
            var match = Directory.EnumerateFiles(full, $"{id}-*.md", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (match is not null)
            {
                return match;
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
        var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        var max = 0;

        foreach (var dir in new[] { config.Paths.ItemsDir, config.Paths.DoneDir })
        {
            var path = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(path))
            {
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var match = pattern.Match(name);
                if (!match.Success)
                {
                    continue;
                }
                if (int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var value))
                {
                    max = Math.Max(max, value);
                }
            }
        }

        var next = max + 1;
        return $"{prefix}-{next.ToString($"D{width}", CultureInfo.InvariantCulture)}";
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
            foreach (var file in Directory.EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
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

    private static string AppendNote(string body, string note)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        var notesIndex = lines.FindIndex(line => line.Trim().Equals("## Notes", StringComparison.OrdinalIgnoreCase));
        if (notesIndex == -1)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }
            lines.Add("## Notes");
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
            lines[i] = string.IsNullOrWhiteSpace(id) ? $"# {title}" : $"# {id} - {title}";
            return string.Join("\n", lines);
        }
        return string.Join("\n", lines);
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
}
