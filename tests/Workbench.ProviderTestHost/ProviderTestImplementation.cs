using Workbench.Core;

namespace Workbench.ProviderTestHost;

internal sealed class ProviderTestImplementation : IResourceProvider
{
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCapabilities(
            "test-process",
            "Provider process test host",
            "1.0.0",
            [ResourceProviderProtocol.Version1],
            ResourceProviderOperations.All,
            ["specification"],
            [ResourceProviderRole.Authority],
            new ProviderBatchCapabilities(10, AtomicApply: true, OrderedResults: true),
            SupportsIdempotency: true,
            SupportsOptimisticConcurrency: true,
            SupportsOfflineSnapshots: true));
    }

    public Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderQueryResponse([], null, []));
    }

    public Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderGetResponse([], []));
    }

    public Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderValidateResponse([]));
    }

    public Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderPlanChangesResponse(
            "plan-1",
            new string('a', 64),
            request.ExpectedAuthorityEpoch,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            RequiresExplicitApproval: true,
            [],
            []));
    }

    public Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderApplyChangesResponse(
            ProviderApplyStatus.Applied,
            request.PlanId,
            request.IdempotencyKey,
            request.ExpectedAuthorityEpoch,
            Snapshot: null,
            []));
    }

    public async Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Deep)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        }

        return new ProviderHealthResponse(
            ProviderHealthState.Healthy,
            DateTimeOffset.UtcNow,
            "1.0.0",
            []);
    }
}
