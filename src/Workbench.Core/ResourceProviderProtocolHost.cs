using System.Text.Json.Serialization.Metadata;

namespace Workbench.Core;

/// <summary>
/// Hosts an <see cref="IResourceProvider"/> over newline-delimited JSON streams. First-party and external providers use
/// this same host contract in their own executable process.
/// </summary>
public sealed class ResourceProviderProtocolHost
{
    private readonly IResourceProvider provider;
    private readonly TimeProvider timeProvider;
    private ProviderCapabilities? capabilities;
    private bool handshakeComplete;

    public ResourceProviderProtocolHost(IResourceProvider provider, TimeProvider? timeProvider = null)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Runs until stdin closes or host cancellation is requested.</summary>
    public async Task RunAsync(
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            ProviderResponseEnvelope response;
            try
            {
                var request = JsonSerializer.Deserialize(
                    line,
                    ProviderProtocolJsonContext.Default.ProviderRequestEnvelope)
                    ?? throw new JsonException("Request envelope was empty.");
                response = await this.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                response = Failure(
                    string.Empty,
                    "provider.protocol.json.invalid",
                    $"Request was not valid protocol JSON: {exception.Message}");
            }

            var json = JsonSerializer.Serialize(
                response,
                ProviderProtocolJsonContext.Default.ProviderResponseEnvelope);
            await output.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Handles one already parsed envelope. Exposed for deterministic host tests and alternate process shells.</summary>
    public async Task<ProviderResponseEnvelope> HandleAsync(
        ProviderRequestEnvelope request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ProtocolVersion, ResourceProviderProtocol.Version1, StringComparison.Ordinal))
        {
            return Failure(
                request.RequestId,
                "provider.protocol.version.unsupported",
                $"Unsupported protocol version '{request.ProtocolVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return Failure(string.Empty, "provider.protocol.request-id.required", "RequestId is required.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = request.DeadlineUtc - this.timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return Failure(
                request.RequestId,
                "provider.protocol.deadline.exceeded",
                "Request deadline has already elapsed.",
                retryable: true);
        }

        deadline.CancelAfter(remaining);

        try
        {
            if (string.Equals(request.Operation, ResourceProviderOperations.Handshake, StringComparison.Ordinal))
            {
                return await this.HandleHandshakeAsync(request, deadline.Token).ConfigureAwait(false);
            }

            if (!this.handshakeComplete
                && !string.Equals(request.Operation, ResourceProviderOperations.Health, StringComparison.Ordinal))
            {
                return Failure(
                    request.RequestId,
                    "provider.protocol.handshake.required",
                    "Handshake must complete before this operation.");
            }

            this.capabilities ??= await this.provider.GetCapabilitiesAsync(deadline.Token).ConfigureAwait(false);
            if (!this.capabilities.Operations.Contains(request.Operation, StringComparer.Ordinal))
            {
                return Failure(
                    request.RequestId,
                    "provider.operation.unsupported",
                    $"Provider does not advertise operation '{request.Operation}'.");
            }

            return request.Operation switch
            {
                ResourceProviderOperations.Query => await this.HandleQueryAsync(request, deadline.Token).ConfigureAwait(false),
                ResourceProviderOperations.Get => await this.HandleGetAsync(request, deadline.Token).ConfigureAwait(false),
                ResourceProviderOperations.Validate => await this.HandleValidateAsync(request, deadline.Token).ConfigureAwait(false),
                ResourceProviderOperations.PlanChanges => await this.HandlePlanAsync(request, deadline.Token).ConfigureAwait(false),
                ResourceProviderOperations.ApplyChanges => await this.HandleApplyAsync(request, deadline.Token).ConfigureAwait(false),
                ResourceProviderOperations.Health => await this.HandleHealthAsync(request, deadline.Token).ConfigureAwait(false),
                _ => Failure(
                    request.RequestId,
                    "provider.operation.unknown",
                    $"Unknown operation '{request.Operation}'.")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request.RequestId,
                "provider.protocol.deadline.exceeded",
                "Provider operation exceeded its deadline.",
                retryable: true);
        }
        catch (ResourceProviderProtocolException exception)
        {
            return new ProviderResponseEnvelope(
                ResourceProviderProtocol.Version1,
                request.RequestId,
                Ok: false,
                Body: null,
                exception.Diagnostics,
                exception.Error);
        }
        catch (JsonException)
        {
            return Failure(
                request.RequestId,
                "provider.protocol.body.invalid",
                $"Operation '{request.Operation}' has an invalid request body.");
        }
#pragma warning disable EPC12 // The wire contract deliberately exposes no provider stack trace.
        catch (Exception exception)
        {
            return Failure(
                request.RequestId,
                "provider.operation.failed",
                $"Provider operation failed ({exception.GetType().Name}).",
                retryable: false);
        }
#pragma warning restore EPC12
    }

    private async Task<ProviderResponseEnvelope> HandleHandshakeAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderHandshakeRequest);
        if (!request.AcceptedProtocolVersions.Contains(ResourceProviderProtocol.Version1, StringComparer.Ordinal))
        {
            return Failure(
                envelope.RequestId,
                "provider.protocol.version.no-match",
                "Client did not offer a protocol version supported by this host.");
        }

        this.capabilities = await this.provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        if (!this.capabilities.ProtocolVersions.Contains(ResourceProviderProtocol.Version1, StringComparer.Ordinal))
        {
            return Failure(
                envelope.RequestId,
                "provider.protocol.capability.invalid",
                "Provider capabilities do not include the host protocol version.");
        }

        if (this.capabilities.Batch.MaxItems <= 0)
        {
            return Failure(
                envelope.RequestId,
                "provider.protocol.batch.invalid",
                "Provider must advertise a positive maximum batch size.");
        }

        if (this.capabilities.Operations.Contains(ResourceProviderOperations.PlanChanges, StringComparer.Ordinal)
            && (!this.capabilities.SupportsOptimisticConcurrency || !this.capabilities.Batch.AtomicApply))
        {
            return Failure(
                envelope.RequestId,
                "provider.protocol.concurrency.required",
                "A provider that plans changes must advertise optimistic concurrency and atomic apply support.");
        }

        if (this.capabilities.Operations.Contains(ResourceProviderOperations.ApplyChanges, StringComparer.Ordinal)
            && (!this.capabilities.SupportsOptimisticConcurrency || !this.capabilities.SupportsIdempotency))
        {
            return Failure(
                envelope.RequestId,
                "provider.protocol.apply-safety.required",
                "A provider that applies changes must advertise idempotency and optimistic concurrency support.");
        }

        this.handshakeComplete = true;
        return Success(
            envelope.RequestId,
            new ProviderHandshakeResponse(ResourceProviderProtocol.Version1, this.capabilities),
            ProviderProtocolJsonContext.Default.ProviderHandshakeResponse);
    }

    private async Task<ProviderResponseEnvelope> HandleQueryAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderQueryRequest);
        if (!this.IsSupportedResourceType(request.ResourceType))
        {
            return ResourceTypeFailure(envelope.RequestId, request.ResourceType);
        }

        if (request.PageSize <= 0 || request.PageSize > this.capabilities!.Batch.MaxItems)
        {
            return BatchFailure(envelope.RequestId, request.PageSize);
        }

        var response = await this.provider.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderQueryResponse);
    }

    private async Task<ProviderResponseEnvelope> HandleGetAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderGetRequest);
        if (!this.IsValidBatch(request.Resources.Count))
        {
            return BatchFailure(envelope.RequestId, request.Resources.Count);
        }

        var unsupportedType = request.Resources
            .Select(resource => resource.ResourceType)
            .FirstOrDefault(resourceType => !this.IsSupportedResourceType(resourceType));
        if (unsupportedType is not null)
        {
            return ResourceTypeFailure(envelope.RequestId, unsupportedType);
        }

        var response = await this.provider.GetAsync(request, cancellationToken).ConfigureAwait(false);
        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderGetResponse);
    }

    private async Task<ProviderResponseEnvelope> HandleValidateAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderValidateRequest);
        if (!this.IsValidBatch(request.Snapshots.Count))
        {
            return BatchFailure(envelope.RequestId, request.Snapshots.Count);
        }

        var unsupportedType = request.Snapshots
            .Select(snapshot => snapshot.ResourceType)
            .FirstOrDefault(resourceType => !this.IsSupportedResourceType(resourceType));
        if (unsupportedType is not null)
        {
            return ResourceTypeFailure(envelope.RequestId, unsupportedType);
        }

        var response = await this.provider.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderValidateResponse);
    }

    private async Task<ProviderResponseEnvelope> HandlePlanAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderPlanChangesRequest);
        if (!this.IsSupportedResourceType(request.ResourceType))
        {
            return ResourceTypeFailure(envelope.RequestId, request.ResourceType);
        }

        if (!this.IsValidBatch(request.Changes.Count))
        {
            return BatchFailure(envelope.RequestId, request.Changes.Count);
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedAuthorityEpoch))
        {
            return Failure(
                envelope.RequestId,
                "provider.concurrency.authority-epoch.required",
                "Planning changes requires an expected authority epoch.");
        }

        ProviderDiagnostic? violation = request.Changes
            .Select(change => ResourceProviderContractValidator.ValidateChange(
                change,
                request.ResourceType,
                request.ResourceSet,
                request.ExpectedAuthorityEpoch))
            .FirstOrDefault(diagnostic => diagnostic is not null);
        if (violation is not null)
        {
            return Failure(
                envelope.RequestId,
                violation.Code,
                violation.Message,
                target: violation.Target);
        }

        var response = await this.provider.PlanChangesAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.PlanId)
            || !ResourceProviderContractValidator.IsSha256(response.PlanDigest)
            || !string.Equals(response.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal)
            || !response.RequiresExplicitApproval)
        {
            return Failure(
                envelope.RequestId,
                "provider.plan.coordinates.invalid",
                "Provider plan must return an ID, digest, matching authority epoch, and explicit approval invariant.");
        }

        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderPlanChangesResponse);
    }

    private async Task<ProviderResponseEnvelope> HandleApplyAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderApplyChangesRequest);
        if (!this.IsSupportedResourceType(request.ResourceType))
        {
            return ResourceTypeFailure(envelope.RequestId, request.ResourceType);
        }

        if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || !string.Equals(envelope.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Failure(
                envelope.RequestId,
                "provider.idempotency.key.invalid",
                "Apply requires the same non-empty idempotency key in the envelope and body.");
        }

        if (string.IsNullOrWhiteSpace(request.PlanId)
            || string.IsNullOrWhiteSpace(request.PlanDigest)
            || string.IsNullOrWhiteSpace(request.ExpectedAuthorityEpoch))
        {
            return Failure(
                envelope.RequestId,
                "provider.apply.coordinates.required",
                "Apply requires plan ID, plan digest, and expected authority epoch.");
        }

        if (string.IsNullOrWhiteSpace(request.Approval.ApprovedBy)
            || string.IsNullOrWhiteSpace(request.Approval.ApprovalReference))
        {
            return Failure(
                envelope.RequestId,
                "provider.apply.approval.required",
                "Apply requires an explicit approver and approval reference.");
        }

        var response = await this.provider.ApplyChangesAsync(request, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(response.PlanId, request.PlanId, StringComparison.Ordinal)
            || !string.Equals(response.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            || !string.Equals(response.AuthorityEpoch, request.ExpectedAuthorityEpoch, StringComparison.Ordinal))
        {
            return Failure(
                envelope.RequestId,
                "provider.apply.coordinates.invalid",
                "Provider apply result did not match the requested plan, idempotency key, or authority epoch.");
        }

        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderApplyChangesResponse);
    }

    private async Task<ProviderResponseEnvelope> HandleHealthAsync(
        ProviderRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var request = DeserializeBody(envelope, ProviderProtocolJsonContext.Default.ProviderHealthRequest);
        var response = await this.provider.GetHealthAsync(request, cancellationToken).ConfigureAwait(false);
        return Success(envelope.RequestId, response, ProviderProtocolJsonContext.Default.ProviderHealthResponse);
    }

    private bool IsValidBatch(int count)
    {
        return count > 0 && count <= this.capabilities!.Batch.MaxItems;
    }

    private ProviderResponseEnvelope BatchFailure(string requestId, int count)
    {
        return Failure(
            requestId,
            "provider.batch.size.invalid",
            $"Batch size {count} must be between 1 and {this.capabilities!.Batch.MaxItems}.");
    }

    private bool IsSupportedResourceType(string resourceType)
    {
        return this.capabilities!.ResourceTypes.Contains(resourceType, StringComparer.OrdinalIgnoreCase);
    }

    private static ProviderResponseEnvelope ResourceTypeFailure(string requestId, string resourceType)
    {
        return Failure(
            requestId,
            "provider.resource-type.unsupported",
            $"Provider does not advertise resource type '{resourceType}'.");
    }

    private static T DeserializeBody<T>(ProviderRequestEnvelope envelope, JsonTypeInfo<T> typeInfo)
    {
        return envelope.Body.Deserialize(typeInfo)
            ?? throw new ResourceProviderProtocolException(
                new ProviderError(
                    "provider.protocol.body.invalid",
                    $"Operation '{envelope.Operation}' has an invalid request body."));
    }

    private static ProviderResponseEnvelope Success<T>(
        string requestId,
        T body,
        JsonTypeInfo<T> typeInfo)
    {
        return new ProviderResponseEnvelope(
            ResourceProviderProtocol.Version1,
            requestId,
            Ok: true,
            JsonSerializer.SerializeToElement(body, typeInfo),
            [],
            Error: null);
    }

    private static ProviderResponseEnvelope Failure(
        string requestId,
        string code,
        string message,
        bool retryable = false,
        string? target = null)
    {
        var diagnostic = new ProviderDiagnostic(
            code,
            ProviderDiagnosticSeverity.Error,
            message,
            target,
            retryable);
        return new ProviderResponseEnvelope(
            ResourceProviderProtocol.Version1,
            requestId,
            Ok: false,
            Body: null,
            [diagnostic],
            new ProviderError(code, message, retryable));
    }
}
