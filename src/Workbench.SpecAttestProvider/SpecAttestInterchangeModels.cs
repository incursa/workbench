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
    public const string StandardRepository = "https://github.com/incursa/spec-trace";
    public const string StandardEdition = "0.1.0-draft.1";
    public const string StandardCommit = "569569f7ffb2b68a45e4b5c3c276951404973470";
    public const string StandardSchemaPath = "model/model.schema.json";
    public const string StandardSchemaSha256 = "27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb";
    public const string EditionManifestSha256 = "6b54ca666d90456ae7edb277114945fcc4163e6084a3f44e913fc19e40691fda";
    public const string EditionManifestSchemaPath = "schemas/standard-edition.schema.json";
    public const string EditionManifestSchemaSha256 = "456de53b5359691b21540bd2803529119930c0db507d760060a224f42e0a7364";
    public const string StandardPayloadSha256 = "288593ff470fc5661a370c22377ec7c7503ae24a66f50e014e1f42b0426f3db4";
    public const string ConformanceCorpusSha256 = "8d6a2d02d5293ca531aee9eaa9aff771dceb88d34f6d8dd45c396710e0881d33";
    public const string SignatureReference = "refs/tags/standard-v0.1.0-draft.1";
    public const string SignatureSha256 = "c0c4e93600a803a3c3f48780e307666cfd76f7122a155c19e416f21774f6d056";
    public const string ProofProfile = "auditable";

    public static ResourceCompatibilityPin StandardPin(string profile = "auditable") => new(
        StandardPinContract,
        StandardRepository,
        StandardEdition,
        StandardCommit,
        StandardSchemaPath,
        StandardSchemaSha256,
        Canonicalization,
        profile,
        EditionManifestSha256,
        EditionManifestSchemaPath,
        EditionManifestSchemaSha256,
        StandardPayloadSha256,
        ConformanceCorpusSha256,
        SignatureReference,
        SignatureSha256,
        ["core", "traceable", "auditable"],
        ProofProfile);
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
