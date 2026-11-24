using System.Text.Json.Serialization;

#pragma warning disable MA0048 // Provider DTOs form one versioned wire contract.
#pragma warning disable RCS1194 // This domain exception intentionally requires structured provider error data.

namespace Workbench.Core;

/// <summary>Names and invariants shared by every resource provider protocol implementation.</summary>
public static class ResourceProviderProtocol
{
    public const string Version1 = "workbench.resource-provider.v1";
    public const string MediaType = "application/vnd.incursa.workbench.resource-provider+json";
}

/// <summary>Operation names carried by the versioned protocol envelope.</summary>
public static class ResourceProviderOperations
{
    public const string Handshake = "handshake";
    public const string Query = "query";
    public const string Get = "get";
    public const string Validate = "validate";
    public const string PlanChanges = "planChanges";
    public const string ApplyChanges = "applyChanges";
    public const string Health = "health";

    public static IReadOnlyList<string> All { get; } =
    [
        Handshake,
        Query,
        Get,
        Validate,
        PlanChanges,
        ApplyChanges,
        Health
    ];
}

/// <summary>One newline-delimited JSON request. The body shape is selected by <see cref="Operation"/>.</summary>
public sealed record ProviderRequestEnvelope(
    string ProtocolVersion,
    string RequestId,
    string Operation,
    DateTimeOffset DeadlineUtc,
    string? IdempotencyKey,
    JsonElement Body);

/// <summary>One newline-delimited JSON response correlated by request identifier.</summary>
public sealed record ProviderResponseEnvelope(
    string ProtocolVersion,
    string RequestId,
    bool Ok,
    JsonElement? Body,
    IReadOnlyList<ProviderDiagnostic> Diagnostics,
    ProviderError? Error);

/// <summary>Machine-actionable failure details. Human detail belongs in diagnostics.</summary>
public sealed record ProviderError(
    string Code,
    string Message,
    bool Retryable = false);

/// <summary>Severity of one structured provider diagnostic.</summary>
public enum ProviderDiagnosticSeverity
{
    [JsonStringEnumMemberName("info")]
    Info,
    [JsonStringEnumMemberName("warning")]
    Warning,
    [JsonStringEnumMemberName("error")]
    Error
}

/// <summary>A structured provider diagnostic suitable for automation and human display.</summary>
public sealed record ProviderDiagnostic(
    string Code,
    ProviderDiagnosticSeverity Severity,
    string Message,
    string? Target = null,
    bool Retryable = false,
    IReadOnlyDictionary<string, string>? Data = null);

/// <summary>Handshake sent before any operation other than health.</summary>
public sealed record ProviderHandshakeRequest(
    string ClientName,
    string ClientVersion,
    IReadOnlyList<string> AcceptedProtocolVersions);

/// <summary>Selected protocol and the provider's complete capability declaration.</summary>
public sealed record ProviderHandshakeResponse(
    string SelectedProtocolVersion,
    ProviderCapabilities Capabilities);

/// <summary>Batch behavior advertised by a provider.</summary>
public sealed record ProviderBatchCapabilities(
    int MaxItems,
    bool AtomicApply,
    bool OrderedResults);

/// <summary>
/// Provider identity and supported contract surface. Capability absence must fail closed before an operation is attempted.
/// </summary>
public sealed record ProviderCapabilities(
    string ProviderId,
    string DisplayName,
    string ProviderVersion,
    IReadOnlyList<string> ProtocolVersions,
    IReadOnlyList<string> Operations,
    IReadOnlyList<string> ResourceTypes,
    IReadOnlyList<ResourceProviderRole> Roles,
    ProviderBatchCapabilities Batch,
    bool SupportsIdempotency,
    bool SupportsOptimisticConcurrency,
    bool SupportsOfflineSnapshots);

/// <summary>Stable address of one provider-neutral resource.</summary>
public sealed record ResourceAddress(
    string ResourceType,
    string ResourceSet,
    string ResourceId);

/// <summary>
/// Immutable compatibility coordinate required to interpret or validate content. It is generic and does not select a SpecAttest pin.
/// </summary>
public sealed record ResourceCompatibilityPin(
    string Contract,
    string Repository,
    string? Edition,
    string Revision,
    string SchemaPath,
    string SchemaSha256,
    string Canonicalization,
    string? Profile);

/// <summary>Provenance retained whenever a resource crosses an authority boundary.</summary>
public sealed record ResourceProvenance(
    string ProviderId,
    DateTimeOffset RecordedAtUtc,
    string? Actor,
    string? SourceRevision,
    string PayloadSha256,
    string? TransferId,
    string? ApprovalReference);

/// <summary>Current writer declaration for one resource type/set authority domain.</summary>
public sealed record ResourceAuthorityDeclaration(
    string ResourceType,
    string ResourceSet,
    string AuthorityProviderId,
    string AuthorityEpoch,
    DateTimeOffset DeclaredAtUtc,
    string? DeclarationDigest);

/// <summary>A canonical resource plus its concurrency token, digest, compatibility pin, and provenance.</summary>
public sealed record ResourceDocument(
    ResourceAddress Address,
    JsonElement Content,
    string VersionToken,
    string PayloadSha256,
    ResourceCompatibilityPin? CompatibilityPin,
    ResourceProvenance Provenance);

/// <summary>A lightweight query result that does not omit concurrency or digest coordinates.</summary>
public sealed record ResourceSummary(
    ResourceAddress Address,
    string VersionToken,
    string PayloadSha256,
    DateTimeOffset? ModifiedAtUtc);

/// <summary>
/// Immutable point-in-time collection. Authoritative snapshots name their authority; imported or derived snapshots set
/// <see cref="Authoritative"/> to false and retain the source declaration.
/// </summary>
public sealed record ResourceSnapshot(
    string SnapshotId,
    string ResourceType,
    string ResourceSet,
    DateTimeOffset CreatedAtUtc,
    string PayloadSha256,
    bool Authoritative,
    ResourceAuthorityDeclaration Authority,
    ResourceCompatibilityPin? CompatibilityPin,
    IReadOnlyList<ResourceDocument> Resources,
    ResourceProvenance Provenance);

/// <summary>A read-only derived view of a snapshot. A projection never becomes normative content.</summary>
public sealed record ResourceProjection(
    string ProjectionId,
    string SourceSnapshotId,
    string ProviderId,
    string MediaType,
    string PayloadSha256,
    JsonElement Content,
    ResourceProvenance Provenance);

/// <summary>
/// Immutable named pointer to a snapshot used for review or comparison. A baseline is not another writable authority.
/// </summary>
public sealed record ResourceBaseline(
    string BaselineId,
    string SnapshotId,
    string SnapshotPayloadSha256,
    DateTimeOffset CreatedAtUtc,
    ResourceProvenance Provenance);

/// <summary>
/// Receipt returned after an explicit publication apply. The manifest digest is authoritative; rendered projections remain derived.
/// </summary>
public sealed record ResourcePublicationReceipt(
    string PublicationId,
    string PublicationTargetProviderId,
    string SourceSnapshotId,
    string SourcePayloadSha256,
    string ManifestSha256,
    DateTimeOffset PublishedAtUtc,
    string ImmutableReference,
    ResourceProvenance Provenance);

/// <summary>Immutable record required when the sole live writer moves between providers.</summary>
public sealed record ResourceAuthorityTransfer(
    string TransferId,
    string ResourceType,
    string ResourceSet,
    string FromProviderId,
    string ToProviderId,
    string FromAuthorityEpoch,
    string ToAuthorityEpoch,
    string SourceVersion,
    string TargetVersion,
    string PayloadSha256,
    DateTimeOffset FrozenAtUtc,
    string ApprovedBy,
    string ApprovalReference,
    DateTimeOffset ApprovedAtUtc,
    DateTimeOffset? RollbackUntilUtc);

/// <summary>Query request with opaque provider filtering and continuation.</summary>
public sealed record ProviderQueryRequest(
    string ResourceType,
    string ResourceSet,
    JsonElement? Filter,
    int PageSize,
    string? ContinuationToken);

/// <summary>One query page. Results are ordered only when the provider advertises ordered batch behavior.</summary>
public sealed record ProviderQueryResponse(
    IReadOnlyList<ResourceSummary> Resources,
    string? ContinuationToken,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Batch get request.</summary>
public sealed record ProviderGetRequest(IReadOnlyList<ResourceAddress> Resources);

/// <summary>Batch get response, including per-item diagnostics for missing or unreadable resources.</summary>
public sealed record ProviderGetResponse(
    IReadOnlyList<ResourceDocument> Resources,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Validates immutable snapshots without mutating authority state.</summary>
public sealed record ProviderValidateRequest(IReadOnlyList<ResourceSnapshot> Snapshots);

/// <summary>Validation result pinned to the exact snapshot digest and compatibility coordinate.</summary>
public sealed record ProviderValidationResult(
    string SnapshotId,
    string PayloadSha256,
    bool Valid,
    ResourceCompatibilityPin? CompatibilityPin,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Batch validation response.</summary>
public sealed record ProviderValidateResponse(IReadOnlyList<ProviderValidationResult> Results);

/// <summary>Kind of change proposed to the current authority.</summary>
public enum ResourceChangeKind
{
    [JsonStringEnumMemberName("create")]
    Create,
    [JsonStringEnumMemberName("update")]
    Update,
    [JsonStringEnumMemberName("delete")]
    Delete,
    [JsonStringEnumMemberName("publish")]
    Publish,
    [JsonStringEnumMemberName("transferAuthority")]
    TransferAuthority
}

/// <summary>
/// One proposed change. Update/delete/transfer operations require an expected version token; all payload-bearing changes carry a digest.
/// </summary>
public sealed record ResourceChange(
    string ChangeId,
    ResourceChangeKind Kind,
    ResourceAddress Address,
    JsonElement? Content,
    string? ExpectedVersionToken,
    string? PayloadSha256,
    ResourceProvenance Provenance,
    ResourceAuthorityTransfer? AuthorityTransfer = null);

/// <summary>Plans a bounded batch against one authority epoch without mutating resources.</summary>
public sealed record ProviderPlanChangesRequest(
    string ResourceType,
    string ResourceSet,
    string ExpectedAuthorityEpoch,
    IReadOnlyList<ResourceChange> Changes);

/// <summary>Disposition of one planned change.</summary>
public enum ProviderChangeDisposition
{
    [JsonStringEnumMemberName("ready")]
    Ready,
    [JsonStringEnumMemberName("conflict")]
    Conflict,
    [JsonStringEnumMemberName("invalid")]
    Invalid,
    [JsonStringEnumMemberName("unsupported")]
    Unsupported
}

/// <summary>Per-change plan result with the version token observed by the provider.</summary>
public sealed record ProviderPlannedChange(
    string ChangeId,
    ProviderChangeDisposition Disposition,
    string? ObservedVersionToken,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>
/// Immutable plan. Applying requires the plan digest, matching authority epoch, idempotency key, and explicit approval.
/// </summary>
public sealed record ProviderPlanChangesResponse(
    string PlanId,
    string PlanDigest,
    string AuthorityEpoch,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool RequiresExplicitApproval,
    IReadOnlyList<ProviderPlannedChange> Changes,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Human or controlling-system approval for an authority-changing apply operation.</summary>
public sealed record ProviderApproval(
    string ApprovedBy,
    string ApprovalReference,
    DateTimeOffset ApprovedAtUtc);

/// <summary>Applies a previously returned immutable plan.</summary>
public sealed record ProviderApplyChangesRequest(
    string ResourceType,
    string ResourceSet,
    string PlanId,
    string PlanDigest,
    string IdempotencyKey,
    string ExpectedAuthorityEpoch,
    ProviderApproval Approval);

/// <summary>Terminal or retryable outcome of an apply operation.</summary>
public enum ProviderApplyStatus
{
    [JsonStringEnumMemberName("applied")]
    Applied,
    [JsonStringEnumMemberName("alreadyApplied")]
    AlreadyApplied,
    [JsonStringEnumMemberName("conflict")]
    Conflict,
    [JsonStringEnumMemberName("rejected")]
    Rejected
}

/// <summary>Apply result with a resulting immutable snapshot when content changed.</summary>
public sealed record ProviderApplyChangesResponse(
    ProviderApplyStatus Status,
    string PlanId,
    string IdempotencyKey,
    string AuthorityEpoch,
    ResourceSnapshot? Snapshot,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Health request. Deep checks are explicit because they may contact dependencies.</summary>
public sealed record ProviderHealthRequest(bool Deep = false);

/// <summary>Provider health classification.</summary>
public enum ProviderHealthState
{
    [JsonStringEnumMemberName("healthy")]
    Healthy,
    [JsonStringEnumMemberName("degraded")]
    Degraded,
    [JsonStringEnumMemberName("unavailable")]
    Unavailable
}

/// <summary>Health response suitable for readiness diagnostics.</summary>
public sealed record ProviderHealthResponse(
    ProviderHealthState State,
    DateTimeOffset CheckedAtUtc,
    string ProviderVersion,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

/// <summary>Protocol exception preserving structured error and diagnostic details.</summary>
public sealed class ResourceProviderProtocolException : Exception
{
    public ResourceProviderProtocolException(
        ProviderError error,
        IReadOnlyList<ProviderDiagnostic>? diagnostics = null)
        : base(error.Message)
    {
        this.Error = error;
        this.Diagnostics = diagnostics ?? [];
    }

    public ProviderError Error { get; }

    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; }
}

#pragma warning restore RCS1194
#pragma warning restore MA0048
