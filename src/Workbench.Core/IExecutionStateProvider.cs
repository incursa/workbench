namespace Workbench.Core;

/// <summary>
/// Read-only boundary for observing execution state owned by an external system.
/// </summary>
public interface IExecutionStateProvider
{
    string Name { get; }

    Task<ExecutionProviderStatus> CheckAvailabilityAsync(
        string repoRoot,
        CancellationToken cancellationToken = default);

    Task<ExecutionObservation> ObserveAsync(
        string repoRoot,
        ExecutionStateReference reference,
        CancellationToken cancellationToken = default);
}
