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
        return Task.FromResult(new ProviderCapabilities(
            this.options.ProviderId,
            "SpecAttest Manager",
            this.providerVersion,
            [ResourceProviderProtocol.Version1],
            [
                ResourceProviderOperations.Query,
                ResourceProviderOperations.Get,
                ResourceProviderOperations.Validate,
                ResourceProviderOperations.Health
            ],
            [SpecAttestInterchangeContract.ResourceType],
            [ResourceProviderRole.ReadOnlyEnricher],
            new ProviderBatchCapabilities(
                this.options.MaximumBatchSize,
                AtomicApply: false,
                OrderedResults: true),
            SupportsIdempotency: false,
            SupportsOptimisticConcurrency: false,
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

    public Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        throw SafeMutationUnavailable();
    }

    public Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        throw SafeMutationUnavailable();
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

    private static ResourceProviderProtocolException SafeMutationUnavailable()
    {
        const string Message = "Current SpecAttest Manager APIs do not expose a pin-aware, staged, atomic, idempotent interchange apply contract; mutation is disabled fail-closed.";
        return new ResourceProviderProtocolException(
            new ProviderError("specattest.manager.safe-mutation.unavailable", Message),
            [Error("specattest.manager.safe-mutation.unavailable", Message)]);
    }
}
