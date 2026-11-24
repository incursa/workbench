using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

#pragma warning disable MA0048 // The transport interface and typed client are one client boundary.

namespace Workbench.Core;

/// <summary>Transport for one request/response exchange with an out-of-process provider.</summary>
public interface IResourceProviderTransport : IAsyncDisposable
{
    Task<ProviderResponseEnvelope> SendAsync(
        ProviderRequestEnvelope request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed provider client that performs handshake/capability checks and enforces the configured authority role before dispatch.
/// </summary>
public sealed class ResourceProviderClient : IAsyncDisposable
{
    private readonly IResourceProviderTransport transport;
    private readonly ResourceProviderRole role;
    private readonly string resourceType;
    private readonly string resourceSet;
    private readonly TimeSpan requestTimeout;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<string, string> publicationPlans = new(StringComparer.Ordinal);
    private bool disposed;

    private ResourceProviderClient(
        IResourceProviderTransport transport,
        ResourceProviderRole role,
        string resourceType,
        string resourceSet,
        TimeSpan requestTimeout,
        TimeProvider timeProvider)
    {
        this.transport = transport;
        this.role = role;
        this.resourceType = resourceType;
        this.resourceSet = resourceSet;
        this.requestTimeout = requestTimeout;
        this.timeProvider = timeProvider;
    }

    /// <summary>Capabilities returned by the completed handshake.</summary>
    public ProviderCapabilities Capabilities { get; private set; } = null!;

    /// <summary>Configured role enforced by this client session.</summary>
    public ResourceProviderRole Role => this.role;

    /// <summary>Resolves an authority binding, connects to a transport, and completes the mandatory protocol handshake.</summary>
    public static async Task<ResourceProviderClient> ConnectAsync(
        IResourceProviderTransport transport,
        ResourceProvidersConfig configuration,
        string resourceType,
        string resourceSet,
        string providerId,
        string clientVersion,
        TimeSpan requestTimeout,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(configuration);
        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), "Request timeout must be greater than zero.");
        }

        var binding = ResourceAuthorityConfigurationValidator.Resolve(configuration, resourceType, resourceSet);
        var configuredRole = ResourceAuthorityConfigurationValidator.ResolveRole(binding, providerId);
        var client = new ResourceProviderClient(
            transport,
            configuredRole,
            binding.ResourceType,
            binding.ResourceSet,
            requestTimeout,
            timeProvider ?? TimeProvider.System);
        try
        {
            var response = await client.InvokeAsync(
                ResourceProviderOperations.Handshake,
                new ProviderHandshakeRequest(
                    "Incursa.Workbench",
                    clientVersion,
                    [ResourceProviderProtocol.Version1]),
                ProviderProtocolJsonContext.Default.ProviderHandshakeRequest,
                ProviderProtocolJsonContext.Default.ProviderHandshakeResponse,
                idempotencyKey: null,
                cancellationToken).ConfigureAwait(false);

            if (!string.Equals(response.SelectedProtocolVersion, ResourceProviderProtocol.Version1, StringComparison.Ordinal))
            {
                throw ProtocolFailure(
                    "provider.protocol.version.unsupported",
                    $"Provider selected unsupported protocol '{response.SelectedProtocolVersion}'.");
            }

            if (!response.Capabilities.ProtocolVersions.Contains(ResourceProviderProtocol.Version1, StringComparer.Ordinal))
            {
                throw ProtocolFailure(
                    "provider.protocol.capability.invalid",
                    "Provider capabilities do not include the selected protocol version.");
            }

            if (!response.Capabilities.Roles.Contains(configuredRole))
            {
                throw ProtocolFailure(
                    "provider.protocol.role.unsupported",
                    $"Provider '{response.Capabilities.ProviderId}' does not advertise the configured role '{configuredRole}'.");
            }

            ValidateMutationCapabilities(response.Capabilities, configuredRole);

            if (!string.Equals(response.Capabilities.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            {
                throw ProtocolFailure(
                    "provider.protocol.identity.mismatch",
                    $"Configured provider '{providerId}' identified itself as '{response.Capabilities.ProviderId}'.");
            }

            if (!response.Capabilities.ResourceTypes.Contains(resourceType, StringComparer.OrdinalIgnoreCase))
            {
                throw ProtocolFailure(
                    "provider.protocol.resource-type.unsupported",
                    $"Provider '{providerId}' does not advertise configured resource type '{resourceType}'.");
            }

            client.Capabilities = response.Capabilities;
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Resolves authority configuration, starts the selected provider process, and completes the handshake.</summary>
    public static async Task<ResourceProviderClient> ConnectConfiguredProcessAsync(
        ResourceProvidersConfig configuration,
        string repoRoot,
        string resourceType,
        string resourceSet,
        string providerId,
        string clientVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _ = ResourceAuthorityConfigurationValidator.Resolve(configuration, resourceType, resourceSet);
        var process = configuration.Providers.Single(provider =>
            string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
        var timeoutSeconds = process.TimeoutSeconds ?? configuration.DefaultTimeoutSeconds;
        var processTransport = new ProviderProcessTransport(process, repoRoot, TimeSpan.FromSeconds(timeoutSeconds));
        var client = await ConnectAsync(
            processTransport,
            configuration,
            resourceType,
            resourceSet,
            providerId,
            clientVersion,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return client;
    }

    public Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.Query);
        this.EnsureBoundScope(request.ResourceType, request.ResourceSet);
        this.EnsureBatchSize(request.PageSize);
        return this.InvokeAsync(
            ResourceProviderOperations.Query,
            request,
            ProviderProtocolJsonContext.Default.ProviderQueryRequest,
            ProviderProtocolJsonContext.Default.ProviderQueryResponse,
            idempotencyKey: null,
            cancellationToken);
    }

    public Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.Get);
        this.EnsureBatchSize(request.Resources.Count);
        foreach (var resource in request.Resources)
        {
            this.EnsureBoundScope(resource.ResourceType, resource.ResourceSet);
        }

        return this.InvokeAsync(
            ResourceProviderOperations.Get,
            request,
            ProviderProtocolJsonContext.Default.ProviderGetRequest,
            ProviderProtocolJsonContext.Default.ProviderGetResponse,
            idempotencyKey: null,
            cancellationToken);
    }

    public Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.Validate);
        this.EnsureBatchSize(request.Snapshots.Count);
        foreach (var snapshot in request.Snapshots)
        {
            this.EnsureBoundScope(snapshot.ResourceType, snapshot.ResourceSet);
        }

        return this.InvokeAsync(
            ResourceProviderOperations.Validate,
            request,
            ProviderProtocolJsonContext.Default.ProviderValidateRequest,
            ProviderProtocolJsonContext.Default.ProviderValidateResponse,
            idempotencyKey: null,
            cancellationToken);
    }

    public async Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.PlanChanges);
        this.EnsureBoundScope(request.ResourceType, request.ResourceSet);
        this.EnsureMutationRole(request.Changes);
        this.EnsureBatchSize(request.Changes.Count);
        ProviderDiagnostic? violation = request.Changes
            .Select(change => ResourceProviderContractValidator.ValidateChange(
                change,
                request.ResourceType,
                request.ResourceSet,
                request.ExpectedAuthorityEpoch))
            .FirstOrDefault(diagnostic => diagnostic is not null);
        if (violation is not null)
        {
            throw ProtocolFailure(violation.Code, violation.Message);
        }

        if (request.Changes.Count > 1 && !this.Capabilities.Batch.AtomicApply)
        {
            throw new InvalidOperationException(
                "Provider cannot plan a multi-item mutation because it does not advertise atomic apply.");
        }

        var response = await this.InvokeAsync(
            ResourceProviderOperations.PlanChanges,
            request,
            ProviderProtocolJsonContext.Default.ProviderPlanChangesRequest,
            ProviderProtocolJsonContext.Default.ProviderPlanChangesResponse,
            idempotencyKey: null,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.PlanId)
            || !ResourceProviderContractValidator.IsSha256(response.PlanDigest)
            || !string.Equals(response.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            || !response.RequiresExplicitApproval)
        {
            throw ProtocolFailure(
                "provider.plan.coordinates.invalid",
                "Provider returned an invalid plan ID, digest, authority epoch, or approval invariant.");
        }

        if (this.role == ResourceProviderRole.PublicationTarget)
        {
            this.publicationPlans[response.PlanId] = response.PlanDigest;
        }

        return response;
    }

    public async Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.ApplyChanges);
        this.EnsureBoundScope(request.ResourceType, request.ResourceSet);
        if (this.role == ResourceProviderRole.ReadOnlyEnricher)
        {
            throw new InvalidOperationException("A read-only enricher cannot apply changes.");
        }

        if (this.role == ResourceProviderRole.PublicationTarget
            && (!this.publicationPlans.TryGetValue(request.PlanId, out string? plannedDigest)
                || !string.Equals(plannedDigest, request.PlanDigest, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "A publication target may apply only a publication-only plan created and verified by this client session.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new InvalidOperationException("Apply changes requires a non-empty idempotency key.");
        }

        var response = await this.InvokeAsync(
            ResourceProviderOperations.ApplyChanges,
            request,
            ProviderProtocolJsonContext.Default.ProviderApplyChangesRequest,
            ProviderProtocolJsonContext.Default.ProviderApplyChangesResponse,
            request.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(response.PlanId, request.PlanId, StringComparison.Ordinal)
            || !string.Equals(response.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            || !string.Equals(response.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal))
        {
            throw ProtocolFailure(
                "provider.apply.coordinates.invalid",
                "Provider apply result did not match the requested plan, idempotency key, or authority epoch.");
        }

        return response;
    }

    public Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default)
    {
        this.EnsureCapability(ResourceProviderOperations.Health);
        return this.InvokeAsync(
            ResourceProviderOperations.Health,
            request,
            ProviderProtocolJsonContext.Default.ProviderHealthRequest,
            ProviderProtocolJsonContext.Default.ProviderHealthResponse,
            idempotencyKey: null,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        await this.transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<TResponse> InvokeAsync<TRequest, TResponse>(
        string operation,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        var requestId = Guid.NewGuid().ToString("N");
        var deadline = this.timeProvider.GetUtcNow().Add(this.requestTimeout);
        var envelope = new ProviderRequestEnvelope(
            ResourceProviderProtocol.Version1,
            requestId,
            operation,
            deadline,
            idempotencyKey,
            JsonSerializer.SerializeToElement(body, requestType));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(this.requestTimeout);
        ProviderResponseEnvelope response;
        try
        {
            response = await this.transport.SendAsync(envelope, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ProtocolFailure(
                "provider.protocol.timeout",
                $"Provider operation '{operation}' exceeded its {this.requestTimeout.TotalSeconds:g} second timeout.",
                retryable: true);
        }

        if (!string.Equals(response.ProtocolVersion, ResourceProviderProtocol.Version1, StringComparison.Ordinal)
            || !string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
        {
            throw ProtocolFailure(
                "provider.protocol.correlation.invalid",
                "Provider response protocol version or request identifier did not match the request.");
        }

        if (!response.Ok)
        {
            throw new ResourceProviderProtocolException(
                response.Error ?? new ProviderError("provider.protocol.failure", "Provider request failed without a structured error."),
                response.Diagnostics);
        }

        if (response.Body is null)
        {
            throw ProtocolFailure(
                "provider.protocol.body.missing",
                $"Provider operation '{operation}' returned no response body.");
        }

        var result = response.Body.Value.Deserialize(responseType);
        return result ?? throw ProtocolFailure(
            "provider.protocol.body.invalid",
            $"Provider operation '{operation}' returned an invalid response body.");
    }

    private void EnsureCapability(string operation)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.Capabilities.Operations.Contains(operation, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Provider '{this.Capabilities.ProviderId}' does not advertise operation '{operation}'.");
        }
    }

    private void EnsureBatchSize(int count)
    {
        if (count <= 0)
        {
            throw new InvalidOperationException("Provider batch must contain at least one item.");
        }

        if (count > this.Capabilities.Batch.MaxItems)
        {
            throw new InvalidOperationException(
                $"Provider batch contains {count} items but the advertised maximum is {this.Capabilities.Batch.MaxItems}.");
        }
    }

    private void EnsureBoundScope(string candidateResourceType, string candidateResourceSet)
    {
        if (!string.Equals(this.resourceType, candidateResourceType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(this.resourceSet, candidateResourceSet, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider session is bound to resource type '{this.resourceType}' and set '{this.resourceSet}'.");
        }
    }

    private void EnsureMutationRole(IReadOnlyList<ResourceChange> changes)
    {
        if (this.role == ResourceProviderRole.ReadOnlyEnricher)
        {
            throw new InvalidOperationException("A read-only enricher cannot plan changes.");
        }

        if (this.role == ResourceProviderRole.PublicationTarget
            && changes.Any(change => change.Kind != ResourceChangeKind.Publish))
        {
            throw new InvalidOperationException(
                "A publication target may plan only publication changes; it cannot write normative content.");
        }
    }

    private static ResourceProviderProtocolException ProtocolFailure(
        string code,
        string message,
        bool retryable = false)
    {
        return new ResourceProviderProtocolException(new ProviderError(code, message, retryable));
    }

    private static void ValidateMutationCapabilities(
        ProviderCapabilities capabilities,
        ResourceProviderRole role)
    {
        if (role == ResourceProviderRole.ReadOnlyEnricher)
        {
            return;
        }

        if (capabilities.Operations.Contains(ResourceProviderOperations.PlanChanges, StringComparer.Ordinal)
            && (!capabilities.SupportsOptimisticConcurrency || !capabilities.Batch.AtomicApply))
        {
            throw ProtocolFailure(
                "provider.protocol.concurrency.required",
                "A mutating provider must advertise optimistic concurrency and atomic apply support.");
        }

        if (capabilities.Operations.Contains(ResourceProviderOperations.ApplyChanges, StringComparer.Ordinal)
            && (!capabilities.SupportsOptimisticConcurrency || !capabilities.SupportsIdempotency))
        {
            throw ProtocolFailure(
                "provider.protocol.apply-safety.required",
                "A provider that applies changes must advertise idempotency and optimistic concurrency support.");
        }
    }
}

#pragma warning restore MA0048
