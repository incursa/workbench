namespace Workbench.Core;

/// <summary>
/// Provider implementation contract. First-party local providers implement this interface and expose it through
/// <see cref="ResourceProviderProtocolHost"/> in a separate process; Workbench does not invoke provider implementations directly.
/// </summary>
public interface IResourceProvider
{
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    Task<ProviderQueryResponse> QueryAsync(
        ProviderQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderGetResponse> GetAsync(
        ProviderGetRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderValidateResponse> ValidateAsync(
        ProviderValidateRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderPlanChangesResponse> PlanChangesAsync(
        ProviderPlanChangesRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderApplyChangesResponse> ApplyChangesAsync(
        ProviderApplyChangesRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderHealthResponse> GetHealthAsync(
        ProviderHealthRequest request,
        CancellationToken cancellationToken = default);
}
