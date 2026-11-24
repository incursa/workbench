using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Core;

#pragma warning disable MA0048 // Interchange DTOs form one versioned wire contract.

namespace Workbench.SpecAttestProvider;

public static class SpecAttestInterchangeContract
{
    public const string Format = "specattest.interchange.v1";
    public const string StandardPinContract = "specattest.standard-pin.v1";
    public const string Canonicalization = "utf8-json-sorted-object-keys-v1";
    public const string PayloadMediaType = "application/vnd.specattest.interchange+zip";
    public const string ResourceType = "specattest_set";
}

public sealed record SpecAttestInterchangePackage(
    SpecAttestInterchangeManifest Manifest,
    IReadOnlyList<SpecAttestInterchangeEntry> Entries,
    JsonElement RetiredIdLedger,
    IReadOnlyList<JsonElement> ExternalReferences,
    IReadOnlyList<JsonElement> EvidenceDescriptors,
    IReadOnlyList<SpecAttestAuditReference> AuditReferences);

public sealed record SpecAttestInterchangeManifest(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("transfer_id")] string TransferId,
    [property: JsonPropertyName("set_id")] string SetId,
    [property: JsonPropertyName("authority_mode")] string AuthorityMode,
    [property: JsonPropertyName("transition")] string Transition,
    [property: JsonPropertyName("source")] SpecAttestInterchangeSource Source,
    [property: JsonPropertyName("standard")] ResourceCompatibilityPin Standard,
    [property: JsonPropertyName("producer")] SpecAttestInterchangeProducer Producer,
    [property: JsonPropertyName("scope")] SpecAttestInterchangeScope Scope,
    [property: JsonPropertyName("payload")] SpecAttestInterchangePayload Payload,
    [property: JsonPropertyName("validation")] SpecAttestInterchangeValidation Validation,
    [property: JsonPropertyName("provenance")] SpecAttestInterchangeProvenance Provenance);

public sealed record SpecAttestInterchangeSource(
    string System,
    string? Repository,
    string? Commit,
    string? BaseCommit,
    string? ManagerProjectId,
    string? ManagerPackageVersion,
    long? ManagerRecordVersion);

public sealed record SpecAttestInterchangeProducer(
    string Name,
    string Version,
    string ValidationEngine);

public sealed record SpecAttestInterchangeScope(
    IReadOnlyList<string> ArtifactIds,
    IReadOnlyList<string> RequirementIds,
    bool RetiredLedgerIncluded);

public sealed record SpecAttestInterchangePayload(
    string MediaType,
    string Sha256,
    string EntryManifestSha256,
    string Canonicalization);

public sealed record SpecAttestInterchangeValidation(
    bool CanonicalProfilePassed,
    DateTimeOffset? ValidatedAt,
    string FindingsSha256);

public sealed record SpecAttestInterchangeProvenance(
    string Actor,
    DateTimeOffset CreatedAt,
    string? ApprovalReference);

public sealed record SpecAttestInterchangeEntry(
    string StableId,
    string EntryKind,
    string Path,
    string Sha256,
    JsonElement CanonicalContent);

public sealed record SpecAttestAuditReference(
    string AuditId,
    DateTimeOffset? OccurredAt,
    string Sha256);

public sealed record SpecAttestManagerExport(
    string PackageVersion,
    string ProjectId,
    long RecordVersion,
    DateTimeOffset UpdatedAt,
    JsonElement Content);

public sealed record SpecAttestManagerStatus(
    ProviderHealthState State,
    string Version,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

#pragma warning restore MA0048
