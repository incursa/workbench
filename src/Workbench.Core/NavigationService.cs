// Navigation and index synchronization for docs and work items.
// Invariants: generated indexes are deterministic given the same inputs and ordering rules.
#pragma warning disable S1144
using System.Collections;
using System.Text;

namespace Workbench.Core;

public static class NavigationService
{
    /// <summary>
    /// Result payload returned by navigation sync operations.
    /// </summary>
    /// <param name="DocsUpdated">Number of docs updated.</param>
    /// <param name="ItemsUpdated">Number of work items updated.</param>
    /// <param name="IndexFilesUpdated">Number of index files updated.</param>
    /// <param name="MissingDocs">Missing docs referenced in front matter.</param>
    /// <param name="MissingItems">Missing work items referenced in docs.</param>
    /// <param name="Warnings">Warnings emitted during sync.</param>
    public sealed record NavigationSyncResult(
        int DocsUpdated,
        int ItemsUpdated,
        int IndexFilesUpdated,
        IList<string> MissingDocs,
        IList<string> MissingItems,
        IList<string> Warnings);

    private sealed record DocEntry(
        string? ArtifactId,
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
        bool dryRun,
        bool syncDocs = true)
    {
        var docSync = syncDocs
            ? await DocService.SyncLinksAsync(repoRoot, config, includeAllDocs: true, syncIssues, includeDone, dryRun)
.ConfigureAwait(false) : new DocService.DocSyncResult(0, 0, new List<string>(), new List<string>());
        var normalizedItems = WorkItemService.NormalizeRelatedLinks(repoRoot, config, includeDone, dryRun);
        var warnings = new List<string>();
        var docs = LoadDocEntries(repoRoot, config, warnings);
        var docsReadmePath = Path.Combine(repoRoot, config.Paths.DocsRoot, "README.md");
        var specsReadmePath = Path.Combine(repoRoot, config.Paths.SpecsRoot, "README.md");
        var architectureReadmePath = Path.Combine(repoRoot, config.Paths.ArchitectureDir, "README.md");
        var workReadmePath = Path.Combine(repoRoot, config.Paths.WorkRoot, "README.md");
        var rootReadmePath = Path.Combine(repoRoot, "README.md");

        var indexFilesUpdated = 0;
        indexFilesUpdated += SyncIndexFile(
            docsReadmePath,
            BuildDocsReadmeTemplate(),
            "workbench:overview-index",
            BuildDocsIndex(repoRoot, config, docsReadmePath, docs),
            force,
            dryRun);
        indexFilesUpdated += SyncIndexFile(
            specsReadmePath,
            BuildSpecsReadmeTemplate(),
            "workbench:specs-index",
            BuildSpecsIndex(repoRoot, config, specsReadmePath, docs),
            force,
            dryRun);
        indexFilesUpdated += SyncIndexFile(
            architectureReadmePath,
            BuildArchitectureReadmeTemplate(),
            "workbench:architecture-index",
            BuildArchitectureIndex(repoRoot, config, architectureReadmePath, docs),
            force,
            dryRun);
        indexFilesUpdated += SyncIndexFile(
            workReadmePath,
            BuildWorkReadmeTemplate(),
            "workbench:work-index",
            BuildWorkIndex(repoRoot, config, workReadmePath, docs),
            force,
            dryRun);
        indexFilesUpdated += SyncIndexFile(
            rootReadmePath,
            BuildRootReadmeTemplate(),
            "workbench:root-index",
            BuildRootIndex(repoRoot, config),
            force,
            dryRun);
        return new NavigationSyncResult(
            docSync.DocsUpdated,
            docSync.ItemsUpdated + normalizedItems,
            indexFilesUpdated,
            docSync.MissingDocs,
            docSync.MissingItems,
            warnings);
    }

    private static List<DocEntry> LoadDocEntries(string repoRoot, WorkbenchConfig config, List<string> warnings)
    {
        var entries = new List<DocEntry>();
        var roots = new[]
        {
                Path.Combine(repoRoot, config.Paths.DocsRoot),
                Path.Combine(repoRoot, "runbooks"),
                Path.Combine(repoRoot, "tracking"),
                Path.Combine(repoRoot, config.Paths.SpecsRoot, "requirements"),
                Path.Combine(repoRoot, config.Paths.SpecsRoot),
                Path.Combine(repoRoot, config.Paths.ArchitectureDir),
                Path.Combine(repoRoot, Path.Combine(config.Paths.SpecsRoot, "verification")),
                Path.Combine(repoRoot, Path.Combine(config.Paths.SpecsRoot, "work-items"))
            };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var searchOption = root.Equals(Path.Combine(repoRoot, config.Paths.SpecsRoot), StringComparison.OrdinalIgnoreCase)
            ? SearchOption.TopDirectoryOnly
            : SearchOption.AllDirectories;

            foreach (var path in Directory.EnumerateFiles(root, "*.md", searchOption))
            {
                var relative = NormalizePath(Path.GetRelativePath(repoRoot, path));
                if (IsDocsIndexFile(config, relative) ||
                    IsSpecsIndexFile(config, relative) ||
                    IsArchitectureIndexFile(config, relative) ||
                    IsDocTemplate(config, relative) ||
                    IsWorkArtifactDoc(config, relative) ||
                    HasCanonicalCueSibling(repoRoot, relative))
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
                var type = GetString(data, "artifact_type") ?? GetString(workbench, "type") ?? InferDocType(relative, config);
                var status = GetString(data, "status") ?? "unknown";
                var title = GetString(data, "title") ?? ExtractTitle(body) ?? Path.GetFileNameWithoutExtension(path);
                var workItems = GetStringList(data, "related_artifacts");
                if (workItems.Count == 0)
                {
                    workItems = GetStringList(workbench, "workItems");
                }
                var section = GetDocSection(relative, config);
                var githubLink = BuildGithubFileLink(config, relative);
                var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");

                entries.Add(new DocEntry(
                    artifactId,
                    title,
                    type,
                    status,
                    relative,
                    section,
                    workItems,
                    githubLink));
            }
        }

        foreach (var source in CanonicalArtifactDiscovery.EnumerateCanonicalSources(repoRoot, config)
                     .Where(source => string.Equals(source.Format, "cue", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var artifact = CueCli.ExportArtifact(repoRoot, source.SourcePath);
                var type = artifact.ArtifactType;
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                var section = GetDocSection(source.DisplayRepoRelativePath, config);
                var githubLink = BuildGithubFileLink(config, source.DisplayRepoRelativePath);
                entries.Add(new DocEntry(
                    string.IsNullOrWhiteSpace(artifact.ArtifactId) ? null : artifact.ArtifactId,
                    string.IsNullOrWhiteSpace(artifact.Title) ? Path.GetFileNameWithoutExtension(source.DisplayPath) : artifact.Title,
                    type,
                    string.IsNullOrWhiteSpace(artifact.Status) ? "unknown" : artifact.Status,
                    source.DisplayRepoRelativePath,
                    section,
                    artifact.RelatedArtifacts?.ToList() ?? new List<string>(),
                    githubLink));
            }
            catch (Exception ex)
            {
                warnings.Add($"{source.SourceRepoRelativePath}: {ex}");
            }
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
            builder.AppendLine("| Doc | ID | Type | Status | GitHub | Work Items |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

            foreach (var entry in group)
            {
                var relativeLink = NormalizePath(Path.GetRelativePath(readmeDir, Path.Combine(repoRoot, entry.RepoRelativePath)));
                var docLink = BuildMarkdownLink(entry.Title, relativeLink);
                var artifactLabel = string.IsNullOrWhiteSpace(entry.ArtifactId) ? "-" : EscapeTableCell(entry.ArtifactId);
                var githubLink = entry.GithubLink is null ? "-" : BuildMarkdownLink("view", entry.GithubLink);
                var workItems = FormatWorkItemLinks(entry.WorkItems, itemsById, readmeDir);

                var typeLabel = FormatDocType(entry.Type);
                var statusLabel = FormatDocStatus(entry.Status);
                builder.AppendLine($"| {docLink} | {artifactLabel} | {typeLabel} | {statusLabel} | {githubLink} | {workItems} |");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildWorkIndex(
        string repoRoot,
        WorkbenchConfig config,
        string workReadmePath,
        List<DocEntry> docs
        )
    {
        var builder = new StringBuilder();
        var activeItems = LoadWorkItemEntries(repoRoot, config.Paths.ItemsDir, config);
        AppendWorkItemTable(builder, "Active items", activeItems, workReadmePath, docs, repoRoot, config);

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
        builder.AppendLine("- [Overview](README.md)");
        builder.AppendLine("- [Requirements](specs/requirements/_index.md)");
        builder.AppendLine("- [Architecture](specs/architecture/WB/_index.md)");
        builder.AppendLine("- [Work items](specs/work-items/WB/_index.md)");
        builder.AppendLine("- [Verification](specs/verification/WB/_index.md)");
        builder.AppendLine("- [Generated](specs/generated)");
        builder.AppendLine("- [Templates](specs/templates)");
        builder.AppendLine("- [Schemas](specs/schemas)");
        builder.AppendLine("- [Runbooks](runbooks/README.md)");
        builder.AppendLine("- [Tracking](tracking/README.md)");
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
            foreach (var status in new[] { "planned", "in_progress", "blocked", "complete", "cancelled", "superseded" })
            {
                counts.TryGetValue(status, out var count);
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {FormatWorkItemStatus(status)} | {count} |"));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static List<WorkItemEntry> LoadWorkItemEntries(string repoRoot, string dir, WorkbenchConfig config, bool recursive = false)
    {
        var items = new List<WorkItemEntry>();
        var path = Path.Combine(repoRoot, dir);
        if (!Directory.Exists(path))
        {
            return items;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.EnumerateFiles(path, "*.md", searchOption))
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
        foreach (var entry in item.Related.Specs.Concat(item.Related.Files))
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

    private static int SyncIndexFile(string filePath, string template, string markerName, string content, bool force, bool dryRun)
    {
        if (!File.Exists(filePath))
        {
            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory());
                var startMarker = $"<!-- {markerName}:start -->";
                var endMarker = $"<!-- {markerName}:end -->";
                if (!ReplaceSection(template, startMarker, endMarker, content, out var newContent))
                {
                    newContent = template;
                }

                File.WriteAllText(filePath, newContent);
            }

            return 1;
        }

        return UpdateIndexSection(filePath, markerName, content, force, dryRun);
    }

    private static int UpdateIndexSection(string filePath, string markerName, string content, bool force, bool dryRun)
    {
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
        return updated || force ? 1 : 0;
    }

    private static string BuildDocsReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Overview",
            string.Empty,
            "High-level orientation, product summaries, and repo-level standards.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:overview-index:start -->",
            "<!-- workbench:overview-index:end -->",
            string.Empty);
    }

    private static string BuildSpecsReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Specs",
            string.Empty,
            "Canonical specifications live here. Keep them directly under `specs/` and use the sibling top-level `architecture/` and `work/` roots for design and execution artifacts.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:specs-index:start -->",
            "<!-- workbench:specs-index:end -->",
            string.Empty);
    }

    private static string BuildArchitectureReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Architecture",
            string.Empty,
            "Architecture and design documents for the repository.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:architecture-index:start -->",
            "<!-- workbench:architecture-index:end -->",
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
            "# Work Items",
            string.Empty,
            "Canonical work items live under `specs/work-items/`.",
            string.Empty,
            "## Layout",
            string.Empty,
            "- `overview`: high-level product, standard, and orientation docs.",
            "- `runbooks`: operational procedures and playbooks.",
            "- `tracking`: milestone and delivery notes.",
            "- `specs/requirements`: canonical requirement docs.",
            "- `specs/architecture`: canonical architecture docs.",
            "- `specs/work-items`: canonical work items.",
            "- `specs/verification`: canonical verification artifacts.",
            "- `specs/generated`: derived indexes, matrices, and reports.",
            "- `specs/templates`: reusable document templates.",
            "- `specs/schemas`: machine-readable validation contracts.",
            string.Empty,
            "## Workflow",
            string.Empty,
            "1. Create a work item with `workbench item new`, `workbench item generate`, or `workbench voice workitem`.",
            "2. Edit the Markdown file to tighten the summary, context, traceability, implementation notes, acceptance criteria, and notes.",
            "3. Link specs, architecture docs, files, PRs, or issues with `workbench item link`, and use `workbench promote` when you want branch + commit scaffolding.",
            "4. Refresh the generated views with `workbench nav sync` and run `workbench validate` before review or automation.",
            string.Empty,
            "## Index",
            string.Empty,
            "Generated by `workbench nav sync`.",
            string.Empty,
            "<!-- workbench:work-index:start -->",
            "<!-- workbench:work-index:end -->",
            string.Empty);
    }

    private static string BuildArchitectureIndex(
        string repoRoot,
        WorkbenchConfig config,
        string architectureReadmePath,
        List<DocEntry> docs)
    {
        var prefix = NormalizePath(config.Paths.ArchitectureDir).TrimEnd('/') + "/";
        var architectureDocs = docs
            .Where(entry => NormalizePath(entry.RepoRelativePath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (architectureDocs.Count == 0)
        {
            return "_No architecture docs found._";
        }

        return BuildDocsIndex(repoRoot, config, architectureReadmePath, architectureDocs);
    }

    private static string BuildSpecsIndex(
        string repoRoot,
        WorkbenchConfig config,
        string specReadmePath,
        List<DocEntry> docs)
    {
        var prefix = NormalizePath(config.Paths.SpecsRoot).TrimEnd('/') + "/";
        var specDocs = docs
            .Where(entry => NormalizePath(entry.RepoRelativePath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (specDocs.Count == 0)
        {
            return "_No specs found._";
        }

        return BuildDocsIndex(repoRoot, config, specReadmePath, specDocs);
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

    private static bool IsSpecsIndexFile(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.Equals($"{config.Paths.SpecsRoot}/README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchitectureIndexFile(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.Equals($"{NormalizePath(config.Paths.ArchitectureDir)}/README.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocTemplate(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        return normalized.StartsWith($"{config.Paths.DocsRoot}/templates/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("templates/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith($"{config.Paths.SpecsRoot}/templates/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith($"{NormalizePath(config.Paths.ArchitectureDir)}/templates/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith($"{NormalizePath(config.Paths.WorkRoot)}/templates/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkArtifactDoc(WorkbenchConfig config, string relativePath)
    {
        var normalized = NormalizePath(relativePath);
        var workRoot = NormalizePath(config.Paths.WorkRoot).TrimEnd('/');
        if (!normalized.StartsWith($"{workRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = normalized[(workRoot.Length + 1)..];
        return remainder.StartsWith("items/", StringComparison.OrdinalIgnoreCase)
            || remainder.StartsWith("templates/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCanonicalCueSibling(string repoRoot, string relativePath)
    {
        if (!SpecTraceLayout.IsCanonicalPath(relativePath) || !relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cuePath = Path.Combine(repoRoot, Path.ChangeExtension(relativePath, ".cue")!.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(cuePath);
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
        if (normalized.StartsWith($"{config.Paths.DocsRoot.TrimEnd('/')}/", StringComparison.OrdinalIgnoreCase))
        {
            return "Overview";
        }

        if (normalized.StartsWith("runbooks/", StringComparison.OrdinalIgnoreCase))
        {
            return "Runbooks";
        }

        if (normalized.StartsWith("tracking/", StringComparison.OrdinalIgnoreCase))
        {
            return "Tracking";
        }

        var architecturePrefix = $"{NormalizePath(config.Paths.ArchitectureDir).TrimEnd('/')}/";
        if (normalized.StartsWith(architecturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var architectureRemainder = normalized[architecturePrefix.Length..];
            var architectureSegments = architectureRemainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (architectureSegments.Length == 0)
            {
                return "Architecture";
            }

            return architectureSegments[0];
        }

        if (normalized.StartsWith($"{config.Paths.SpecsRoot.TrimEnd('/')}/", StringComparison.OrdinalIgnoreCase))
        {
            return "Specs";
        }

        return "Repo";
    }

    private static string InferDocType(string relativePath, WorkbenchConfig config)
    {
        var normalized = NormalizePath(relativePath).ToLowerInvariant();
        var architectureRoot = NormalizePath(config.Paths.ArchitectureDir).TrimEnd('/').ToLowerInvariant();
        if (normalized.Contains($"{architectureRoot}/"))
        {
            return "architecture";
        }

        var specsRoot = config.Paths.SpecsRoot.TrimEnd('/').ToLowerInvariant();
        if (normalized.StartsWith($"{specsRoot}/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("/specs/templates/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("/specs/generated/", StringComparison.OrdinalIgnoreCase))
        {
            return "specification";
        }
        if (normalized.Contains("/runbooks/"))
        {
            return normalized.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase) ? "doc" : "runbook";
        }
        if (normalized.Contains($"/{config.Paths.DocsRoot.TrimEnd('/').ToLowerInvariant()}/") ||
            normalized.Contains("/tracking/") ||
            normalized.Contains("/templates/"))
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
            "planned" => 0,
            "in_progress" => 1,
            "in-progress" => 1,
            "blocked" => 2,
            "complete" => 3,
            "cancelled" => 4,
            "superseded" => 5,
            _ => 6
        };
    }

    private static string FormatDocType(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            "spec" => "🧭 spec",
            "specification" => "🧭 specification",
            "architecture" => "🧩 architecture",
            "guide" => "🧩 guide",
            "work_item" => "🛠 work item",
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
            "planned" => "🟦 planned",
            "in_progress" => "🔵 in-progress",
            "draft" => "🟡 draft",
            "active" => "🟢 active",
            "accepted" => "✅ accepted",
            "blocked" => "🟥 blocked",
            "complete" => "✅ complete",
            "cancelled" => "🚫 cancelled",
            "superseded" => "↩ superseded",
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
            "planned" => "🟦 planned",
            "in_progress" => "🔵 in-progress",
            "in-progress" => "🔵 in-progress",
            "blocked" => "🟥 blocked",
            "complete" => "✅ complete",
            "cancelled" => "🚫 cancelled",
            "superseded" => "↩ superseded",
            _ => EscapeTableCell(status)
        };
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "superseded", StringComparison.OrdinalIgnoreCase);
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
