using System.Text.Json;

namespace Workbench.Core;

public sealed class ExecutionStateService
{
    public const int CurrentSchemaVersion = 1;
    private readonly IExecutionStateProvider provider;
    private readonly TimeProvider timeProvider;

    public ExecutionStateService(IExecutionStateProvider provider, TimeProvider? timeProvider = null)
    {
        this.provider = provider;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ExecutionStateData> RefreshAsync(
        string repoRoot,
        string workItemId,
        IReadOnlyList<ExecutionStateReference> references,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workItemId))
        {
            throw new InvalidOperationException("Work item ID is required.");
        }

        if (references.Count == 0)
        {
            throw new InvalidOperationException("At least one pull request, release, or deployment reference is required.");
        }

        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            throw new InvalidOperationException($"Config error: {configError}");
        }

        _ = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
        var snapshotPath = GetSnapshotPath(repoRoot, workItemId);
        var previous = ReadSnapshot(snapshotPath);
        var previousByReference = previous?.Observations.ToDictionary(
            observation => BuildKey(observation.Kind, observation.ExternalReference),
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ExecutionObservation>(StringComparer.OrdinalIgnoreCase);
        var observations = new List<ExecutionObservation>();
        var diagnostics = new List<ExecutionDiagnostic>();
        ExecutionProviderStatus availability;
        try
        {
            availability = await this.provider.CheckAvailabilityAsync(repoRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            availability = new ExecutionProviderStatus(false, FormatProviderError(ex));
        }

        foreach (var reference in references
                     .DistinctBy(reference => BuildKey(reference.Kind, reference.ExternalReference), StringComparer.OrdinalIgnoreCase))
        {
            var key = BuildKey(reference.Kind, reference.ExternalReference);
            if (!availability.Available)
            {
                var retained = RetainPrevious(previousByReference, key, observations);
                diagnostics.Add(new ExecutionDiagnostic(
                    this.provider.Name,
                    reference.Kind,
                    reference.ExternalReference,
                    "warning",
                    availability.Diagnostic ?? "Execution provider is unavailable.",
                    retained is not null,
                    retained?.ObservedAtUtc,
                    this.GetObservationAgeSeconds(retained)));
                continue;
            }

            try
            {
                observations.Add(await this.provider.ObserveAsync(repoRoot, reference, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var retained = RetainPrevious(previousByReference, key, observations);
                diagnostics.Add(new ExecutionDiagnostic(
                    this.provider.Name,
                    reference.Kind,
                    reference.ExternalReference,
                    "warning",
                    FormatProviderError(ex),
                    retained is not null,
                    retained?.ObservedAtUtc,
                    this.GetObservationAgeSeconds(retained)));
            }
        }

        var snapshot = new ExecutionStateSnapshot(
            CurrentSchemaVersion,
            workItemId.Trim(),
            this.provider.Name,
            this.timeProvider.GetUtcNow(),
            observations
                .OrderBy(observation => observation.Kind, StringComparer.Ordinal)
                .ThenBy(observation => observation.ExternalReference, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            diagnostics
                .OrderBy(diagnostic => diagnostic.Kind, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.ExternalReference, StringComparer.OrdinalIgnoreCase)
                .ToList());

        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        await File.WriteAllTextAsync(
            snapshotPath,
            JsonSerializer.Serialize(snapshot, WorkbenchJsonContext.Default.ExecutionStateSnapshot) + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
        return new ExecutionStateData(NormalizePath(Path.GetRelativePath(repoRoot, snapshotPath)), snapshot);
    }

    public static ExecutionStateData? Show(string repoRoot, string workItemId)
    {
        var path = GetSnapshotPath(repoRoot, workItemId);
        var snapshot = ReadSnapshot(path);
        return snapshot is null
            ? null
            : new ExecutionStateData(NormalizePath(Path.GetRelativePath(repoRoot, path)), snapshot);
    }

    public static string GetSnapshotPath(string repoRoot, string workItemId)
    {
        var safeId = string.Concat(workItemId.Trim().Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'));
        if (string.IsNullOrWhiteSpace(safeId))
        {
            throw new InvalidOperationException("Work item ID is required.");
        }

        return Path.Combine(repoRoot, "artifacts", "execution-state", $"{safeId}.json");
    }

    private static ExecutionStateSnapshot? ReadSnapshot(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize(
            File.ReadAllText(path),
            WorkbenchJsonContext.Default.ExecutionStateSnapshot);
    }

    private static ExecutionObservation? RetainPrevious(
        IReadOnlyDictionary<string, ExecutionObservation> previous,
        string key,
        ICollection<ExecutionObservation> observations)
    {
        if (previous.TryGetValue(key, out var observation))
        {
            observations.Add(observation);
            return observation;
        }

        return null;
    }

    private long? GetObservationAgeSeconds(ExecutionObservation? observation)
    {
        return observation is null
            ? null
            : Math.Max(0, (long)(this.timeProvider.GetUtcNow() - observation.ObservedAtUtc).TotalSeconds);
    }

    private static string FormatProviderError(Exception exception)
    {
        return $"{exception.GetType().Name}: {exception.Message}";
    }

    private static string BuildKey(string kind, string externalReference)
    {
        return $"{kind}:{externalReference}";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
