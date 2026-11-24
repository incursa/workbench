using System.Text.Json;
using Workbench.Core;

namespace Workbench.CsvProvider;

/// <summary>
/// Exports immutable authority snapshots to editable CSV projections and turns only explicitly marked rows into
/// reconciliation proposals. The proposal must still be planned and explicitly applied by the declared authority.
/// </summary>
public static class CsvProjectionReconciler
{
    public static string Export(
        ResourceSnapshot source,
        string outputDirectory,
        string providerId = CsvProviderOptions.DefaultProjectionProviderId,
        string providerVersion = CsvProviderOptions.CurrentProviderVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        CsvProviderOptions options = CreateOptions(source, outputDirectory, providerId, providerVersion);
        CsvFileStore store = new(options, TimeProvider.System);
        List<IReadOnlyDictionary<string, string>> rows = [];
        foreach (ResourceDocument resource in source.Resources.OrderBy(
                     resource => resource.Address.ResourceId,
                     StringComparer.Ordinal))
        {
            if (!string.Equals(resource.Address.ResourceType, source.ResourceType, StringComparison.Ordinal)
                || !string.Equals(resource.Address.ResourceSet, source.ResourceSet, StringComparison.Ordinal))
            {
                throw CsvContentCodec.InvalidContent(
                    "csv.projection.snapshot.scope-mismatch",
                    $"Resource '{resource.Address.ResourceId}' does not match snapshot scope.");
            }

            string digest = CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(resource.Content));
            if (!string.Equals(digest, resource.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw CsvContentCodec.InvalidContent(
                    "csv.projection.snapshot.digest-mismatch",
                    $"Resource '{resource.Address.ResourceId}' payload digest is invalid.");
            }

            rows.Add(CsvContentCodec.CreateFields(
                source.ResourceType,
                source.ResourceSet,
                resource.Content,
                CsvRecordMode.Projection,
                CsvProjectionOperation.None,
                providerId,
                providerVersion,
                source.Authority,
                resource.VersionToken,
                resource.PayloadSha256,
                resource.Provenance));
        }

        store.Save(source.ResourceType, rows, CsvContentCodec.GetHeaders(source.ResourceType));
        return options.GetCsvPath(source.ResourceType);
    }

    public static CsvReconciliationPreview Preview(
        ResourceSnapshot baseline,
        string projectionDirectory,
        string providerId = CsvProviderOptions.DefaultProjectionProviderId,
        string providerVersion = CsvProviderOptions.CurrentProviderVersion)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        CsvProviderOptions options = CreateOptions(baseline, projectionDirectory, providerId, providerVersion);
        CsvFileStore store = new(options, TimeProvider.System);
        CsvTable table = store.Load(baseline.ResourceType, CsvRecordMode.Projection);
        Dictionary<string, ResourceDocument> baselineById = baseline.Resources.ToDictionary(
            resource => resource.Address.ResourceId,
            StringComparer.Ordinal);
        List<ResourceChange> proposed = [];
        List<ProviderPlannedChange> results = [];
        List<ProviderDiagnostic> diagnostics = [];

        string computedSnapshotDigest = CsvFileStore.ComputeSnapshotDigest(baseline.Resources);
        if (!string.Equals(computedSnapshotDigest, baseline.PayloadSha256, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(CsvContentCodec.Error(
                "csv.projection.baseline.digest-mismatch",
                "The immutable baseline snapshot digest is invalid.",
                baseline.SnapshotId));
        }

        foreach (CsvRowDocument row in table.Rows)
        {
            string id = row.Document.Address.ResourceId;
            baselineById.TryGetValue(id, out ResourceDocument? original);
            bool contentChanged = original is null
                || !string.Equals(row.Document.PayloadSha256, original.PayloadSha256, StringComparison.OrdinalIgnoreCase);

            if (row.Operation == CsvProjectionOperation.None)
            {
                if (contentChanged)
                {
                    results.Add(new ProviderPlannedChange(
                        $"csv-{id}",
                        ProviderChangeDisposition.Invalid,
                        original?.VersionToken,
                        [CsvContentCodec.Error(
                            "csv.projection.operation.required",
                            "Edited projection content is ignored until operation is explicitly set to 'upsert'.",
                            id)]));
                }

                continue;
            }

            if (row.Operation == CsvProjectionOperation.Delete)
            {
                if (original is null)
                {
                    results.Add(Invalid(id, "csv.projection.delete.missing", "Cannot delete a resource absent from the baseline."));
                    continue;
                }

                if (!BaseMatches(row, original))
                {
                    results.Add(Conflict(id, original, "Projection delete uses stale base coordinates."));
                    continue;
                }

                ResourceChange change = new(
                    $"csv-delete-{id}",
                    ResourceChangeKind.Delete,
                    original.Address,
                    Content: null,
                    original.VersionToken,
                    PayloadSha256: null,
                    CreateProposalProvenance(providerId, row, baseline));
                proposed.Add(change);
                results.Add(Ready(change.ChangeId, original.VersionToken));
                continue;
            }

            if (original is null)
            {
                if (!string.IsNullOrEmpty(row.BaseVersionToken) || !string.IsNullOrEmpty(row.BasePayloadSha256))
                {
                    results.Add(Invalid(
                        id,
                        "csv.projection.create.base-present",
                        "A new projection row must leave both base coordinates empty."));
                    continue;
                }

                ResourceChange create = new(
                    $"csv-create-{id}",
                    ResourceChangeKind.Create,
                    row.Document.Address,
                    row.Document.Content,
                    ExpectedVersionToken: null,
                    row.Document.PayloadSha256,
                    CreateProposalProvenance(providerId, row, baseline));
                proposed.Add(create);
                results.Add(Ready(create.ChangeId, null));
                continue;
            }

            if (!BaseMatches(row, original))
            {
                results.Add(Conflict(id, original, "Projection update uses stale base coordinates."));
                continue;
            }

            if (!contentChanged)
            {
                results.Add(Invalid(id, "csv.projection.update.no-change", "Upsert is marked but content did not change."));
                continue;
            }

            ResourceChange update = new(
                $"csv-update-{id}",
                ResourceChangeKind.Update,
                original.Address,
                row.Document.Content,
                original.VersionToken,
                row.Document.PayloadSha256,
                CreateProposalProvenance(providerId, row, baseline));
            proposed.Add(update);
            results.Add(Ready(update.ChangeId, original.VersionToken));
        }

        HashSet<string> projectionIds = table.Rows
            .Select(row => row.Document.Address.ResourceId)
            .ToHashSet(StringComparer.Ordinal);
        int omittedCount = baseline.Resources.Count(resource => !projectionIds.Contains(resource.Address.ResourceId));
        if (omittedCount > 0)
        {
            diagnostics.Add(new ProviderDiagnostic(
                "csv.projection.rows.omitted",
                ProviderDiagnosticSeverity.Info,
                $"{omittedCount} baseline row(s) are absent from the projection and were not interpreted as deletes; deletion requires an explicit 'delete' operation.",
                baseline.SnapshotId));
        }

        bool hasFailure = diagnostics.Any(diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error)
            || results.Any(result => result.Disposition != ProviderChangeDisposition.Ready);
        ProviderPlanChangesRequest? request = proposed.Count > 0 && !hasFailure
            ? new ProviderPlanChangesRequest(
                baseline.ResourceType,
                baseline.ResourceSet,
                baseline.Authority.AuthorityEpoch,
                proposed)
            : null;
        return new CsvReconciliationPreview(request, results, diagnostics);
    }

    public static Task<ProviderPlanChangesResponse> PlanAsync(
        ResourceProviderClient authorityClient,
        CsvReconciliationPreview preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorityClient);
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.CanPlan || preview.PlanRequest is null)
        {
            throw new InvalidOperationException("CSV reconciliation preview contains no conflict-free explicit change plan.");
        }

        return authorityClient.PlanChangesAsync(preview.PlanRequest, cancellationToken);
    }

    public static Task<ProviderApplyChangesResponse> ApplyAsync(
        ResourceProviderClient authorityClient,
        ProviderPlanChangesResponse plan,
        string resourceType,
        string resourceSet,
        string idempotencyKey,
        ProviderApproval approval,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorityClient);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.RequiresExplicitApproval
            || plan.Changes.Count == 0
            || plan.Changes.Any(change => change.Disposition != ProviderChangeDisposition.Ready))
        {
            throw new InvalidOperationException("Only a reviewed, conflict-free plan requiring explicit approval can be applied.");
        }

        return authorityClient.ApplyChangesAsync(
            new ProviderApplyChangesRequest(
                resourceType,
                resourceSet,
                plan.PlanId,
                plan.PlanDigest,
                idempotencyKey,
                plan.AuthorityEpoch,
                approval),
            cancellationToken);
    }

    private static CsvProviderOptions CreateOptions(
        ResourceSnapshot source,
        string outputDirectory,
        string providerId,
        string providerVersion)
    {
        return new CsvProviderOptions
        {
            RootDirectory = outputDirectory,
            ResourceSet = source.ResourceSet,
            ProviderId = providerId,
            ProviderVersion = providerVersion,
            Role = ResourceProviderRole.ReadOnlyEnricher,
            AuthorityProviderId = source.Authority.AuthorityProviderId,
            AuthorityEpoch = source.Authority.AuthorityEpoch
        };
    }

    private static bool BaseMatches(CsvRowDocument row, ResourceDocument original)
    {
        return string.Equals(row.BaseVersionToken, original.VersionToken, StringComparison.Ordinal)
            && string.Equals(row.BasePayloadSha256, original.PayloadSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static ResourceProvenance CreateProposalProvenance(
        string providerId,
        CsvRowDocument row,
        ResourceSnapshot baseline)
    {
        return new ResourceProvenance(
            providerId,
            row.Document.Provenance.RecordedAtUtc,
            row.Document.Provenance.Actor,
            baseline.SnapshotId,
            row.Document.PayloadSha256,
            TransferId: null,
            ApprovalReference: null);
    }

    private static ProviderPlannedChange Ready(string changeId, string? observedVersion)
    {
        return new ProviderPlannedChange(changeId, ProviderChangeDisposition.Ready, observedVersion, []);
    }

    private static ProviderPlannedChange Invalid(string id, string code, string message)
    {
        return new ProviderPlannedChange(
            $"csv-{id}",
            ProviderChangeDisposition.Invalid,
            ObservedVersionToken: null,
            [CsvContentCodec.Error(code, message, id)]);
    }

    private static ProviderPlannedChange Conflict(string id, ResourceDocument original, string message)
    {
        return new ProviderPlannedChange(
            $"csv-{id}",
            ProviderChangeDisposition.Conflict,
            original.VersionToken,
            [CsvContentCodec.Error("csv.projection.base.stale", message, id)]);
    }
}
