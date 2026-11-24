using System.Collections.Concurrent;
using System.Reflection;
using Workbench.Core;

namespace Workbench.SpecAttestProvider;

public sealed class SpecAttestResourceProvider : IResourceProvider
{
    public const string DefaultProviderId = "specattest";

    private readonly ISpecAttestManagerClient manager;
    private readonly SpecAttestProviderOptions options;
    private readonly TimeProvider timeProvider;
    private readonly string providerVersion;
    private readonly ConcurrentDictionary<string, SpecAttestStoredPlan> plans = new(StringComparer.Ordinal);

    public SpecAttestResourceProvider(
        ISpecAttestManagerClient manager,
        SpecAttestProviderOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.manager = manager;
        this.options = options;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.providerVersion = typeof(SpecAttestResourceProvider).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(SpecAttestResourceProvider).Assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        bool managerAuthority = string.Equals(this.options.AuthorityMode, SpecAttestAuthorityModes.Manager, StringComparison.Ordinal);
        return Task.FromResult(new ProviderCapabilities(
            this.options.ProviderId,
            "SpecAttest Manager",
            this.providerVersion,
            [ResourceProviderProtocol.Version1],
            [
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                .. managerAuthority ? [ResourceProviderOperations.PlanChanges, ResourceProviderOperations.ApplyChanges] : Array.Empty<string>(),
                ResourceProviderOperations.Health
            ],
            [SpecAttestInterchangeContract.ResourceType],
            [managerAuthority ? ResourceProviderRole.Authority : ResourceProviderRole.ReadOnlyEnricher],
            new ProviderBatchCapabilities(
                MaxItems: 1,
                AtomicApply: managerAuthority,
                OrderedResults: true),
            SupportsIdempotency: managerAuthority,
            SupportsOptimisticConcurrency: managerAuthority,
            SupportsOfflineSnapshots: true));
    }

    public async Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureScope(request.ResourceType, request.ResourceSet);
        SpecAttestManagerExport export = await this.manager
            .GetCanonicalExportAsync(this.options.ManagerProjectId, cancellationToken)
            .ConfigureAwait(false);
        ResourceDocument document = this.Translate(export);
        ResourceSummary summary = new(
            document.Address,
            document.VersionToken,
            document.PayloadSha256,
            export.UpdatedAt);
        return new ProviderQueryResponse(
            [summary],
            ContinuationToken: null,
            [UnattestedDiagnostic()]);
    }

    public async Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        foreach (ResourceAddress address in request.Resources)
        {
            this.EnsureAddress(address);
        }

        SpecAttestManagerExport export = await this.manager
            .GetCanonicalExportAsync(this.options.ManagerProjectId, cancellationToken)
            .ConfigureAwait(false);
        ResourceDocument document = this.Translate(export);
        return new ProviderGetResponse([document], [UnattestedDiagnostic()]);
    }

    public Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        List<ProviderValidationResult> results = [];
        foreach (ResourceSnapshot snapshot in request.Snapshots)
        {
            List<ProviderDiagnostic> diagnostics = [];
            if (!string.Equals(snapshot.ResourceType, SpecAttestInterchangeContract.ResourceType, StringComparison.Ordinal)
                || !string.Equals(snapshot.ResourceSet, this.options.SetId, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "specattest.snapshot.scope.mismatch",
                    "Snapshot does not belong to the configured SpecAttest set.",
                    snapshot.SnapshotId));
            }

            if (!string.Equals(snapshot.Authority.ResourceType, snapshot.ResourceType, StringComparison.Ordinal)
                || !string.Equals(snapshot.Authority.ResourceSet, snapshot.ResourceSet, StringComparison.Ordinal)
                || !string.Equals(
                    snapshot.Authority.AuthorityProviderId,
                    this.options.AuthorityProviderId,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    snapshot.Authority.AuthorityEpoch,
                    this.options.AuthorityEpoch,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "specattest.snapshot.authority.conflict",
                    "Snapshot authority declaration conflicts with the configured authority mode.",
                    snapshot.SnapshotId));
            }

            foreach (ResourceDocument resource in snapshot.Resources)
            {
                this.ValidateResource(resource, diagnostics);
            }

            results.Add(new ProviderValidationResult(
                snapshot.SnapshotId,
                snapshot.PayloadSha256,
                diagnostics.All(static diagnostic => diagnostic.Severity != ProviderDiagnosticSeverity.Error),
                snapshot.CompatibilityPin,
                diagnostics));
        }

        return Task.FromResult(new ProviderValidateResponse(results));
    }

    public async Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureManagerAuthority();
        this.EnsureScope(request.ResourceType, request.ResourceSet);
        if (!string.Equals(request.ExpectedAuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            throw Failure("specattest.authority-epoch.conflict", "Configured Manager authority epoch does not match the request.");
        }

        if (request.Changes.Count != 1)
        {
            throw Failure("specattest.change-count.invalid", "SpecAttest set mutation requires exactly one atomic set change.");
        }

        ResourceChange change = request.Changes[0];
        this.EnsureAddress(change.Address);
        if (change.Kind != ResourceChangeKind.Update || change.Content is null || change.PayloadSha256 is null)
        {
            return InvalidPlan(request, change, "specattest.change.unsupported", "SpecAttest Manager accepts one payload-bearing set update.", this.timeProvider.GetUtcNow());
        }

        string contentDigest = SpecAttestCanonicalJson.Sha256(change.Content.Value);
        if (!string.Equals(contentDigest, change.PayloadSha256, StringComparison.Ordinal))
        {
            return InvalidPlan(request, change, "specattest.change.digest-mismatch", "Change content does not match its declared digest.", this.timeProvider.GetUtcNow());
        }

        SpecAttestInterchangePackage? desired = change.Content.Value.Deserialize(
            SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        if (desired is null)
        {
            return InvalidPlan(request, change, "specattest.interchange.empty", "Change content is not a SpecAttest interchange package.", this.timeProvider.GetUtcNow());
        }

        ProviderDiagnostic[] validationErrors = SpecAttestInterchangeValidator.ValidateForAcceptance(
                desired,
                this.options.StandardPin,
                this.options.SetId,
                SpecAttestAuthorityModes.Manager)
            .Where(static diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error)
            .ToArray();
        if (validationErrors.Length > 0)
        {
            DateTimeOffset invalidAt = this.timeProvider.GetUtcNow();
            ProviderPlannedChange invalid = new(
                change.ChangeId,
                ProviderChangeDisposition.Invalid,
                change.ExpectedVersionToken,
                validationErrors);
            return new ProviderPlanChangesResponse(
                "specattest-invalid",
                SpecAttestCanonicalJson.Sha256(change.ChangeId),
                request.ExpectedAuthorityEpoch,
                invalidAt,
                ExpiresAtUtc: null,
                RequiresExplicitApproval: true,
                [invalid],
                validationErrors);
        }

        SpecAttestManagerExport current = await this.manager
            .GetCanonicalExportAsync(this.options.ManagerProjectId, cancellationToken)
            .ConfigureAwait(false);
        string observedVersion = $"{current.PackageVersion}:{current.RecordVersion}";
        if (!string.Equals(change.ExpectedVersionToken, observedVersion, StringComparison.Ordinal))
        {
            return InvalidPlan(request, change, "specattest.concurrency.version-conflict", "Manager project version changed before planning.", this.timeProvider.GetUtcNow(), ProviderChangeDisposition.Conflict, observedVersion);
        }

        SpecAttestInterchangePackage currentPackage = SpecAttestInterchange.FromManagerExport(current, this.options, this.providerVersion);
        bool nonRequirementsMatch = desired.Entries
            .Where(static entry => !string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
            .Select(static entry => (entry.StableId, entry.Sha256))
            .SequenceEqual(
                currentPackage.Entries
                    .Where(static entry => !string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
                    .Select(static entry => (entry.StableId, entry.Sha256)));
        if (!nonRequirementsMatch)
        {
            return InvalidPlan(request, change, "specattest.manager.artifact-mutation.unsupported", "Manager safe intake cannot mutate specification artifacts through this contract.", this.timeProvider.GetUtcNow());
        }

        string[] desiredRequirementIds = desired.Entries
            .Where(static entry => string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
            .Select(static entry => entry.StableId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] currentRequirementIds = currentPackage.Entries
            .Where(static entry => string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
            .Select(static entry => entry.StableId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (currentRequirementIds.Except(desiredRequirementIds, StringComparer.Ordinal).Any())
        {
            return InvalidPlan(request, change, "specattest.manager.requirement-delete.unsupported", "Manager safe intake cannot delete requirements; retire or supersede them explicitly.", this.timeProvider.GetUtcNow());
        }

        JsonElement currentIntake = await this.manager
            .GetIntakePackageAsync(this.options.ManagerProjectId, cancellationToken)
            .ConfigureAwait(false);
        JsonElement stageRequest = SpecAttestSafeMutation.BuildStageRequest(
            desired,
            currentIntake,
            this.options.StandardPin,
            change.PayloadSha256);
        DateTimeOffset createdAt = this.timeProvider.GetUtcNow();
        JsonElement planCoordinates = JsonSerializer.SerializeToElement(new
        {
            request.ExpectedAuthorityEpoch,
            change.ChangeId,
            change.ExpectedVersionToken,
            change.PayloadSha256,
            stageRequest
        });
        string planDigest = SpecAttestCanonicalJson.Sha256(planCoordinates);
        string planId = "specattest-plan-" + planDigest[..24];
        SpecAttestStoredPlan stored = new(
            planId,
            planDigest,
            request.ExpectedAuthorityEpoch,
            createdAt,
            change,
            stageRequest);
        this.plans[planId] = stored;
        ProviderPlannedChange ready = new(
            change.ChangeId,
            ProviderChangeDisposition.Ready,
            observedVersion,
            []);
        return new ProviderPlanChangesResponse(
            planId,
            planDigest,
            request.ExpectedAuthorityEpoch,
            createdAt,
            ExpiresAtUtc: null,
            RequiresExplicitApproval: true,
            [ready],
            []);
    }

    public async Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureManagerAuthority();
        this.EnsureScope(request.ResourceType, request.ResourceSet);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || !this.plans.TryGetValue(request.PlanId, out SpecAttestStoredPlan? plan)
            || !string.Equals(plan.PlanDigest, request.PlanDigest, StringComparison.Ordinal)
            || !string.Equals(plan.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(plan.AuthorityEpoch, this.options.AuthorityEpoch, StringComparison.Ordinal))
        {
            return ApplyConflict(request, "specattest.plan.coordinates.conflict", "Stored SpecAttest plan does not match the apply coordinates.");
        }

        JsonElement stageRequest = SpecAttestSafeMutation.WithIdempotencyKey(plan.StageRequest, request.IdempotencyKey);
        long expectedProjectVersion = SpecAttestSafeMutation.ExpectedProjectVersion(stageRequest);
        SpecAttestSafeMutationOperation staged;
        try
        {
            staged = await this.manager.StageSafeMutationAsync(
                this.options.ManagerProjectId,
                stageRequest,
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceProviderProtocolException exception)
        {
            return ApplyConflict(request, exception.Error.Code, exception.Error.Message);
        }

        bool alreadyApplied = string.Equals(staged.Status, "completed", StringComparison.OrdinalIgnoreCase);
        SpecAttestSafeMutationOperation completed;
        try
        {
            completed = await this.manager.ExecuteSafeMutationAsync(
                this.options.ManagerProjectId,
                staged.ImportRunId,
                "commit",
                expectedProjectVersion,
                staged.RecordVersion,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceProviderProtocolException exception)
        {
            return ApplyConflict(request, exception.Error.Code, exception.Error.Message);
        }
        if (!string.Equals(completed.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyConflict(request, "specattest.manager.commit.incomplete", "Manager did not complete the safe mutation.");
        }

        SpecAttestManagerExport export = await this.manager
            .GetCanonicalExportAsync(this.options.ManagerProjectId, cancellationToken)
            .ConfigureAwait(false);
        ResourceDocument document = this.Translate(export);
        ResourceSnapshot snapshot = new(
            $"specattest-{completed.ImportRunId}",
            SpecAttestInterchangeContract.ResourceType,
            this.options.SetId,
            this.timeProvider.GetUtcNow(),
            document.PayloadSha256,
            Authoritative: true,
            new ResourceAuthorityDeclaration(
                SpecAttestInterchangeContract.ResourceType,
                this.options.SetId,
                this.options.ProviderId,
                this.options.AuthorityEpoch,
                this.timeProvider.GetUtcNow(),
                DeclarationDigest: null),
            this.options.StandardPin,
            [document],
            document.Provenance);
        return new ProviderApplyChangesResponse(
            alreadyApplied ? ProviderApplyStatus.AlreadyApplied : ProviderApplyStatus.Applied,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            snapshot,
            []);
    }

    public async Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default)
    {
        SpecAttestManagerStatus status = await this.manager
            .GetStatusAsync(request.Deep, cancellationToken)
            .ConfigureAwait(false);
        return new ProviderHealthResponse(
            status.State,
            this.timeProvider.GetUtcNow(),
            status.Version,
            status.Diagnostics);
    }

    private ResourceDocument Translate(SpecAttestManagerExport export)
    {
        SpecAttestInterchangePackage package = SpecAttestInterchange.FromManagerExport(
            export,
            this.options,
            this.providerVersion);
        JsonElement content = JsonSerializer.SerializeToElement(
            package,
            SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        string payloadSha256 = SpecAttestInterchange.ComputePackageSha256(package);
        return new ResourceDocument(
            new ResourceAddress(
                SpecAttestInterchangeContract.ResourceType,
                this.options.SetId,
                this.options.SetId),
            content,
            $"{export.PackageVersion}:{export.RecordVersion}",
            payloadSha256,
            this.options.StandardPin,
            new ResourceProvenance(
                this.options.ProviderId,
                export.UpdatedAt,
                Actor: null,
                $"manager:{export.ProjectId}:{export.RecordVersion}",
                payloadSha256,
                package.Manifest.TransferId,
                ApprovalReference: null));
    }

    private void ValidateResource(
        ResourceDocument resource,
        ICollection<ProviderDiagnostic> diagnostics)
    {
        try
        {
            this.EnsureAddress(resource.Address);
        }
        catch (ResourceProviderProtocolException exception)
        {
            foreach (ProviderDiagnostic diagnostic in exception.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }
        }

        string contentSha256 = SpecAttestCanonicalJson.Sha256(resource.Content);
        if (!string.Equals(contentSha256, resource.PayloadSha256, StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "specattest.resource.digest-mismatch",
                "Resource payload digest does not match its canonical content.",
                resource.Address.ResourceId));
        }

        if (resource.CompatibilityPin is null
            || resource.CompatibilityPin != this.options.StandardPin)
        {
            diagnostics.Add(Error(
                "specattest.resource.standard-pin.conflict",
                "Resource compatibility pin does not exactly match the configured Standard pin.",
                resource.Address.ResourceId));
        }

        SpecAttestInterchangePackage? package;
        try
        {
            package = resource.Content.Deserialize(
                SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error(
                "specattest.interchange.invalid-json",
                $"Resource is not a valid SpecAttest interchange package: {exception.Message}",
                resource.Address.ResourceId));
            return;
        }

        if (package is null)
        {
            diagnostics.Add(Error(
                "specattest.interchange.empty",
                "Resource does not contain a SpecAttest interchange package.",
                resource.Address.ResourceId));
            return;
        }

        foreach (ProviderDiagnostic diagnostic in SpecAttestInterchangeValidator.ValidateForAcceptance(
                     package,
                     this.options.StandardPin,
                     this.options.SetId,
                     this.options.AuthorityMode))
        {
            diagnostics.Add(diagnostic);
        }
    }

    private void EnsureAddress(ResourceAddress address)
    {
        this.EnsureScope(address.ResourceType, address.ResourceSet);
        if (!string.Equals(address.ResourceId, this.options.SetId, StringComparison.Ordinal))
        {
            throw new ResourceProviderProtocolException(
                new ProviderError(
                    "specattest.resource.not-found",
                    $"Provider is configured for set '{this.options.SetId}', not '{address.ResourceId}'."),
                [Error(
                    "specattest.resource.not-found",
                    "The requested set is not configured for this provider process.",
                    address.ResourceId)]);
        }
    }

    private void EnsureScope(string resourceType, string resourceSet)
    {
        if (!string.Equals(resourceType, SpecAttestInterchangeContract.ResourceType, StringComparison.Ordinal)
            || !string.Equals(resourceSet, this.options.SetId, StringComparison.Ordinal))
        {
            throw new ResourceProviderProtocolException(
                new ProviderError(
                    "specattest.resource.scope-mismatch",
                    "Request does not match the configured SpecAttest authority domain."),
                [Error(
                    "specattest.resource.scope-mismatch",
                    "Request does not match the configured SpecAttest authority domain.",
                    $"{resourceType}/{resourceSet}")]);
        }
    }

    private static ProviderDiagnostic UnattestedDiagnostic()
    {
        return new ProviderDiagnostic(
            "specattest.validation.not-attested",
            ProviderDiagnosticSeverity.Warning,
            "Current Manager exports do not record an exact Standard pin and parity result; the snapshot is inspectable but not acceptable as an authority transition.");
    }

    private static ProviderDiagnostic Error(string code, string message, string? target = null)
    {
        return new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, target);
    }

    private void EnsureManagerAuthority()
    {
        if (!string.Equals(this.options.AuthorityMode, SpecAttestAuthorityModes.Manager, StringComparison.Ordinal))
        {
            throw Failure("specattest.manager.safe-mutation.unavailable", "Safe mutation requires Manager authority mode.");
        }
    }

    private static ProviderPlanChangesResponse InvalidPlan(
        ProviderPlanChangesRequest request,
        ResourceChange change,
        string code,
        string message,
        DateTimeOffset createdAt,
        ProviderChangeDisposition disposition = ProviderChangeDisposition.Invalid,
        string? observedVersion = null)
    {
        ProviderDiagnostic diagnostic = Error(code, message, change.ChangeId);
        ProviderPlannedChange planned = new(change.ChangeId, disposition, observedVersion ?? change.ExpectedVersionToken, [diagnostic]);
        return new ProviderPlanChangesResponse(
            "specattest-invalid",
            SpecAttestCanonicalJson.Sha256(change.ChangeId),
            request.ExpectedAuthorityEpoch,
            createdAt,
            ExpiresAtUtc: null,
            RequiresExplicitApproval: true,
            [planned],
            [diagnostic]);
    }

    private static ProviderApplyChangesResponse ApplyConflict(ProviderApplyChangesRequest request, string code, string message)
        => new(
            ProviderApplyStatus.Conflict,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            Snapshot: null,
            [Error(code, message, request.PlanId)]);

    private static ResourceProviderProtocolException Failure(string code, string message)
        => new(new ProviderError(code, message), [Error(code, message)]);
}
