using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Core;

namespace Workbench.CsvProvider;

public sealed class CsvResourceProvider : IResourceProvider
{
    private static readonly JsonSerializerOptions stateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CsvProviderOptions options;
    private readonly CsvFileStore store;
    private readonly TimeProvider timeProvider;

    public CsvResourceProvider(CsvProviderOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.options = options;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.store = new CsvFileStore(options, this.timeProvider);
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> operations = this.options.Role == ResourceProviderRole.Authority
            ? ResourceProviderOperations.All
            :
            [
                ResourceProviderOperations.Handshake,
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                ResourceProviderOperations.Health
            ];
        return Task.FromResult(new ProviderCapabilities(
            this.options.ProviderId,
            "Workbench CSV provider",
            this.options.ProviderVersion,
            [ResourceProviderProtocol.Version1],
            operations,
            CsvResourceTypes.All,
            [this.options.Role],
            new ProviderBatchCapabilities(this.options.MaxBatchSize, AtomicApply: true, OrderedResults: true),
            SupportsIdempotency: true,
            SupportsOptimisticConcurrency: true,
            SupportsOfflineSnapshots: true));
    }

    public Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.ValidateScope(request.ResourceType, request.ResourceSet);
        CsvTable table = this.Load(request.ResourceType);
        IEnumerable<CsvRowDocument> query = table.Rows;
        List<ProviderDiagnostic> diagnostics = [];
        if (request.Filter is { } filter)
        {
            query = ApplyFilter(query, filter);
        }

        int offset = ParseContinuationToken(request.ContinuationToken);
        List<CsvRowDocument> page = query
            .OrderBy(row => row.Document.Address.ResourceId, StringComparer.Ordinal)
            .Skip(offset)
            .Take(request.PageSize + 1)
            .ToList();
        string? continuation = page.Count > request.PageSize
            ? (offset + request.PageSize).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;
        IReadOnlyList<ResourceSummary> resources = page
            .Take(request.PageSize)
            .Select(row => new ResourceSummary(
                row.Document.Address,
                row.Document.VersionToken,
                row.Document.PayloadSha256,
                row.Document.Provenance.RecordedAtUtc))
            .ToList();
        return Task.FromResult(new ProviderQueryResponse(resources, continuation, diagnostics));
    }

    public Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ResourceDocument> resources = [];
        List<ProviderDiagnostic> diagnostics = [];
        foreach (IGrouping<string, ResourceAddress> group in request.Resources.GroupBy(
                     address => address.ResourceType,
                     StringComparer.Ordinal))
        {
            this.ValidateScope(group.Key, group.First().ResourceSet);
            Dictionary<string, CsvRowDocument> byId = this.Load(group.Key).Rows.ToDictionary(
                row => row.Document.Address.ResourceId,
                StringComparer.Ordinal);
            foreach (ResourceAddress address in group)
            {
                this.ValidateScope(address.ResourceType, address.ResourceSet);
                if (byId.TryGetValue(address.ResourceId, out CsvRowDocument? row))
                {
                    resources.Add(row.Document);
                }
                else
                {
                    diagnostics.Add(new ProviderDiagnostic(
                        "csv.resource.not-found",
                        ProviderDiagnosticSeverity.Error,
                        $"CSV resource '{address.ResourceId}' was not found.",
                        address.ResourceId));
                }
            }
        }

        return Task.FromResult(new ProviderGetResponse(resources, diagnostics));
    }

    public Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ProviderValidationResult> results = [];
        foreach (ResourceSnapshot snapshot in request.Snapshots)
        {
            List<ProviderDiagnostic> diagnostics = [];
            if (!string.Equals(snapshot.ResourceSet, this.options.ResourceSet, StringComparison.Ordinal))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.scope.resource-set.mismatch",
                    $"Snapshot resource set '{snapshot.ResourceSet}' does not match '{this.options.ResourceSet}'.",
                    snapshot.SnapshotId));
            }

            foreach (ResourceDocument resource in snapshot.Resources)
            {
                ValidateDocument(snapshot.ResourceType, snapshot.ResourceSet, resource, diagnostics);
            }

            string computedSnapshotDigest = CsvFileStore.ComputeSnapshotDigest(snapshot.Resources);
            if (!string.Equals(computedSnapshotDigest, snapshot.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.snapshot.digest.mismatch",
                    "Snapshot payload digest does not match its resources.",
                    snapshot.SnapshotId));
            }

            results.Add(new ProviderValidationResult(
                snapshot.SnapshotId,
                snapshot.PayloadSha256,
                diagnostics.All(diagnostic => diagnostic.Severity != ProviderDiagnosticSeverity.Error),
                snapshot.CompatibilityPin,
                diagnostics));
        }

        return Task.FromResult(new ProviderValidateResponse(results));
    }

    public Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.EnsureAuthority();
        this.ValidateScope(request.ResourceType, request.ResourceSet);
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        if (!string.Equals(request.ExpectedAuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePlanConflict(
                request,
                now,
                "csv.authority.epoch.stale",
                "Expected authority epoch is stale or belongs to another authority."));
        }

        CsvTable table = this.Load(request.ResourceType);
        Dictionary<string, CsvRowDocument> current = table.Rows.ToDictionary(
            row => row.Document.Address.ResourceId,
            StringComparer.Ordinal);
        List<ProviderPlannedChange> results = [];
        HashSet<string> changeIds = new(StringComparer.Ordinal);
        foreach (ResourceChange change in request.Changes)
        {
            if (!changeIds.Add(change.ChangeId))
            {
                results.Add(Invalid(change, "csv.plan.change-id.duplicate", "Change IDs must be unique within a plan."));
                continue;
            }

            results.Add(this.PlanChange(request, change, current));
        }

        IReadOnlyDictionary<string, string> sourcePayloads = current.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Document.PayloadSha256,
            StringComparer.Ordinal);
        DateTimeOffset expires = now.AddMinutes(this.options.PlanLifetimeMinutes);
        string planDigest = ComputePlanDigest(request, now, expires, table.FileDigest, sourcePayloads, results);
        string planId = $"csv-plan-{planDigest[..24]}";
        PersistedCsvPlan plan = new(
            planId,
            planDigest,
            this.options.AuthorityEpoch,
            now,
            expires,
            request.ResourceType,
            request.ResourceSet,
            table.FileDigest,
            sourcePayloads,
            request.Changes,
            results);
        WriteState(GetPlanPath(planId), plan);
        return Task.FromResult(new ProviderPlanChangesResponse(
            planId,
            planDigest,
            this.options.AuthorityEpoch,
            now,
            expires,
            RequiresExplicitApproval: true,
            results,
            []));
    }

    public Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.EnsureAuthority();
        this.ValidateScope(request.ResourceType, request.ResourceSet);
        Directory.CreateDirectory(this.options.StateDirectory);
        using FileStream applyLock = AcquireApplyLock(this.options.StateDirectory);

        string applyPath = GetApplyPath(request.IdempotencyKey);
        PersistedCsvApply? priorApply = ReadState<PersistedCsvApply>(applyPath);
        if (priorApply is not null)
        {
            if (!string.Equals(priorApply.PlanId, request.PlanId, StringComparison.Ordinal)
                || !string.Equals(priorApply.PlanDigest, request.PlanDigest, StringComparison.Ordinal))
            {
                return Task.FromResult(ApplyResult(
                    ProviderApplyStatus.Conflict,
                    request,
                    null,
                    "csv.idempotency.reused",
                    "The idempotency key was already used with a different plan."));
            }

            if (!this.IsValidPersistedApply(priorApply, request))
            {
                return Task.FromResult(ApplyResult(
                    ProviderApplyStatus.Conflict,
                    request,
                    null,
                    "csv.idempotency.receipt.invalid",
                    "The stored CSV apply receipt is missing or failed its digest and coordinate checks."));
            }

            return Task.FromResult(new ProviderApplyChangesResponse(
                ProviderApplyStatus.AlreadyApplied,
                request.PlanId,
                request.IdempotencyKey,
                this.options.AuthorityEpoch,
                priorApply.Snapshot,
                []));
        }

        PersistedCsvPlan? plan = ReadState<PersistedCsvPlan>(GetPlanPath(request.PlanId));
        if (plan is null)
        {
            return Task.FromResult(ApplyResult(
                ProviderApplyStatus.Rejected,
                request,
                null,
                "csv.plan.not-found",
                "The immutable CSV change plan was not found."));
        }

        ProviderApplyChangesResponse? invalidPlan = this.ValidateApplyCoordinates(request, plan);
        if (invalidPlan is not null)
        {
            return Task.FromResult(invalidPlan);
        }

        CsvTable table = this.Load(request.ResourceType);
        if (!string.Equals(table.FileDigest, plan.SourceFileDigest, StringComparison.Ordinal))
        {
            if (PlanMatchesCurrentState(plan, table))
            {
                ResourceSnapshot alreadyAppliedSnapshot = this.store.CreateSnapshot(
                    request.ResourceType,
                    table,
                    authoritative: true);
                this.WriteApplyState(applyPath, request, alreadyAppliedSnapshot);
                return Task.FromResult(new ProviderApplyChangesResponse(
                    ProviderApplyStatus.AlreadyApplied,
                    request.PlanId,
                    request.IdempotencyKey,
                    this.options.AuthorityEpoch,
                    alreadyAppliedSnapshot,
                    []));
            }

            return Task.FromResult(ApplyResult(
                ProviderApplyStatus.Conflict,
                request,
                null,
                "csv.plan.source.stale",
                "The CSV file changed after planning; validate and create a new plan."));
        }

        List<IReadOnlyDictionary<string, string>> rows = table.Rows
            .Select(row => row.RawFields)
            .ToList();
        foreach (ResourceChange change in plan.Changes)
        {
            ApplyChange(rows, request.ResourceType, change, request.Approval, plan.PlanId);
        }

        this.store.Save(request.ResourceType, rows, table.Headers);
        CsvTable updated = this.Load(request.ResourceType);
        ResourceSnapshot snapshot = this.store.CreateSnapshot(request.ResourceType, updated, authoritative: true);
        this.WriteApplyState(applyPath, request, snapshot);
        return Task.FromResult(new ProviderApplyChangesResponse(
            ProviderApplyStatus.Applied,
            request.PlanId,
            request.IdempotencyKey,
            this.options.AuthorityEpoch,
            snapshot,
            []));
    }

    public Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ProviderDiagnostic> diagnostics = [];
        ProviderHealthState state = ProviderHealthState.Healthy;
        try
        {
            Directory.CreateDirectory(this.options.RootDirectory);
            if (request.Deep)
            {
                foreach (string resourceType in CsvResourceTypes.All)
                {
                    _ = this.Load(resourceType);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ResourceProviderProtocolException)
        {
            state = ProviderHealthState.Unavailable;
            diagnostics.Add(new ProviderDiagnostic(
                "csv.health.storage.unavailable",
                ProviderDiagnosticSeverity.Error,
                exception.Message,
                this.options.RootDirectory,
                Retryable: exception is IOException));
        }

        return Task.FromResult(new ProviderHealthResponse(
            state,
            this.timeProvider.GetUtcNow(),
            this.options.ProviderVersion,
            diagnostics));
    }

    private CsvTable Load(string resourceType)
    {
        CsvRecordMode mode = this.options.Role == ResourceProviderRole.Authority
            ? CsvRecordMode.Authoritative
            : CsvRecordMode.Projection;
        return this.store.Load(resourceType, mode);
    }

    private ProviderPlannedChange PlanChange(
        ProviderPlanChangesRequest request,
        ResourceChange change,
        IReadOnlyDictionary<string, CsvRowDocument> current)
    {
        if (!string.Equals(change.Address.ResourceType, request.ResourceType, StringComparison.Ordinal)
            || !string.Equals(change.Address.ResourceSet, request.ResourceSet, StringComparison.Ordinal))
        {
            return Invalid(change, "csv.plan.scope.mismatch", "Change address is outside the planned resource scope.");
        }

        current.TryGetValue(change.Address.ResourceId, out CsvRowDocument? existing);
        if (change.Kind == ResourceChangeKind.Create && existing is not null)
        {
            return Conflict(change, existing.Document.VersionToken, "csv.resource.exists", "Create target already exists.");
        }

        if (change.Kind is ResourceChangeKind.Update or ResourceChangeKind.Delete && existing is null)
        {
            return Conflict(change, null, "csv.resource.missing", "Update or delete target does not exist.");
        }

        if (change.Kind is ResourceChangeKind.Update or ResourceChangeKind.Delete
            && !string.Equals(change.ExpectedVersionToken, existing!.Document.VersionToken, StringComparison.Ordinal))
        {
            return Conflict(
                change,
                existing.Document.VersionToken,
                "csv.resource.version.stale",
                "Expected resource version is stale.");
        }

        if (change.Kind is ResourceChangeKind.Publish or ResourceChangeKind.TransferAuthority)
        {
            return new ProviderPlannedChange(
                change.ChangeId,
                ProviderChangeDisposition.Unsupported,
                existing?.Document.VersionToken,
                [CsvContentCodec.Error(
                    "csv.change-kind.unsupported",
                    $"CSV authority does not support change kind '{change.Kind}'.",
                    change.ChangeId)]);
        }

        if (change.Kind == ResourceChangeKind.Delete)
        {
            if (change.Content is not null || !string.IsNullOrEmpty(change.PayloadSha256))
            {
                return Invalid(change, "csv.delete.payload.invalid", "Delete changes must not carry content or a payload digest.");
            }

            return Ready(change, existing!.Document.VersionToken, []);
        }

        if (change.Content is not { } content || string.IsNullOrWhiteSpace(change.PayloadSha256))
        {
            return Invalid(change, "csv.change.payload.required", "Create and update changes require content and payload digest.");
        }

        List<ProviderDiagnostic> diagnostics = [];
        try
        {
            ResourceAuthorityDeclaration authority = CreateAuthority(request.ResourceType);
            ResourceProvenance provenance = change.Provenance;
            IReadOnlyDictionary<string, string>? extensions = existing is null
                ? null
                : GetExtensions(request.ResourceType, existing.RawFields);
            IReadOnlyDictionary<string, string> fields = CsvContentCodec.CreateFields(
                request.ResourceType,
                request.ResourceSet,
                content,
                CsvRecordMode.Authoritative,
                CsvProjectionOperation.None,
                this.options.ProviderId,
                this.options.ProviderVersion,
                authority,
                baseVersionToken: "pending",
                basePayloadSha256: "pending",
                provenance,
                extensions);
            CsvContent normalized = CsvContentCodec.ParseContent(
                request.ResourceType,
                fields,
                diagnostics,
                change.ChangeId);
            if (!string.Equals(normalized.Id, change.Address.ResourceId, StringComparison.Ordinal))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.content.id.mismatch",
                    "Content ID must match the resource address ID.",
                    change.ChangeId));
            }

            string computedDigest = CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(normalized.Json));
            if (!string.Equals(computedDigest, change.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.change.payload-digest.mismatch",
                    "Change payload digest does not match canonical content.",
                    change.ChangeId));
            }
        }
        catch (ResourceProviderProtocolException exception)
        {
            diagnostics.AddRange(exception.Diagnostics.Count > 0
                ? exception.Diagnostics
                : [CsvContentCodec.Error(exception.Error.Code, exception.Error.Message, change.ChangeId)]);
        }

        return diagnostics.Any(diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error)
            ? new ProviderPlannedChange(
                change.ChangeId,
                ProviderChangeDisposition.Invalid,
                existing?.Document.VersionToken,
                diagnostics)
            : Ready(change, existing?.Document.VersionToken, diagnostics);
    }

    private ProviderApplyChangesResponse? ValidateApplyCoordinates(
        ProviderApplyChangesRequest request,
        PersistedCsvPlan plan)
    {
        if (plan.SourcePayloads is null)
        {
            return ApplyResult(
                ProviderApplyStatus.Conflict,
                request,
                null,
                "csv.plan.integrity.invalid",
                "The persisted plan is missing its source resource coordinates.");
        }

        ProviderPlanChangesRequest plannedRequest = new(
            plan.ResourceType,
            plan.ResourceSet,
            plan.AuthorityEpoch,
            plan.Changes);
        string computedPlanDigest = ComputePlanDigest(
            plannedRequest,
            plan.CreatedAtUtc,
            plan.ExpiresAtUtc,
            plan.SourceFileDigest,
            plan.SourcePayloads,
            plan.Results);
        string computedPlanId = $"csv-plan-{computedPlanDigest[..24]}";
        if (!string.Equals(plan.PlanDigest, computedPlanDigest, StringComparison.Ordinal)
            || !string.Equals(plan.PlanId, computedPlanId, StringComparison.Ordinal)
            || !string.Equals(request.PlanId, plan.PlanId, StringComparison.Ordinal)
            || !string.Equals(request.PlanDigest, plan.PlanDigest, StringComparison.Ordinal)
            || !string.Equals(request.ResourceType, plan.ResourceType, StringComparison.Ordinal)
            || !string.Equals(request.ResourceSet, plan.ResourceSet, StringComparison.Ordinal))
        {
            return ApplyResult(
                ProviderApplyStatus.Conflict,
                request,
                null,
                "csv.plan.integrity.invalid",
                "Apply coordinates or persisted plan content do not match the immutable plan digest.");
        }

        if (!string.Equals(request.ExpectedAuthorityEpoch, plan.AuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedAuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            return ApplyResult(
                ProviderApplyStatus.Conflict,
                request,
                null,
                "csv.authority.epoch.stale",
                "Authority epoch changed after planning.");
        }

        if (this.timeProvider.GetUtcNow() > plan.ExpiresAtUtc)
        {
            return ApplyResult(
                ProviderApplyStatus.Rejected,
                request,
                null,
                "csv.plan.expired",
                "The CSV change plan expired; create and approve a new plan.");
        }

        if (plan.Results.Any(result => result.Disposition != ProviderChangeDisposition.Ready))
        {
            return ApplyResult(
                ProviderApplyStatus.Rejected,
                request,
                null,
                "csv.plan.not-ready",
                "A plan containing conflicts, invalid changes, or unsupported changes cannot be applied.");
        }

        return null;
    }

    private void ApplyChange(
        IList<IReadOnlyDictionary<string, string>> rows,
        string resourceType,
        ResourceChange change,
        ProviderApproval approval,
        string planId)
    {
        int index = rows
            .Select((row, rowIndex) => (row, rowIndex))
            .FirstOrDefault(pair => string.Equals(
                pair.row.TryGetValue("resource_id", out string? id) ? id : null,
                change.Address.ResourceId,
                StringComparison.Ordinal))
            .rowIndex;
        bool found = rows.Count > 0
            && index >= 0
            && index < rows.Count
            && rows[index].TryGetValue("resource_id", out string? foundId)
            && string.Equals(foundId, change.Address.ResourceId, StringComparison.Ordinal);

        if (change.Kind == ResourceChangeKind.Delete)
        {
            if (found)
            {
                rows.RemoveAt(index);
            }

            return;
        }

        JsonElement content = change.Content!.Value;
        string payloadSha256 = CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(content));
        string versionToken = $"csv-v1:{payloadSha256}";
        IReadOnlyDictionary<string, string>? extensions = found
            ? GetExtensions(resourceType, rows[index])
            : null;
        ResourceProvenance provenance = new(
            this.options.ProviderId,
            this.timeProvider.GetUtcNow(),
            approval.ApprovedBy,
            planId,
            payloadSha256,
            TransferId: null,
            approval.ApprovalReference);
        IReadOnlyDictionary<string, string> replacement = CsvContentCodec.CreateFields(
            resourceType,
            this.options.ResourceSet,
            content,
            CsvRecordMode.Authoritative,
            CsvProjectionOperation.None,
            this.options.ProviderId,
            this.options.ProviderVersion,
            CreateAuthority(resourceType),
            versionToken,
            payloadSha256,
            provenance,
            extensions);
        if (found)
        {
            rows[index] = replacement;
        }
        else
        {
            rows.Add(replacement);
        }
    }

    private static bool PlanMatchesCurrentState(PersistedCsvPlan plan, CsvTable table)
    {
        if (plan.SourcePayloads is null)
        {
            return false;
        }

        Dictionary<string, string> expected = new(plan.SourcePayloads, StringComparer.Ordinal);
        foreach (ResourceChange change in plan.Changes)
        {
            if (change.Kind == ResourceChangeKind.Delete)
            {
                expected.Remove(change.Address.ResourceId);
            }
            else if (change.PayloadSha256 is { } payloadSha256)
            {
                expected[change.Address.ResourceId] = payloadSha256;
            }
        }

        Dictionary<string, ResourceDocument> current = table.Rows.ToDictionary(
            row => row.Document.Address.ResourceId,
            row => row.Document,
            StringComparer.Ordinal);
        if (current.Count != expected.Count)
        {
            return false;
        }

        return expected.All(pair => current.TryGetValue(pair.Key, out ResourceDocument? resource)
            && string.Equals(resource.PayloadSha256, pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateDocument(
        string resourceType,
        string resourceSet,
        ResourceDocument resource,
        ICollection<ProviderDiagnostic> diagnostics)
    {
        if (!string.Equals(resource.Address.ResourceType, resourceType, StringComparison.Ordinal)
            || !string.Equals(resource.Address.ResourceSet, resourceSet, StringComparison.Ordinal))
        {
            diagnostics.Add(CsvContentCodec.Error(
                "csv.snapshot.resource.scope-mismatch",
                "Resource address does not match its snapshot scope.",
                resource.Address.ResourceId));
        }

        try
        {
            string id = resource.Content.ValueKind == JsonValueKind.Object
                && resource.Content.TryGetProperty("id", out JsonElement idElement)
                && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString() ?? string.Empty
                    : string.Empty;
            if (!string.Equals(id, resource.Address.ResourceId, StringComparison.Ordinal))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.content.id.mismatch",
                    "Content ID does not match the resource address.",
                    resource.Address.ResourceId));
            }

            string digest = CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(resource.Content));
            if (!string.Equals(digest, resource.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CsvContentCodec.Error(
                    "csv.resource.digest.mismatch",
                    "Resource payload digest does not match canonical content.",
                    resource.Address.ResourceId));
            }

            ValidateJsonShape(resourceType, resource.Content, diagnostics, resource.Address.ResourceId);
        }
        catch (ResourceProviderProtocolException exception)
        {
            if (exception.Diagnostics.Count > 0)
            {
                foreach (ProviderDiagnostic diagnostic in exception.Diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }
            }
            else
            {
                diagnostics.Add(CsvContentCodec.Error(
                    exception.Error.Code,
                    exception.Error.Message,
                    resource.Address.ResourceId));
            }
        }
    }

    private static void ValidateJsonShape(
        string resourceType,
        JsonElement content,
        ICollection<ProviderDiagnostic> diagnostics,
        string target)
    {
        ResourceAuthorityDeclaration authority = new(resourceType, "validation", "validation", "validation", DateTimeOffset.UnixEpoch, null);
        ResourceProvenance provenance = new("validation", DateTimeOffset.UnixEpoch, null, null, new string('0', 64), null, null);
        IReadOnlyDictionary<string, string> fields = CsvContentCodec.CreateFields(
            resourceType,
            "validation",
            content,
            CsvRecordMode.Projection,
            CsvProjectionOperation.None,
            "validation",
            CsvProviderOptions.CurrentProviderVersion,
            authority,
            "validation",
            new string('0', 64),
            provenance);
        _ = CsvContentCodec.ParseContent(resourceType, fields, diagnostics, target);
    }

    private static IEnumerable<CsvRowDocument> ApplyFilter(IEnumerable<CsvRowDocument> rows, JsonElement filter)
    {
        if (filter.ValueKind != JsonValueKind.Object)
        {
            throw CsvContentCodec.InvalidContent("csv.query.filter.invalid", "CSV query filter must be a JSON object.");
        }

        HashSet<string> known = ["status", "idPrefix"];
        string? unknownName = filter.EnumerateObject()
            .Select(property => property.Name)
            .FirstOrDefault(name => !known.Contains(name));
        if (unknownName is not null)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.query.filter.unsupported",
                $"Unsupported CSV query filter '{unknownName}'.");
        }

        if (filter.TryGetProperty("status", out JsonElement status))
        {
            if (status.ValueKind != JsonValueKind.String)
            {
                throw CsvContentCodec.InvalidContent("csv.query.filter.invalid", "Filter 'status' must be a string.");
            }

            string expected = status.GetString() ?? string.Empty;
            rows = rows.Where(row => row.Document.Content.TryGetProperty("status", out JsonElement value)
                && string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.TryGetProperty("idPrefix", out JsonElement idPrefix))
        {
            if (idPrefix.ValueKind != JsonValueKind.String)
            {
                throw CsvContentCodec.InvalidContent("csv.query.filter.invalid", "Filter 'idPrefix' must be a string.");
            }

            string expected = idPrefix.GetString() ?? string.Empty;
            rows = rows.Where(row => row.Document.Address.ResourceId.StartsWith(expected, StringComparison.Ordinal));
        }

        return rows;
    }

    private static int ParseContinuationToken(string? token)
    {
        if (token is null)
        {
            return 0;
        }

        if (!int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int offset)
            || offset < 0)
        {
            throw CsvContentCodec.InvalidContent("csv.query.continuation.invalid", "CSV continuation token is invalid.");
        }

        return offset;
    }

    private static ProviderPlannedChange Ready(
        ResourceChange change,
        string? observedVersion,
        IReadOnlyList<ProviderDiagnostic> diagnostics)
    {
        return new ProviderPlannedChange(change.ChangeId, ProviderChangeDisposition.Ready, observedVersion, diagnostics);
    }

    private static ProviderPlannedChange Invalid(ResourceChange change, string code, string message)
    {
        return new ProviderPlannedChange(
            change.ChangeId,
            ProviderChangeDisposition.Invalid,
            ObservedVersionToken: null,
            [CsvContentCodec.Error(code, message, change.ChangeId)]);
    }

    private static ProviderPlannedChange Conflict(
        ResourceChange change,
        string? observedVersion,
        string code,
        string message)
    {
        return new ProviderPlannedChange(
            change.ChangeId,
            ProviderChangeDisposition.Conflict,
            observedVersion,
            [CsvContentCodec.Error(code, message, change.ChangeId)]);
    }

    private static ProviderPlanChangesResponse CreatePlanConflict(
        ProviderPlanChangesRequest request,
        DateTimeOffset now,
        string code,
        string message)
    {
        IReadOnlyList<ProviderPlannedChange> changes = request.Changes
            .Select(change => Conflict(change, null, code, message))
            .ToList();
        string digest = CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(JsonSerializer.SerializeToElement(new
        {
            request.ResourceType,
            request.ResourceSet,
            request.ExpectedAuthorityEpoch,
            code
        })));
        return new ProviderPlanChangesResponse(
            $"csv-conflict-{digest[..24]}",
            digest,
            request.ExpectedAuthorityEpoch,
            now,
            now,
            RequiresExplicitApproval: true,
            changes,
            [CsvContentCodec.Error(code, message, request.ResourceSet)]);
    }

    private static ProviderApplyChangesResponse ApplyResult(
        ProviderApplyStatus status,
        ProviderApplyChangesRequest request,
        ResourceSnapshot? snapshot,
        string code,
        string message)
    {
        return new ProviderApplyChangesResponse(
            status,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            snapshot,
            [CsvContentCodec.Error(code, message, request.PlanId)]);
    }

    private static string ComputePlanDigest(
        ProviderPlanChangesRequest request,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string fileDigest,
        IReadOnlyDictionary<string, string> sourcePayloads,
        IReadOnlyList<ProviderPlannedChange> results)
    {
        JsonElement json = JsonSerializer.SerializeToElement(new
        {
            request.ResourceType,
            request.ResourceSet,
            request.ExpectedAuthorityEpoch,
            request.Changes,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = expiresAt,
            SourceFileDigest = fileDigest,
            SourcePayloads = sourcePayloads.OrderBy(static pair => pair.Key, StringComparer.Ordinal),
            Results = results
        }, stateJsonOptions);
        return CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(json));
    }

    private ResourceAuthorityDeclaration CreateAuthority(string resourceType)
    {
        return new ResourceAuthorityDeclaration(
            resourceType,
            this.options.ResourceSet,
            this.options.AuthorityProviderId,
            this.options.AuthorityEpoch,
            this.timeProvider.GetUtcNow(),
            DeclarationDigest: null);
    }

    private static IReadOnlyDictionary<string, string> GetExtensions(
        string resourceType,
        IReadOnlyDictionary<string, string> fields)
    {
        HashSet<string> reserved = CsvContentCodec.GetHeaders(resourceType).ToHashSet(StringComparer.Ordinal);
        return fields
            .Where(pair => !reserved.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private void WriteApplyState(
        string path,
        ProviderApplyChangesRequest request,
        ResourceSnapshot snapshot)
    {
        WriteState(path, new PersistedCsvApply(
            request.IdempotencyKey,
            request.PlanId,
            request.PlanDigest,
            this.timeProvider.GetUtcNow(),
            snapshot.SnapshotId,
            snapshot.PayloadSha256,
            snapshot));
    }

    private bool IsValidPersistedApply(PersistedCsvApply apply, ProviderApplyChangesRequest request)
    {
        ResourceSnapshot? snapshot = apply.Snapshot;
        if (snapshot is null
            || !string.Equals(apply.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            || !string.Equals(apply.SnapshotId, snapshot.SnapshotId, StringComparison.Ordinal)
            || !string.Equals(apply.SnapshotPayloadSha256, snapshot.PayloadSha256, StringComparison.Ordinal)
            || !string.Equals(snapshot.ResourceType, request.ResourceType, StringComparison.Ordinal)
            || !string.Equals(snapshot.ResourceSet, request.ResourceSet, StringComparison.Ordinal)
            || !string.Equals(snapshot.Authority.AuthorityProviderId, this.options.AuthorityProviderId, StringComparison.Ordinal)
            || !string.Equals(snapshot.Authority.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(
                CsvFileStore.ComputeSnapshotDigest(snapshot.Resources),
                snapshot.PayloadSha256,
                StringComparison.Ordinal))
        {
            return false;
        }

        return snapshot.Resources.All(resource =>
            string.Equals(
                CsvFileStore.HashBytes(CsvCanonicalJson.Canonicalize(resource.Content)),
                resource.PayloadSha256,
                StringComparison.Ordinal));
    }

    private string GetPlanPath(string planId)
    {
        return Path.Combine(this.options.StateDirectory, "plans", $"{planId}.json");
    }

    private string GetApplyPath(string idempotencyKey)
    {
        string digest = CsvFileStore.HashBytes(System.Text.Encoding.UTF8.GetBytes(idempotencyKey));
        return Path.Combine(this.options.StateDirectory, "applied", $"{digest}.json");
    }

    private static T? ReadState<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), stateJsonOptions)
            ?? throw CsvContentCodec.InvalidContent("csv.state.invalid", $"CSV provider state '{path}' is empty.");
    }

    private static void WriteState<T>(string path, T state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, stateJsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static FileStream AcquireApplyLock(string stateDirectory)
    {
        try
        {
            return new FileStream(
                Path.Combine(stateDirectory, "apply.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException exception)
        {
            throw new ResourceProviderProtocolException(
                new ProviderError("csv.apply.locked", "Another CSV apply is in progress.", Retryable: true),
                [new ProviderDiagnostic(
                    "csv.apply.locked",
                    ProviderDiagnosticSeverity.Error,
                    exception.Message,
                    stateDirectory,
                    Retryable: true)]);
        }
    }

    private void EnsureAuthority()
    {
        if (this.options.Role != ResourceProviderRole.Authority)
        {
            throw new ResourceProviderProtocolException(new ProviderError(
                "csv.role.read-only",
                "Editable CSV projections are read-only providers; send their reconciliation proposal to the declared authority."));
        }
    }

    private void ValidateScope(string resourceType, string resourceSet)
    {
        if (!CsvResourceTypes.All.Contains(resourceType, StringComparer.Ordinal))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.resource-type.unsupported",
                $"CSV provider does not support resource type '{resourceType}'.");
        }

        if (!string.Equals(resourceSet, this.options.ResourceSet, StringComparison.Ordinal))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.resource-set.unsupported",
                $"CSV provider is bound to resource set '{this.options.ResourceSet}', not '{resourceSet}'.");
        }
    }
}
