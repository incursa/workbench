namespace Workbench.Core;

/// <summary>Builds concise, deterministic repository context without mutating state.</summary>
public static class ContextService
{
    private static readonly IReadOnlySet<string> terminalStatuses = new HashSet<string>(
        ["complete", "cancelled", "superseded"],
        StringComparer.OrdinalIgnoreCase);

    public static ContextData GetContext(string repoRoot, WorkbenchConfig config, string? workItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(config);

        var normalizedRoot = Path.GetFullPath(repoRoot);
        var policy = WorkflowPolicyResolver.Resolve(config.Workflow, config.Validation);
        var branch = TryGetBranch(normalizedRoot);
        var clean = TryGetClean(normalizedRoot);
        var items = WorkItemService.ListItems(normalizedRoot, config, includeDone: true).Items
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = SelectItem(items, workItemId, branch);
        var active = items
            .Where(item => !terminalStatuses.Contains(item.Status))
            .Select(item => Summarize(normalizedRoot, item))
            .ToList();
        var applicable = selected is null
            ? new List<ContextArtifactReference>()
            : ResolveApplicableArtifacts(normalizedRoot, config, selected);

        return new ContextData(
            1,
            new ContextRepository(normalizedRoot, branch, clean),
            policy,
            selected is null ? null : Summarize(normalizedRoot, selected),
            active,
            applicable,
            BuildConstraints(config, policy),
            Recommend(policy, selected, active));
    }

    private static WorkItem? SelectItem(IReadOnlyList<WorkItem> items, string? explicitId, string? branch)
    {
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return items.FirstOrDefault(item =>
                       item.Id.Equals(explicitId, StringComparison.OrdinalIgnoreCase) ||
                       item.ArtifactId.Equals(explicitId, StringComparison.OrdinalIgnoreCase))
                   ?? throw new FileNotFoundException($"Work item not found: {explicitId}");
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            return null;
        }

        return items
            .OrderByDescending(item => item.Id.Length)
            .FirstOrDefault(item => branch.Contains(item.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static ContextWorkItemSummary Summarize(string repoRoot, WorkItem item)
    {
        return new ContextWorkItemSummary(
            item.Id,
            item.Title,
            item.Status,
            NormalizeRelativePath(repoRoot, item.Path));
    }

    private static List<ContextArtifactReference> ResolveApplicableArtifacts(
        string repoRoot,
        WorkbenchConfig config,
        WorkItem item)
    {
        var catalog = BuildArtifactCatalog(repoRoot, config);
        var references = item.Addresses
            .Concat(item.DesignLinks)
            .Concat(item.VerificationLinks)
            .Concat(item.RelatedArtifacts)
            .Concat(item.Related.Specs)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase);

        var result = new List<ContextArtifactReference>();
        foreach (var reference in references)
        {
            if (catalog.TryGetValue(reference, out var exact))
            {
                result.Add(exact.ToReference(reference));
                continue;
            }

            var containing = catalog.Values
                .OrderBy(entry => string.Equals(entry.ArtifactType, "specification", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(entry => entry.ArtifactId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(entry => entry.RequirementIds.Contains(reference));
            if (containing is not null)
            {
                result.Add(containing.ToReference(reference));
                continue;
            }

            var localPath = ResolveLocalReference(repoRoot, reference);
            result.Add(localPath is null
                ? new ContextArtifactReference(reference, null, null, null, null, false)
                : new ContextArtifactReference(
                    reference,
                    null,
                    "document",
                    Path.GetFileNameWithoutExtension(localPath),
                    NormalizeRelativePath(repoRoot, localPath),
                    true));
        }

        return result;
    }

    private static Dictionary<string, CatalogEntry> BuildArtifactCatalog(string repoRoot, WorkbenchConfig config)
    {
        var catalog = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in CanonicalArtifactDiscovery.EnumerateCanonicalSources(repoRoot, config))
        {
            try
            {
                if (source.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var artifact = CanonicalArtifactJsonLoader.Load(repoRoot, source.SourcePath);
                    if (!string.IsNullOrWhiteSpace(artifact.ArtifactId))
                    {
                        catalog[artifact.ArtifactId] = new CatalogEntry(
                            artifact.ArtifactId,
                            artifact.ArtifactType,
                            artifact.Title,
                            source.DisplayRepoRelativePath,
                            true,
                            ExtractRequirementIds(File.ReadAllText(source.SourcePath)));
                    }
                    continue;
                }

                var content = File.ReadAllText(source.SourcePath);
                if (!FrontMatter.TryParse(content, out var frontMatter, out _))
                {
                    continue;
                }

                var artifactId = ReadString(frontMatter!.Data, "artifact_id")
                    ?? ReadString(frontMatter.Data, "artifactId");
                if (string.IsNullOrWhiteSpace(artifactId))
                {
                    continue;
                }

                catalog[artifactId] = new CatalogEntry(
                    artifactId,
                    ReadString(frontMatter.Data, "artifact_type"),
                    ReadString(frontMatter.Data, "title"),
                    source.DisplayRepoRelativePath,
                    true,
                    ExtractRequirementIds(content));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
            {
                // Context is advisory. A malformed artifact is surfaced by validation,
                // while unresolved references remain useful here.
            }
        }
        return catalog;
    }

    private static IReadOnlyList<string> BuildConstraints(WorkbenchConfig config, WorkflowPolicy policy)
    {
        return
        [
            $"workflow.level={policy.Level}",
            $"validation.profile={policy.ValidationProfile}",
            $"git.requireCleanWorkingTree={config.Git.RequireCleanWorkingTree.ToString().ToLowerInvariant()}",
            $"github.sync.mode={config.Github.Sync.Mode}"
        ];
    }

    private static ContextRecommendedAction Recommend(
        WorkflowPolicy policy,
        WorkItem? selected,
        IReadOnlyList<ContextWorkItemSummary> active)
    {
        if (selected is not null)
        {
            if (selected.Status.Equals("planned", StringComparison.OrdinalIgnoreCase))
            {
                return new ContextRecommendedAction(
                    $"workbench item status {selected.Id} in_progress",
                    "The selected work item is planned and ready to start.");
            }
            if (selected.Status.Equals("blocked", StringComparison.OrdinalIgnoreCase))
            {
                return new ContextRecommendedAction(
                    $"workbench item show {selected.Id}",
                    "The selected work item is blocked; inspect its recorded context before continuing.");
            }
            return new ContextRecommendedAction(
                $"workbench validate --profile {policy.ValidationProfile}",
                terminalStatuses.Contains(selected.Status)
                    ? "The selected work item is terminal; validate the repository instead of closing it again."
                    : "Validate the selected work against the repository policy before finishing.");
        }

        if (active.Count == 1)
        {
            return new ContextRecommendedAction(
                $"workbench context --work-item {active[0].Id}",
                "One active work item exists; select it for focused context.");
        }
        if (active.Count > 1)
        {
            return new ContextRecommendedAction(
                "workbench item list --format json",
                "Multiple active work items exist; choose the one that matches the current change.");
        }
        if (policy.Level.Equals(WorkflowPolicyResolver.Light, StringComparison.OrdinalIgnoreCase))
        {
            return new ContextRecommendedAction(
                "workbench validate --profile core",
                "Light policy permits trivial or emergency work without creating a work item.");
        }
        return new ContextRecommendedAction(
            "workbench item new --type work_item --title \"<title>\"",
            "No active work item was found; create one for meaningful work or select light policy for low-ceremony changes.");
    }

    private static string? TryGetBranch(string repoRoot)
    {
        try
        {
            return GitService.GetCurrentBranch(repoRoot);
        }
        catch (InvalidOperationException)
        {
            var symbolic = GitService.Run(repoRoot, "symbolic-ref", "--quiet", "--short", "HEAD");
            return symbolic.ExitCode == 0 && !string.IsNullOrWhiteSpace(symbolic.StdOut)
                ? symbolic.StdOut
                : null;
        }
    }

    private static bool? TryGetClean(string repoRoot)
    {
        try
        {
            return GitService.IsClean(repoRoot);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ResolveLocalReference(string repoRoot, string reference)
    {
        if (Uri.TryCreate(reference, UriKind.Absolute, out _))
        {
            return null;
        }
        var candidate = Path.IsPathRooted(reference)
            ? reference
            : Path.Combine(repoRoot, reference.TrimStart('/', '\\'));
        var fullCandidate = Path.GetFullPath(candidate);
        var relative = Path.GetRelativePath(Path.GetFullPath(repoRoot), fullCandidate);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return null;
        }

        return File.Exists(fullCandidate) ? fullCandidate : null;
    }

    private static IReadOnlySet<string> ExtractRequirementIds(string content)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var heading = line.TrimStart();
            if (!heading.StartsWith('#'))
            {
                continue;
            }

            var text = heading.TrimStart('#').TrimStart();
            if (!text.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = text.IndexOfAny([' ', '\t', '`']);
            ids.Add(separator < 0 ? text : text[..separator]);
        }

        return ids;
    }

    private static string NormalizeRelativePath(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
    }

    private static string? ReadString(IDictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private sealed record CatalogEntry(
        string? ArtifactId,
        string? ArtifactType,
        string? Title,
        string? Path,
        bool Resolved,
        IReadOnlySet<string> RequirementIds)
    {
        public ContextArtifactReference ToReference(string reference)
        {
            return new ContextArtifactReference(reference, ArtifactId, ArtifactType, Title, Path, Resolved);
        }
    }
}
