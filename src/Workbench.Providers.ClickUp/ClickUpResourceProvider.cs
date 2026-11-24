using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Workbench.Core;

namespace Workbench.Providers.ClickUp;

/// <summary>Process-isolated ClickUp implementation of the provider-neutral execution-resource contract.</summary>
public sealed class ClickUpResourceProvider : IResourceProvider
{
    public const string ProviderId = "clickup";
    public const string ResourceType = "work_item_execution";
    public const string ProviderVersion = "1.0.0";

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ClickUpProviderOptions options;
    private readonly ClickUpApiClient api;
    private readonly ClickUpStateStore? stateStore;
    private readonly TimeProvider timeProvider;

    public ClickUpResourceProvider(
        ClickUpProviderOptions options,
        HttpClient httpClient,
        string token,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.api = new ClickUpApiClient(httpClient, options, token, delay);
        this.stateStore = options.StateDirectory is null ? null : new ClickUpStateStore(options.StateDirectory);
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> operations = this.options.Mode switch
        {
            ClickUpProviderMode.ReadOnly =>
            [
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                ResourceProviderOperations.Health
            ],
            ClickUpProviderMode.Plan =>
            [
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                ResourceProviderOperations.PlanChanges,
                ResourceProviderOperations.Health
            ],
            ClickUpProviderMode.Apply =>
            [
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                ResourceProviderOperations.PlanChanges,
                ResourceProviderOperations.ApplyChanges,
                ResourceProviderOperations.Health
            ],
            _ => throw new InvalidOperationException("Unsupported ClickUp provider mode.")
        };

        return Task.FromResult(new ProviderCapabilities(
            ProviderId,
            "ClickUp work-item execution provider",
            ProviderVersion,
            [ResourceProviderProtocol.Version1],
            operations,
            [ResourceType],
            [ResourceProviderRole.Authority, ResourceProviderRole.ReadOnlyEnricher],
            new ProviderBatchCapabilities(MaxItems: 1, AtomicApply: true, OrderedResults: true),
            SupportsIdempotency: this.options.Mode == ClickUpProviderMode.Apply,
            SupportsOptimisticConcurrency: this.options.Mode != ClickUpProviderMode.ReadOnly,
            SupportsOfflineSnapshots: true));
    }

    public async Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureScope(request.ResourceType, request.ResourceSet);
        var filterArchived = GetArchivedFilter(request.Filter);
        var continuation = DecodeContinuation(request.ContinuationToken);
        if (continuation is not null && continuation.Archived != filterArchived)
        {
            throw Failure("clickup.query.continuation.invalid", "Continuation token does not match the query filter.");
        }

        var page = continuation?.Page ?? 0;
        var offset = continuation?.Offset ?? 0;
        var apiPage = await this.api.GetTasksAsync(page, filterArchived, cancellationToken).ConfigureAwait(false);
        var hasMorePages = apiPage.LastPage == false || (apiPage.LastPage is null && apiPage.Tasks.Count == 100);
        if (offset < 0 || offset > apiPage.Tasks.Count)
        {
            throw Failure("clickup.query.continuation.invalid", "Continuation token has an invalid task offset.");
        }

        if (offset == apiPage.Tasks.Count)
        {
            var emptyContinuation = hasMorePages ? EncodeContinuation(page + 1, 0, filterArchived) : null;
            return new ProviderQueryResponse([], emptyContinuation, []);
        }

        var task = apiPage.Tasks[offset];
        var document = this.MapTask(task);
        string? next;
        if (offset + 1 < apiPage.Tasks.Count)
        {
            next = EncodeContinuation(page, offset + 1, filterArchived);
        }
        else
        {
            next = hasMorePages ? EncodeContinuation(page + 1, 0, filterArchived) : null;
        }
        return new ProviderQueryResponse(
            [new ResourceSummary(document.Address, document.VersionToken, document.PayloadSha256, ParseClickUpTime(task.DateUpdated))],
            next,
            []);
    }

    public async Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        var address = request.Resources.Single();
        this.EnsureScope(address.ResourceType, address.ResourceSet);
        var task = await this.api.GetTaskAsync(address.ResourceId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            var diagnostic = Diagnostic(
                "clickup.task.not-found",
                ProviderDiagnosticSeverity.Warning,
                "ClickUp task was not found or is not accessible.",
                address.ResourceId);
            return new ProviderGetResponse([], [diagnostic]);
        }

        this.EnsureTaskInConfiguredList(task);

        return new ProviderGetResponse([this.MapTask(task)], []);
    }

    public Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = request.Snapshots.Select(this.ValidateSnapshot).ToList();
        return Task.FromResult(new ProviderValidateResponse(results));
    }

    public async Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureModeAllowsPlan();
        this.EnsureScope(request.ResourceType, request.ResourceSet);
        if (!string.Equals(request.ExpectedAuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            throw Failure("clickup.authority-epoch.conflict", "Configured ClickUp authority epoch does not match the request.");
        }

        var change = request.Changes.Single();
        var planned = await this.PlanChangeAsync(change, cancellationToken).ConfigureAwait(false);
        change = CanonicalizePlannedChange(change, planned);
        var createdAt = this.timeProvider.GetUtcNow();
        var expiresAt = createdAt.Add(this.options.PlanLifetime);
        var planDigest = this.ComputePlanDigest(
            request.ExpectedAuthorityEpoch,
            createdAt,
            expiresAt,
            change,
            planned);
        var planId = "clickup-plan-" + planDigest[..24];
        var storedPlan = new StoredPlan(
            planId,
            planDigest,
            request.ExpectedAuthorityEpoch,
            createdAt,
            expiresAt,
            change,
            planned);
        await this.RequireStateStore().SavePlanAsync(storedPlan, cancellationToken).ConfigureAwait(false);

        var diagnostics = planned.Diagnostics;
        return new ProviderPlanChangesResponse(
            planId,
            planDigest,
            request.ExpectedAuthorityEpoch,
            createdAt,
            expiresAt,
            RequiresExplicitApproval: true,
            [planned],
            diagnostics);
    }

    public async Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (this.options.Mode != ClickUpProviderMode.Apply)
        {
            throw Failure("clickup.apply.disabled", "ClickUp provider is not configured in apply mode.");
        }

        this.EnsureScope(request.ResourceType, request.ResourceSet);
        var store = this.RequireStateStore();
        await using var applyLease = (await store.AcquireApplyLeaseAsync(cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);
        var fingerprint = DigestObject(request);
        var existingReceipt = await store.LoadReceiptAsync(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            if (!string.Equals(existingReceipt.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return ApplyConflict(
                    request,
                    "clickup.idempotency.key-reused",
                    "Idempotency key was previously used for different apply coordinates.");
            }


            if (!this.IsValidStoredReceipt(existingReceipt.Response, request))
            {
                return ApplyConflict(
                    request,
                    "clickup.idempotency.receipt.invalid",
                    "Stored ClickUp idempotency receipt failed coordinate or snapshot validation.");
            }

            return existingReceipt.Response with { Status = ProviderApplyStatus.AlreadyApplied };
        }

        var plan = await store.LoadPlanAsync(request.PlanId, cancellationToken).ConfigureAwait(false);
        var computedPlanDigest = plan is null
            ? null
            : this.ComputePlanDigest(
                plan.AuthorityEpoch,
                plan.CreatedAtUtc,
                plan.ExpiresAtUtc,
                plan.Change,
                plan.PlannedChange);
        if (plan is null
            || !string.Equals(plan.PlanId, request.PlanId, StringComparison.Ordinal)
            || !string.Equals(plan.PlanDigest, request.PlanDigest, StringComparison.Ordinal)
            || !string.Equals(computedPlanDigest, plan.PlanDigest, StringComparison.Ordinal)
            || !string.Equals(plan.PlanId, "clickup-plan-" + computedPlanDigest![..24], StringComparison.Ordinal)
            || !string.Equals(plan.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(plan.AuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            return ApplyConflict(
                request,
                "clickup.plan.coordinates.conflict",
                "Stored ClickUp plan does not match the requested plan coordinates.");
        }

        if (plan.ExpiresAtUtc <= this.timeProvider.GetUtcNow())
        {
            return ApplyConflict(request, "clickup.plan.expired", "Stored ClickUp plan has expired; create a new preview.");
        }

        if (plan.PlannedChange.Disposition != ProviderChangeDisposition.Ready)
        {
            return new ProviderApplyChangesResponse(
                ProviderApplyStatus.Rejected,
                request.PlanId,
                request.IdempotencyKey,
                request.ExpectedAuthorityEpoch,
                Snapshot: null,
                plan.PlannedChange.Diagnostics);
        }

        var response = await this.ApplyPlanAsync(plan, request, fingerprint, store, cancellationToken).ConfigureAwait(false);
        if (response.Status is ProviderApplyStatus.Applied or ProviderApplyStatus.AlreadyApplied)
        {
            await store.SaveReceiptAsync(
                request.IdempotencyKey,
                new StoredReceipt(fingerprint, response),
                cancellationToken).ConfigureAwait(false);
            store.DeletePendingCreate(request.IdempotencyKey);
        }

        return response;
    }

    public async Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Deep)
        {
            return new ProviderHealthResponse(ProviderHealthState.Healthy, this.timeProvider.GetUtcNow(), ProviderVersion, []);
        }

        try
        {
            await this.api.CheckListAsync(cancellationToken).ConfigureAwait(false);
            return new ProviderHealthResponse(ProviderHealthState.Healthy, this.timeProvider.GetUtcNow(), ProviderVersion, []);
        }
        catch (ResourceProviderProtocolException exception)
        {
            return new ProviderHealthResponse(
                ProviderHealthState.Unavailable,
                this.timeProvider.GetUtcNow(),
                ProviderVersion,
                exception.Diagnostics.Count > 0
                    ? exception.Diagnostics
                    : [Diagnostic(exception.Error.Code, ProviderDiagnosticSeverity.Error, exception.Error.Message)]);
        }
    }

    private async Task<ProviderPlannedChange> PlanChangeAsync(
        ResourceChange change,
        CancellationToken cancellationToken)
    {
        if (change.Kind is ResourceChangeKind.Publish or ResourceChangeKind.TransferAuthority)
        {
            return Planned(
                change,
                ProviderChangeDisposition.Unsupported,
                observedVersion: null,
                "clickup.change.unsupported",
                "ClickUp execution provider does not support publication or authority-transfer changes.");
        }

        if (change.Kind == ResourceChangeKind.Create)
        {
            var desired = TryReadContent(change, out var error);
            if (desired is null)
            {
                return Planned(change, ProviderChangeDisposition.Invalid, null, "clickup.content.invalid", error!);
            }

            if (string.IsNullOrWhiteSpace(this.options.LocalReferenceCustomFieldId)
                || string.IsNullOrWhiteSpace(desired.LocalReference))
            {
                return Planned(
                    change,
                    ProviderChangeDisposition.Unsupported,
                    null,
                    "clickup.create.idempotency-field.required",
                    "Create requires --local-reference-field-id and a non-empty localReference.");
            }

            var customFields = await this.api.GetCustomFieldsAsync(cancellationToken).ConfigureAwait(false);
            var localReferenceField = customFields.SingleOrDefault(field =>
                string.Equals(field.Id, this.options.LocalReferenceCustomFieldId, StringComparison.Ordinal));
            if (localReferenceField is null
                || localReferenceField.Type is not ("short_text" or "text")
                || !IsApplicableToStandardTask(localReferenceField))
            {
                return Planned(
                    change,
                    ProviderChangeDisposition.Invalid,
                    null,
                    "clickup.create.idempotency-field.invalid",
                    "Configured local-reference field must be an applicable short-text or text field for standard tasks.");
            }

            var existing = await this.FindByLocalReferenceAsync(desired.LocalReference, cancellationToken).ConfigureAwait(false);
            return existing is null
                ? Planned(change, ProviderChangeDisposition.Ready, null, "clickup.create.ready", "ClickUp task create is ready.", ProviderDiagnosticSeverity.Info)
                : Planned(
                    change,
                    ProviderChangeDisposition.Conflict,
                    this.MapTask(existing).VersionToken,
                    "clickup.create.local-reference-conflict",
                    "A ClickUp task already uses this local reference.");
        }

        var current = await this.api.GetTaskAsync(change.Address.ResourceId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return Planned(
                change,
                ProviderChangeDisposition.Conflict,
                null,
                "clickup.task.not-found",
                "ClickUp task was not found or is not accessible.");
        }

        this.EnsureTaskInConfiguredList(current);

        var currentDocument = this.MapTask(current);
        if (!string.Equals(change.ExpectedVersionToken, currentDocument.VersionToken, StringComparison.Ordinal))
        {
            return Planned(
                change,
                ProviderChangeDisposition.Conflict,
                currentDocument.VersionToken,
                "clickup.version-token.conflict",
                "ClickUp task changed after the caller snapshot.");
        }

        if (change.Kind == ResourceChangeKind.Delete)
        {
            return Planned(
                change,
                ProviderChangeDisposition.Ready,
                currentDocument.VersionToken,
                "clickup.delete.maps-to-archive",
                "Delete is safely mapped to ClickUp archive; no remote DELETE is issued.",
                ProviderDiagnosticSeverity.Warning);
        }

        var update = TryReadContent(change, out var updateError);
        if (update is null)
        {
            return Planned(change, ProviderChangeDisposition.Invalid, currentDocument.VersionToken, "clickup.content.invalid", updateError!);
        }

        if (!string.IsNullOrWhiteSpace(update.RemoteTaskId)
            && !string.Equals(update.RemoteTaskId, change.Address.ResourceId, StringComparison.Ordinal))
        {
            return Planned(
                change,
                ProviderChangeDisposition.Invalid,
                currentDocument.VersionToken,
                "clickup.remote-id.mismatch",
                "Content remoteTaskId must match the resource address.");
        }

        return Planned(
            change,
            ProviderChangeDisposition.Ready,
            currentDocument.VersionToken,
            "clickup.update.ready",
            "ClickUp execution-field update is ready.",
            ProviderDiagnosticSeverity.Info);
    }

    private async Task<ProviderApplyChangesResponse> ApplyPlanAsync(
        StoredPlan plan,
        ProviderApplyChangesRequest request,
        string requestFingerprint,
        ClickUpStateStore store,
        CancellationToken cancellationToken)
    {
        var change = plan.Change;
        ClickUpTask task;
        var alreadyApplied = false;
        if (change.Kind == ResourceChangeKind.Create)
        {
            var desired = ReadContent(change);
            var pendingCreate = await store.LoadPendingCreateAsync(request.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (pendingCreate is not null
                && (!string.Equals(pendingCreate.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
                    || !string.Equals(pendingCreate.LocalReference, desired.LocalReference, StringComparison.Ordinal)))
            {
                return ApplyConflict(
                    request,
                    "clickup.create.pending-conflict",
                    "Pending ClickUp create does not match this apply request.");
            }

            ClickUpTask? existing;
            if (!string.IsNullOrWhiteSpace(pendingCreate?.RemoteTaskId))
            {
                existing = await this.api.GetTaskAsync(pendingCreate.RemoteTaskId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                existing = await this.FindByLocalReferenceAsync(desired.LocalReference!, cancellationToken).ConfigureAwait(false);
            }

            if (existing is not null)
            {
                this.EnsureTaskInConfiguredList(existing);
                if (!OwnedFieldsMatch(existing, desired))
                {
                    return ApplyConflict(
                        request,
                        "clickup.create.local-reference-conflict",
                        "Existing ClickUp task with the local reference does not match the planned execution fields.");
                }

                task = existing;
                alreadyApplied = true;
            }
            else if (pendingCreate is not null)
            {
                throw Failure(
                    "clickup.create.outcome-unknown",
                    "A prior create may have succeeded but is not yet visible; retry the same apply later.",
                    retryable: true);
            }
            else
            {
                pendingCreate = new StoredPendingCreate(requestFingerprint, desired.LocalReference!, RemoteTaskId: null);
                await store.SavePendingCreateAsync(request.IdempotencyKey, pendingCreate, cancellationToken)
                    .ConfigureAwait(false);
                task = await this.api.CreateTaskAsync(this.BuildCreateBody(desired), cancellationToken).ConfigureAwait(false);
                pendingCreate = pendingCreate with { RemoteTaskId = task.Id };
                await store.SavePendingCreateAsync(request.IdempotencyKey, pendingCreate, cancellationToken)
                    .ConfigureAwait(false);
                task = await this.api.GetTaskAsync(task.Id, cancellationToken).ConfigureAwait(false)
                    ?? throw Failure(
                        "clickup.write.outcome-unknown",
                        "Created ClickUp task could not be re-read for idempotency verification.",
                        retryable: true);
                this.EnsureTaskInConfiguredList(task);
                if (!TaskHasLocalReference(task, desired.LocalReference!))
                {
                    throw Failure(
                        "clickup.write.outcome-unknown",
                        "Created ClickUp task did not retain the configured local-reference marker.",
                        retryable: true);
                }
            }
        }
        else
        {
            var current = await this.api.GetTaskAsync(change.Address.ResourceId, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return ApplyConflict(request, "clickup.task.not-found", "ClickUp task no longer exists or is inaccessible.");
            }

            this.EnsureTaskInConfiguredList(current);

            var currentDocument = this.MapTask(current);
            if (!string.Equals(currentDocument.VersionToken, change.ExpectedVersionToken, StringComparison.Ordinal))
            {
                var desiredAlreadyApplied = change.Kind == ResourceChangeKind.Delete
                    ? current.Archived
                    : OwnedFieldsMatch(current, ReadContent(change));
                if (!desiredAlreadyApplied)
                {
                    return ApplyConflict(
                        request,
                        "clickup.version-token.conflict",
                        "ClickUp task changed after preview; apply was not attempted.");
                }

                task = current;
                alreadyApplied = true;
            }
            else if (change.Kind == ResourceChangeKind.Delete)
            {
                task = await this.api.UpdateTaskAsync(
                    change.Address.ResourceId,
                    JsonSerializer.SerializeToElement(new { archived = true }, jsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                task = await this.api.UpdateTaskAsync(
                    change.Address.ResourceId,
                    BuildUpdateBody(current, ReadContent(change)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var document = this.MapTask(task, request.Approval.ApprovalReference);
        var snapshot = this.CreateSnapshot(document, request.Approval.ApprovalReference);
        return new ProviderApplyChangesResponse(
            alreadyApplied ? ProviderApplyStatus.AlreadyApplied : ProviderApplyStatus.Applied,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            snapshot,
            []);
    }

    private ResourceDocument MapTask(ClickUpTask task, string? approvalReference = null)
    {
        var localReference = task.CustomFields?
            .FirstOrDefault(field => string.Equals(field.Id, this.options.LocalReferenceCustomFieldId, StringComparison.Ordinal))?
            .Value is { } value
            ? ElementString(value)
            : null;
        var content = CanonicalizeContent(new ClickUpWorkItemExecutionContent(
            task.Name,
            task.Status.Status,
            MapPriority(task.Priority),
            (task.Assignees ?? []).Select(user => ElementString(user.Id)).ToList(),
            ParseClickUpTime(task.StartDate),
            ParseClickUpTime(task.DueDate),
            task.Archived,
            task.Id,
            task.Url,
            localReference,
            task.DateUpdated));
        var contentElement = JsonSerializer.SerializeToElement(content, jsonOptions);
        var payloadDigest = DigestElement(contentElement);
        var provenance = new ResourceProvenance(
            ProviderId,
            this.timeProvider.GetUtcNow(),
            Actor: null,
            SourceRevision: task.DateUpdated,
            payloadDigest,
            TransferId: null,
            approvalReference);
        return new ResourceDocument(
            new ResourceAddress(ResourceType, this.options.ListId, task.Id),
            contentElement,
            $"clickup:{task.DateUpdated}:{payloadDigest[..16]}",
            payloadDigest,
            CompatibilityPin: null,
            provenance);
    }

    private ProviderValidationResult ValidateSnapshot(ResourceSnapshot snapshot)
    {
        var diagnostics = new List<ProviderDiagnostic>();
        if (!string.Equals(snapshot.ResourceType, ResourceType, StringComparison.Ordinal)
            || !string.Equals(snapshot.ResourceSet, this.options.ListId, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                "clickup.snapshot.scope.invalid",
                ProviderDiagnosticSeverity.Error,
                "Snapshot is outside the configured ClickUp execution-resource scope.",
                snapshot.SnapshotId));
        }

        if (!string.Equals(snapshot.Authority.ResourceType, ResourceType, StringComparison.Ordinal)
            || !string.Equals(snapshot.Authority.ResourceSet, this.options.ListId, StringComparison.Ordinal)
            || !string.Equals(snapshot.Authority.AuthorityProviderId, ProviderId, StringComparison.Ordinal)
            || !string.Equals(snapshot.Authority.AuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                "clickup.snapshot.authority.invalid",
                ProviderDiagnosticSeverity.Error,
                "Snapshot authority coordinates do not match the configured ClickUp authority.",
                snapshot.SnapshotId));
        }

        var snapshotPayload = JsonSerializer.SerializeToElement(snapshot.Resources, jsonOptions);
        var expectedSnapshotDigest = DigestElement(snapshotPayload);
        if (!string.Equals(snapshot.PayloadSha256, expectedSnapshotDigest, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.SnapshotId, "clickup-snapshot-" + expectedSnapshotDigest[..24], StringComparison.Ordinal)
            || !string.Equals(snapshot.Provenance.PayloadSha256, expectedSnapshotDigest, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshot.Provenance.ProviderId, ProviderId, StringComparison.Ordinal)
            || (snapshot.Resources.Count == 1
                && !string.Equals(
                    snapshot.Provenance.SourceRevision,
                    snapshot.Resources[0].Provenance.SourceRevision,
                    StringComparison.Ordinal)))
        {
            diagnostics.Add(Diagnostic(
                "clickup.snapshot.digest.invalid",
                ProviderDiagnosticSeverity.Error,
                "Snapshot digest or provenance does not match its canonical resources.",
                snapshot.SnapshotId));
        }

        foreach (var resource in snapshot.Resources)
        {
            ClickUpWorkItemExecutionContent? content = null;
            string? contentError = null;
            try
            {
                content = resource.Content.Deserialize<ClickUpWorkItemExecutionContent>(jsonOptions);
            }
            catch (JsonException)
            {
                // Recorded below as invalid content.
            }

            if (content is not null)
            {
                _ = ValidateContent(content, out contentError);
            }

            if (content is null || contentError is not null)
            {
                diagnostics.Add(Diagnostic(
                    "clickup.content.invalid",
                    ProviderDiagnosticSeverity.Error,
                    contentError ?? "Execution content is invalid.",
                    resource.Address.ResourceId));
                continue;
            }

            var canonicalContent = CanonicalizeContent(content);
            var canonicalElement = JsonSerializer.SerializeToElement(canonicalContent, jsonOptions);
            var expectedPayloadDigest = DigestElement(canonicalElement);
            if (!string.Equals(resource.Address.ResourceType, ResourceType, StringComparison.Ordinal)
                || !string.Equals(resource.Address.ResourceSet, this.options.ListId, StringComparison.Ordinal)
                || !string.Equals(resource.Address.ResourceId, canonicalContent.RemoteTaskId, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    "clickup.resource.address.invalid",
                    ProviderDiagnosticSeverity.Error,
                    "Resource address does not match its canonical ClickUp execution content.",
                    resource.Address.ResourceId));
            }

            if (!string.Equals(resource.PayloadSha256, expectedPayloadDigest, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(resource.Provenance.PayloadSha256, expectedPayloadDigest, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Diagnostic(
                    "clickup.payload-digest.invalid",
                    ProviderDiagnosticSeverity.Error,
                    "Resource payload digest does not match its content.",
                    resource.Address.ResourceId));
            }

            var expectedVersionToken = $"clickup:{canonicalContent.SourceRevision}:{expectedPayloadDigest[..16]}";
            if (!string.Equals(resource.VersionToken, expectedVersionToken, StringComparison.Ordinal)
                || !string.Equals(resource.Provenance.ProviderId, ProviderId, StringComparison.Ordinal)
                || !string.Equals(resource.Provenance.SourceRevision, canonicalContent.SourceRevision, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    "clickup.resource.coordinates.invalid",
                    ProviderDiagnosticSeverity.Error,
                    "Resource version token or provenance does not match its canonical content.",
                    resource.Address.ResourceId));
            }
        }

        return new ProviderValidationResult(
            snapshot.SnapshotId,
            snapshot.PayloadSha256,
            diagnostics.Count == 0,
            snapshot.CompatibilityPin,
            diagnostics);
    }

    private async Task<ClickUpTask?> FindByLocalReferenceAsync(
        string localReference,
        CancellationToken cancellationToken)
    {
        ClickUpTask? match = null;
        foreach (var archived in new[] { false, true })
        {
            for (var page = 0; page < 10_000; page++)
            {
                var result = await this.api.GetTasksAsync(page, archived, cancellationToken).ConfigureAwait(false);
                foreach (var candidate in result.Tasks.Where(task => TaskHasLocalReference(task, localReference)))
                {
                    if (match is not null && !string.Equals(match.Id, candidate.Id, StringComparison.Ordinal))
                    {
                        throw Failure(
                            "clickup.create.local-reference-duplicate",
                            "More than one ClickUp task uses the configured local reference.");
                    }

                    match = candidate;
                }

                if (result.LastPage == true || result.Tasks.Count < 100)
                {
                    break;
                }
            }
        }

        return match;
    }

    private bool TaskHasLocalReference(ClickUpTask task, string localReference)
    {
        return task.CustomFields?.Any(field =>
            string.Equals(field.Id, this.options.LocalReferenceCustomFieldId, StringComparison.Ordinal)
            && field.Value is { } value
            && string.Equals(ElementString(value), localReference, StringComparison.Ordinal)) == true;
    }

    private static bool IsApplicableToStandardTask(ClickUpCustomFieldDefinition field)
    {
        return field.AppliedObjects is null or { Count: 0 }
            || field.AppliedObjects.Any(applied =>
                string.Equals(ElementString(applied.ObjectType), "19", StringComparison.Ordinal)
                && string.Equals(ElementString(applied.ObjectId), "0", StringComparison.Ordinal));
    }

    private JsonElement BuildCreateBody(ClickUpWorkItemExecutionContent content)
    {
        var root = BuildOwnedFields(content);
        root["name"] = content.Title;
        root["custom_fields"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = this.options.LocalReferenceCustomFieldId,
                ["value"] = content.LocalReference
            }
        };
        return JsonSerializer.SerializeToElement(root, jsonOptions);
    }

    private static JsonElement BuildUpdateBody(ClickUpTask current, ClickUpWorkItemExecutionContent content)
    {
        var root = BuildOwnedFields(content);
        var currentAssignees = (current.Assignees ?? []).Select(user => ElementString(user.Id)).ToHashSet(StringComparer.Ordinal);
        var desiredAssignees = content.AssigneeIds.ToHashSet(StringComparer.Ordinal);
        root["assignees"] = new JsonObject
        {
            ["add"] = new JsonArray(desiredAssignees.Except(currentAssignees, StringComparer.Ordinal).Select(id => JsonValue.Create(ParseAssigneeId(id))).ToArray()),
            ["rem"] = new JsonArray(currentAssignees.Except(desiredAssignees, StringComparer.Ordinal).Select(id => JsonValue.Create(ParseAssigneeId(id))).ToArray())
        };
        return JsonSerializer.SerializeToElement(root, jsonOptions);
    }

    private static JsonObject BuildOwnedFields(ClickUpWorkItemExecutionContent content)
    {
        if (!ValidateContent(content, out var error))
        {
            throw Failure("clickup.content.invalid", error!);
        }

        var root = new JsonObject
        {
            ["status"] = content.OperationalStatus,
            ["priority"] = PriorityNumber(content.Priority),
            ["start_date"] = ToUnixMilliseconds(content.StartAtUtc),
            ["start_date_time"] = true,
            ["due_date"] = ToUnixMilliseconds(content.DueAtUtc),
            ["due_date_time"] = true,
            ["archived"] = content.Archived
        };
        if (content.AssigneeIds.Count > 0)
        {
            root["assignees"] = new JsonArray(content.AssigneeIds.Select(id => JsonValue.Create(ParseAssigneeId(id))).ToArray());
        }

        return root;
    }

    private ResourceSnapshot CreateSnapshot(ResourceDocument document, string approvalReference)
    {
        var createdAt = this.timeProvider.GetUtcNow();
        var snapshotPayload = JsonSerializer.SerializeToElement(new[] { document }, jsonOptions);
        var snapshotDigest = DigestElement(snapshotPayload);
        var provenance = new ResourceProvenance(
            ProviderId,
            createdAt,
            Actor: null,
            document.Provenance.SourceRevision,
            snapshotDigest,
            TransferId: null,
            approvalReference);
        return new ResourceSnapshot(
            "clickup-snapshot-" + snapshotDigest[..24],
            ResourceType,
            this.options.ListId,
            createdAt,
            snapshotDigest,
            Authoritative: true,
            new ResourceAuthorityDeclaration(
                ResourceType,
                this.options.ListId,
                ProviderId,
                this.options.AuthorityEpoch,
                createdAt,
                DeclarationDigest: null),
            CompatibilityPin: null,
            [document],
            provenance);
    }

    private static ProviderApplyChangesResponse ApplyConflict(
        ProviderApplyChangesRequest request,
        string code,
        string message)
    {
        return new ProviderApplyChangesResponse(
            ProviderApplyStatus.Conflict,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            Snapshot: null,
            [Diagnostic(code, ProviderDiagnosticSeverity.Error, message)]);
    }

    private bool IsValidStoredReceipt(
        ProviderApplyChangesResponse response,
        ProviderApplyChangesRequest request)
    {
        return response.Status is ProviderApplyStatus.Applied or ProviderApplyStatus.AlreadyApplied
            && string.Equals(response.PlanId, request.PlanId, StringComparison.Ordinal)
            && string.Equals(response.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            && string.Equals(response.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            && response.Snapshot is not null
            && this.ValidateSnapshot(response.Snapshot).Valid;
    }

    private void EnsureScope(string resourceType, string resourceSet)
    {
        if (!string.Equals(resourceType, ResourceType, StringComparison.Ordinal)
            || !string.Equals(resourceSet, this.options.ListId, StringComparison.Ordinal))
        {
            throw Failure(
                "clickup.scope.mismatch",
                $"ClickUp provider is bound to resource type '{ResourceType}' and its configured List resource set.");
        }
    }

    private void EnsureTaskInConfiguredList(ClickUpTask task)
    {
        if (task.List is null
            || !string.Equals(task.List.Id, this.options.ListId, StringComparison.Ordinal))
        {
            throw Failure(
                "clickup.task.list-mismatch",
                "ClickUp task is outside the configured List resource set.");
        }
    }

    private void EnsureModeAllowsPlan()
    {
        if (this.options.Mode == ClickUpProviderMode.ReadOnly)
        {
            throw Failure("clickup.plan.disabled", "ClickUp provider is configured in read-only mode.");
        }
    }

    private ClickUpStateStore RequireStateStore()
    {
        return this.stateStore
            ?? throw Failure("clickup.state.required", "Durable state is required for plan and apply operations.");
    }

    private static ClickUpWorkItemExecutionContent? TryReadContent(ResourceChange change, out string? error)
    {
        if (change.Content is null)
        {
            error = "Create and update changes require execution content.";
            return null;
        }

        try
        {
            var content = change.Content.Value.Deserialize<ClickUpWorkItemExecutionContent>(jsonOptions);
            if (content is null)
            {
                error = "Execution content is empty.";
                return null;
            }

            if (!ValidateContent(content, out error))
            {
                return null;
            }

            content = CanonicalizeContent(content);
            var canonicalElement = JsonSerializer.SerializeToElement(content, jsonOptions);
            var digest = DigestElement(canonicalElement);
            if (!string.IsNullOrWhiteSpace(change.PayloadSha256)
                && !string.Equals(change.PayloadSha256, digest, StringComparison.OrdinalIgnoreCase))
            {
                error = "Change payload digest does not match execution content.";
                return null;
            }

            return content;
        }
        catch (JsonException)
        {
            error = "Execution content is not valid ClickUp work_item_execution JSON.";
            return null;
        }
    }

    private static ClickUpWorkItemExecutionContent ReadContent(ResourceChange change)
    {
        return TryReadContent(change, out var error)
            ?? throw Failure("clickup.content.invalid", error!);
    }

    private static ResourceChange CanonicalizePlannedChange(
        ResourceChange change,
        ProviderPlannedChange planned)
    {
        if (planned.Disposition != ProviderChangeDisposition.Ready
            || change.Kind is not (ResourceChangeKind.Create or ResourceChangeKind.Update))
        {
            return change;
        }

        var canonicalContent = CanonicalizeContent(ReadContent(change));
        var contentElement = JsonSerializer.SerializeToElement(canonicalContent, jsonOptions);
        return change with
        {
            Content = contentElement,
            PayloadSha256 = DigestElement(contentElement)
        };
    }

    private string ComputePlanDigest(
        string authorityEpoch,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        ResourceChange change,
        ProviderPlannedChange plannedChange)
    {
        return DigestObject(new
        {
            ResourceType,
            ResourceSet = this.options.ListId,
            AuthorityEpoch = authorityEpoch,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            Change = change,
            PlannedChange = plannedChange
        });
    }

    private static bool ValidateContent(ClickUpWorkItemExecutionContent content, out string? error)
    {
        if (string.IsNullOrWhiteSpace(content.Title))
        {
            error = "Execution content title is required for observation and create bootstrap.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content.OperationalStatus))
        {
            error = "Execution content operationalStatus is required.";
            return false;
        }

        if (content.AssigneeIds is null)
        {
            error = "Execution content assigneeIds is required.";
            return false;
        }

        if (content.RemoteTaskId is null || content.Url is null || content.SourceRevision is null)
        {
            error = "Execution content remoteTaskId, url, and sourceRevision fields are required.";
            return false;
        }

        if (content.AssigneeIds.Any(id => !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            error = "Execution content assigneeIds must contain ClickUp numeric user IDs.";
            return false;
        }

        error = null;
        return true;
    }

    private static ClickUpWorkItemExecutionContent CanonicalizeContent(
        ClickUpWorkItemExecutionContent content)
    {
        var assigneeIds = content.AssigneeIds
            .Select(id => long.Parse(id, NumberStyles.None, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return content with
        {
            AssigneeIds = assigneeIds,
            StartAtUtc = content.StartAtUtc?.ToUniversalTime(),
            DueAtUtc = content.DueAtUtc?.ToUniversalTime()
        };
    }

    private static bool OwnedFieldsMatch(ClickUpTask task, ClickUpWorkItemExecutionContent desired)
    {
        var currentAssignees = (task.Assignees ?? []).Select(user => ElementString(user.Id)).Order(StringComparer.Ordinal);
        return string.Equals(task.Status.Status, desired.OperationalStatus, StringComparison.Ordinal)
            && MapPriority(task.Priority) == desired.Priority
            && currentAssignees.SequenceEqual(desired.AssigneeIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            && ParseClickUpTime(task.StartDate) == desired.StartAtUtc
            && ParseClickUpTime(task.DueDate) == desired.DueAtUtc
            && task.Archived == desired.Archived;
    }

    private static ProviderPlannedChange Planned(
        ResourceChange change,
        ProviderChangeDisposition disposition,
        string? observedVersion,
        string code,
        string message,
        ProviderDiagnosticSeverity severity = ProviderDiagnosticSeverity.Error)
    {
        return new ProviderPlannedChange(
            change.ChangeId,
            disposition,
            observedVersion,
            [Diagnostic(code, severity, message, change.ChangeId)]);
    }

    private static ProviderDiagnostic Diagnostic(
        string code,
        ProviderDiagnosticSeverity severity,
        string message,
        string? target = null)
    {
        return new ProviderDiagnostic(code, severity, message, target);
    }

    private static ResourceProviderProtocolException Failure(
        string code,
        string message,
        bool retryable = false)
    {
        return new ResourceProviderProtocolException(
            new ProviderError(code, message, retryable),
            [new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, Retryable: retryable)]);
    }

    private static bool GetArchivedFilter(JsonElement? filter)
    {
        if (filter is null || filter.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (filter.Value.ValueKind != JsonValueKind.Object)
        {
            throw Failure("clickup.query.filter.invalid", "ClickUp query filter must be an object.");
        }

        foreach (var property in filter.Value.EnumerateObject())
        {
            if (!string.Equals(property.Name, "archived", StringComparison.Ordinal))
            {
                throw Failure("clickup.query.filter.unsupported", "Only the 'archived' query filter is supported.");
            }
        }

        if (!filter.Value.TryGetProperty("archived", out var archived))
        {
            return false;
        }

        if (archived.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw Failure("clickup.query.filter.invalid", "The 'archived' query filter must be boolean.");
        }

        return archived.GetBoolean();
    }

    private static string EncodeContinuation(int page, int offset, bool archived)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new QueryContinuation(page, offset, archived), jsonOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static QueryContinuation? DecodeContinuation(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var normalized = token.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            var continuation = JsonSerializer.Deserialize<QueryContinuation>(Convert.FromBase64String(normalized), jsonOptions);
            return continuation is { Page: >= 0, Offset: >= 0 }
                ? continuation
                : throw new FormatException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw Failure("clickup.query.continuation.invalid", "Continuation token is invalid.");
        }
    }

    private static ClickUpPriority? MapPriority(ClickUpPriorityValue? priority)
    {
        var value = priority?.Id ?? priority?.Priority?.ToLowerInvariant();
        return value switch
        {
            "1" or "urgent" => ClickUpPriority.Urgent,
            "2" or "high" => ClickUpPriority.High,
            "3" or "normal" => ClickUpPriority.Normal,
            "4" or "low" => ClickUpPriority.Low,
            _ => null
        };
    }

    private static int? PriorityNumber(ClickUpPriority? priority)
    {
        return priority switch
        {
            ClickUpPriority.Urgent => 1,
            ClickUpPriority.High => 2,
            ClickUpPriority.Normal => 3,
            ClickUpPriority.Low => 4,
            null => null,
            _ => throw new InvalidOperationException("Unknown ClickUp priority.")
        };
    }

    private static long ParseAssigneeId(string id)
    {
        return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Failure("clickup.assignee.invalid", "Assignee ID is not a ClickUp numeric user ID.");
    }

    private static string ElementString(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();
    }

    private static DateTimeOffset? ParseClickUpTime(string? milliseconds)
    {
        return long.TryParse(milliseconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : null;
    }

    private static long? ToUnixMilliseconds(DateTimeOffset? value)
    {
        return value?.ToUniversalTime().ToUnixTimeMilliseconds();
    }

    private static string DigestObject<T>(T value)
    {
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions));
    }

    private static string DigestElement(JsonElement value)
    {
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions));
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
