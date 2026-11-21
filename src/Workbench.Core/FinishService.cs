namespace Workbench.Core;

/// <summary>
/// Completes a work item and reconciles deterministic local repository state.
/// </summary>
public static class FinishService
{
    private const string CompletionHeading = "## Completion Notes";
    private static readonly IReadOnlySet<string> nonCompletableTerminalStatuses = new HashSet<string>(
        ["cancelled", "superseded"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records completion evidence, completes the item, refreshes local derived views, and validates the repository.
    /// </summary>
    public static async Task<FinishData> FinishAsync(
        string repoRoot,
        WorkbenchConfig config,
        FinishOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.WorkItemId))
        {
            throw new ArgumentException("Work item ID is required.", nameof(options));
        }

        if (options.DryRun)
        {
            var workItemPath = WorkItemService.GetItemPathById(repoRoot, config, options.WorkItemId.Trim());
            using var simulation = FinishSimulationWorkspace.Create(repoRoot, config, workItemPath);
            return await FinishCoreAsync(
                simulation.Root,
                config,
                options with { DryRun = false },
                reportedDryRun: true).ConfigureAwait(false);
        }

        return await FinishCoreAsync(repoRoot, config, options, reportedDryRun: false).ConfigureAwait(false);
    }

    private static async Task<FinishData> FinishCoreAsync(
        string repoRoot,
        WorkbenchConfig config,
        FinishOptions options,
        bool reportedDryRun)
    {

        var pullRequest = NormalizePullRequest(options.PullRequest);
        var evidenceEntries = BuildEvidenceEntries(pullRequest, options.Evidence, options.Note);
        var path = WorkItemService.GetItemPathById(repoRoot, config, options.WorkItemId.Trim());
        var originalContent = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        if (!FrontMatter.TryParse(originalContent, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var previousStatus = ReadString(frontMatter!.Data, "status") ?? string.Empty;
        if (nonCompletableTerminalStatuses.Contains(previousStatus))
        {
            throw new InvalidOperationException(
                $"Work item {options.WorkItemId.Trim()} is {previousStatus} and cannot be finished as complete.");
        }
        var statusChanged = !string.Equals(previousStatus, "complete", StringComparison.OrdinalIgnoreCase);
        var updatedBody = EnsureCompletionEvidence(frontMatter.Body, evidenceEntries, out var evidenceChanged);

        if (statusChanged)
        {
            frontMatter.Data["status"] = "complete";
        }

        if (statusChanged || evidenceChanged)
        {
            await File.WriteAllTextAsync(path, new FrontMatter(frontMatter.Data, updatedBody).Serialize()).ConfigureAwait(false);
        }

        var navigation = await RunNavigationAsync(repoRoot, config).ConfigureAwait(false);
        var validation = RunValidation(repoRoot, config, options.ValidationProfile);

        return new FinishData(
            options.WorkItemId.Trim(),
            NormalizeRepoPath(repoRoot, path),
            previousStatus,
            statusChanged ? "complete" : previousStatus,
            statusChanged,
            evidenceChanged,
            reportedDryRun,
            navigation,
            validation,
            BuildNextAction(options.WorkItemId.Trim(), navigation, validation));
    }

    private static async Task<FinishNavigationData> RunNavigationAsync(
        string repoRoot,
        WorkbenchConfig config)
    {
        try
        {
            var result = await NavigationService.SyncNavigationAsync(
                repoRoot,
                config,
                includeDone: false,
                syncIssues: false,
                force: false,
                dryRun: false,
                syncDocs: false,
                normalizeItems: false).ConfigureAwait(false);
            return new FinishNavigationData(
                true,
                result.DocsUpdated,
                result.ItemsUpdated,
                result.IndexFilesUpdated,
                result.MissingDocs.Select(message => NormalizeDiagnostic(repoRoot, message)).ToArray(),
                result.MissingItems.Select(message => NormalizeDiagnostic(repoRoot, message)).ToArray(),
                result.Warnings.Select(message => NormalizeDiagnostic(repoRoot, message)).ToArray(),
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new FinishNavigationData(
                false,
                0,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                NormalizeDiagnostic(repoRoot, ex.Message));
        }
    }

    private static FinishValidationData RunValidation(
        string repoRoot,
        WorkbenchConfig config,
        string? profile)
    {
        try
        {
            var result = ValidationService.ValidateRepo(
                repoRoot,
                config,
                new ValidationOptions(
                    LinkInclude: Array.Empty<string>(),
                    LinkExclude: Array.Empty<string>(),
                    SkipDocSchema: false,
                    Profile: profile,
                    Scope: Array.Empty<string>()));
            return new FinishValidationData(
                true,
                result.Errors.Count == 0,
                result.Profile,
                result.Errors.Select(message => NormalizeDiagnostic(repoRoot, message)).ToArray(),
                result.Warnings.Select(message => NormalizeDiagnostic(repoRoot, message)).ToArray(),
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return new FinishValidationData(
                true,
                false,
                profile ?? ValidationProfiles.Core,
                Array.Empty<string>(),
                Array.Empty<string>(),
                NormalizeDiagnostic(repoRoot, ex.Message));
        }
    }

    private static string? BuildNextAction(
        string workItemId,
        FinishNavigationData navigation,
        FinishValidationData validation)
    {
        if (!navigation.Ok)
        {
            return $"Resolve the navigation error and rerun workbench finish {workItemId}.";
        }
        if (!validation.Ok)
        {
            return $"Resolve validation findings and rerun workbench validate --profile {validation.Profile}.";
        }
        return null;
    }

    private static string? NormalizePullRequest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Pull request must be an absolute HTTP or HTTPS URL.");
        }

        return normalized;
    }

    private static IReadOnlyList<string> BuildEvidenceEntries(
        string? pullRequest,
        IList<string>? evidence,
        string? note)
    {
        var entries = new List<string>();
        if (pullRequest is not null)
        {
            entries.Add($"Pull request: {pullRequest}");
        }

        if (evidence is not null)
        {
            foreach (var value in evidence)
            {
                var normalized = NormalizeEntry(value);
                if (normalized.Length > 0)
                {
                    entries.Add($"Evidence: {normalized}");
                }
            }
        }

        var normalizedNote = NormalizeEntry(note);
        if (normalizedNote.Length > 0)
        {
            entries.Add($"Note: {normalizedNote}");
        }

        return entries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string EnsureCompletionEvidence(
        string body,
        IReadOnlyList<string> entries,
        out bool changed)
    {
        changed = false;
        if (entries.Count == 0)
        {
            return body;
        }

        var normalizedBody = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalizedBody.Split('\n').ToList();
        var headingIndex = lines.FindIndex(line =>
            string.Equals(line.Trim(), CompletionHeading, StringComparison.OrdinalIgnoreCase));

        if (headingIndex < 0)
        {
            var result = normalizedBody.TrimEnd();
            if (result.Length > 0)
            {
                result += "\n\n";
            }
            result += CompletionHeading + "\n\n" + string.Join("\n", entries.Select(entry => $"- {entry}")) + "\n";
            changed = true;
            return result;
        }

        var sectionEnd = lines.Count;
        for (var index = headingIndex + 1; index < lines.Count; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                sectionEnd = index;
                break;
            }
        }

        var existing = lines
            .Skip(headingIndex + 1)
            .Take(sectionEnd - headingIndex - 1)
            .Select(NormalizeEntry)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = entries.Where(entry => !existing.Contains(entry)).ToList();
        if (missing.Count == 0)
        {
            return body;
        }

        while (sectionEnd > headingIndex + 1 && string.IsNullOrWhiteSpace(lines[sectionEnd - 1]))
        {
            sectionEnd--;
        }

        if (sectionEnd == headingIndex + 1)
        {
            lines.Insert(sectionEnd++, string.Empty);
        }

        foreach (var entry in missing)
        {
            lines.Insert(sectionEnd++, $"- {entry}");
        }

        if (sectionEnd < lines.Count && !string.IsNullOrWhiteSpace(lines[sectionEnd]))
        {
            lines.Insert(sectionEnd, string.Empty);
        }

        changed = true;
        return string.Join("\n", lines);
    }

    private static string NormalizeEntry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("-", StringComparison.Ordinal))
        {
            normalized = normalized[1..].TrimStart();
        }
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? ReadString(IDictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString()?.Trim() : null;
    }

    private static string NormalizeRepoPath(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
    }

    private static string NormalizeDiagnostic(string repoRoot, string message)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return message
            .Replace(normalizedRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot + Path.AltDirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot, ".", StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');
    }
}
