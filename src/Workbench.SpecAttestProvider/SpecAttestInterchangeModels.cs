using System.Text.Json;
using System.Text.Json.Nodes;
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
    public const string StandardEdition = "0.1.0-draft.2";
    public const string StandardCommit = "3dedb948549ee3bc07adc7360e80f9c4125076a0";
    public const string StandardSchemaPath = "model/model.schema.json";
    public const string StandardSchemaSha256 = "27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb";
    public const string EditionManifestSha256 = "885cb5168dcdd528b53046c6e50fe65ca4ebd8aa903ea4867b4e78b12f92abea";
    public const string EditionManifestSchemaPath = "schemas/standard-edition.schema.json";
    public const string EditionManifestSchemaSha256 = "eb12cb68f01bd03cb2c29ae549ca8d202a129f75c304ebcbf1b4e64a975048ef";
    public const string StandardPayloadSha256 = "0643a8067ed5684dcbaa29da5c9a206af813c142d1a7620bce2a81bddedd4b92";
    public const string ConformanceCorpusSha256 = "19498f4987e2babde1e8151c9886bc74ac491aca44f5ecdfb7af6added24cc87";
    public const string SignatureReference = "refs/tags/standard-v0.1.0-draft.2";
    public const string SignatureSha256 = "607d32b380053b7cb2c748e0e81dd0d3dd943f2d9a71df7dc2ff8575fa88c865";
    public const string ProofProfile = "auditable";
    public const string SafeMutationContract = "specattest.manager-safe-mutation.v1";

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

    public static bool MatchesManagerPin(JsonElement pin, ResourceCompatibilityPin expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return string.Equals(Text(pin, "contract"), expected.Contract, StringComparison.Ordinal)
            && string.Equals(Text(pin, "repository"), expected.Repository, StringComparison.Ordinal)
            && string.Equals(Text(pin, "edition"), expected.Edition, StringComparison.Ordinal)
            && string.Equals(Text(pin, "commit"), expected.Revision, StringComparison.Ordinal)
            && string.Equals(Text(pin, "schemaPath"), expected.SchemaPath, StringComparison.Ordinal)
            && string.Equals(Text(pin, "schemaSha256"), expected.SchemaSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "editionManifestSha256"), expected.EditionManifestSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "editionManifestSchemaPath"), expected.EditionManifestSchemaPath, StringComparison.Ordinal)
            && string.Equals(Text(pin, "editionManifestSchemaSha256"), expected.EditionManifestSchemaSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "payloadSha256"), expected.PayloadSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "conformanceCorpusSha256"), expected.ConformanceCorpusSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "signatureReference"), expected.SignatureReference, StringComparison.Ordinal)
            && string.Equals(Text(pin, "signatureSha256"), expected.SignatureSha256, StringComparison.Ordinal)
            && string.Equals(Text(pin, "canonicalization"), expected.Canonicalization, StringComparison.Ordinal)
            && string.Equals(Text(pin, "proofProfile"), expected.ProofProfile, StringComparison.Ordinal)
            && pin.TryGetProperty("canonicalProfiles", out JsonElement profiles)
            && profiles.ValueKind == JsonValueKind.Array
            && profiles.EnumerateArray().Select(static item => item.GetString()).SequenceEqual(
                expected.CanonicalProfiles ?? [],
                StringComparer.Ordinal);
    }

    public static JsonObject ManagerPin(ResourceCompatibilityPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        SpecAttestInterchangeValidator.ValidatePin(pin);
        return new JsonObject
        {
            ["contract"] = pin.Contract,
            ["repository"] = pin.Repository,
            ["edition"] = pin.Edition,
            ["commit"] = pin.Revision,
            ["schemaPath"] = pin.SchemaPath,
            ["schemaSha256"] = pin.SchemaSha256,
            ["editionManifestSha256"] = pin.EditionManifestSha256,
            ["editionManifestSchemaPath"] = pin.EditionManifestSchemaPath,
            ["editionManifestSchemaSha256"] = pin.EditionManifestSchemaSha256,
            ["payloadSha256"] = pin.PayloadSha256,
            ["conformanceCorpusSha256"] = pin.ConformanceCorpusSha256,
            ["signatureReference"] = pin.SignatureReference,
            ["signatureSha256"] = pin.SignatureSha256,
            ["canonicalization"] = pin.Canonicalization,
            ["canonicalProfiles"] = new JsonArray((pin.CanonicalProfiles ?? [])
                .Select(static profile => (JsonNode?)JsonValue.Create(profile)).ToArray()),
            ["proofProfile"] = pin.ProofProfile
        };
    }

    private static string? Text(JsonElement value, string propertyName)
        => value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
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

public sealed record SpecAttestSafeMutationCapabilities(
    string Contract,
    IReadOnlyList<string> Operations,
    bool IdempotencyKeysRequired,
    bool ExpectedVersionsRequired,
    bool AtomicCommit,
    bool AutomaticMerge,
    bool ImmutableAuditAndVersions,
    bool DeterministicRetry);

public sealed record SpecAttestSafeMutationOperation(
    string ImportRunId,
    string ProjectId,
    string Status,
    long RecordVersion,
    long ExpectedProjectVersion,
    string RequestDigest,
    JsonElement Content);

#pragma warning restore MA0048
