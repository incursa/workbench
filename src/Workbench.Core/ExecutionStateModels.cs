using System.Text.Json.Serialization;

#pragma warning disable MA0048 // Execution-state DTOs form one versioned serialization contract.

namespace Workbench.Core;

public static class ExecutionKinds
{
    public const string PullRequest = "pull_request";
    public const string Release = "release";
    public const string Deployment = "deployment";
}

public static class ExecutionStates
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Unknown = "unknown";
}

public sealed record ExecutionStateReference(
    string Kind,
    GithubRepoRef Repository,
    string Identifier,
    string ExternalReference,
    string? Url);

public sealed record ExecutionProviderStatus(
    bool Available,
    string? Diagnostic);

public sealed record ExecutionObservation(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("externalReference")] string ExternalReference,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("normalizedState")] string NormalizedState,
    [property: JsonPropertyName("nativeState")] string NativeState,
    [property: JsonPropertyName("observedAtUtc")] DateTimeOffset ObservedAtUtc,
    [property: JsonPropertyName("revision")] string? Revision);

public sealed record ExecutionDiagnostic(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("externalReference")] string? ExternalReference,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("retainedObservation")] bool RetainedObservation,
    [property: JsonPropertyName("previousObservedAtUtc")] DateTimeOffset? PreviousObservedAtUtc,
    [property: JsonPropertyName("previousObservationAgeSeconds")] long? PreviousObservationAgeSeconds);

public sealed record ExecutionStateSnapshot(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("workItemId")] string WorkItemId,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("refreshedAtUtc")] DateTimeOffset RefreshedAtUtc,
    [property: JsonPropertyName("observations")] IList<ExecutionObservation> Observations,
    [property: JsonPropertyName("diagnostics")] IList<ExecutionDiagnostic> Diagnostics);

public sealed record ExecutionStateData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("snapshot")] ExecutionStateSnapshot Snapshot);

public sealed record ExecutionStateOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] ExecutionStateData Data);
#pragma warning restore MA0048
