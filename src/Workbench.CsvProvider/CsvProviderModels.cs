using System.Text.Json;
using Workbench.Core;

#pragma warning disable MA0048 // Internal CSV state records form one persistence contract.

namespace Workbench.CsvProvider;

public enum CsvRecordMode
{
    Authoritative,
    Projection
}

public enum CsvProjectionOperation
{
    None,
    Upsert,
    Delete
}

public sealed record CsvReconciliationPreview(
    ProviderPlanChangesRequest? PlanRequest,
    IReadOnlyList<ProviderPlannedChange> Changes,
    IReadOnlyList<ProviderDiagnostic> Diagnostics)
{
    public bool CanPlan => this.PlanRequest is not null
        && this.Changes.Count > 0
        && this.Changes.All(change => change.Disposition == ProviderChangeDisposition.Ready)
        && this.Diagnostics.All(diagnostic => diagnostic.Severity != ProviderDiagnosticSeverity.Error);
}

internal sealed record CsvRowDocument(
    ResourceDocument Document,
    CsvRecordMode Mode,
    CsvProjectionOperation Operation,
    string BaseVersionToken,
    string BasePayloadSha256,
    IReadOnlyDictionary<string, string> RawFields);

internal sealed record CsvTable(
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvRowDocument> Rows,
    string FileDigest);

internal sealed record PersistedCsvPlan(
    string PlanId,
    string PlanDigest,
    string AuthorityEpoch,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string ResourceType,
    string ResourceSet,
    string SourceFileDigest,
    IReadOnlyDictionary<string, string> SourcePayloads,
    IReadOnlyList<ResourceChange> Changes,
    IReadOnlyList<ProviderPlannedChange> Results);

internal sealed record PersistedCsvApply(
    string IdempotencyKey,
    string PlanId,
    string PlanDigest,
    DateTimeOffset AppliedAtUtc,
    string SnapshotId,
    string SnapshotPayloadSha256,
    ResourceSnapshot? Snapshot);

internal sealed record CsvContent(
    string Id,
    string Title,
    string Status,
    IReadOnlyDictionary<string, JsonElement> Fields,
    IReadOnlyDictionary<string, string> CsvExtensions,
    JsonElement Json);

#pragma warning restore MA0048
