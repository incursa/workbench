using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Workbench.Core;

#pragma warning disable MA0048 // Codec and validator share the same versioned interchange contract.

namespace Workbench.SpecAttestProvider;

public static class SpecAttestInterchange
{
    public static SpecAttestInterchangePackage FromManagerExport(
        SpecAttestManagerExport export,
        SpecAttestProviderOptions options,
        string producerVersion)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!string.Equals(export.ProjectId, options.ManagerProjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manager returned project '{export.ProjectId}' for configured project '{options.ManagerProjectId}'.");
        }

        JsonElement root = export.Content;
        List<SpecAttestInterchangeEntry> entries = [];
        AddEntries(root, "artifacts", "artifact_id", static item => RequiredText(item, "artifact_type"), "artifacts", entries);
        AddEntries(root, "requirements", "id", static _ => "requirement", "requirements", entries);
        entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.StableId, right.StableId));

        bool retiredLedgerIncluded = root.TryGetProperty("retiredRequirementLedger", out JsonElement managerRetiredLedger);
        JsonElement retiredLedger = retiredLedgerIncluded ? managerRetiredLedger.Clone() : JsonSerializer.SerializeToElement(new JsonObject
        {
            ["retired_requirements"] = new JsonArray()
        });
        IReadOnlyList<JsonElement> evidence = CloneArray(root, "evidenceSnapshots");
        IReadOnlyList<SpecAttestAuditReference> auditReferences = BuildAuditReferences(root);
        IReadOnlyList<JsonElement> externalReferences = CloneArray(root, "externalReferences");
        string entryManifestSha256 = ComputeEntryManifestSha256(entries);
        string payloadSha256 = ComputePayloadSha256(entries, retiredLedger, externalReferences, evidence, auditReferences);
        string transferId = CreateDeterministicTransferId(options.SetId, payloadSha256);

        SpecAttestInterchangeManifest manifest = new(
            SpecAttestInterchangeContract.Format,
            transferId,
            options.SetId,
            options.AuthorityMode,
            "snapshot-export",
            new SpecAttestInterchangeSource(
                "manager",
                Repository: null,
                Commit: null,
                BaseCommit: null,
                options.ManagerProjectId,
                export.PackageVersion,
                export.RecordVersion),
            options.StandardPin,
            new SpecAttestInterchangeProducer(
                "Incursa.Workbench.SpecAttestProvider",
                producerVersion,
                "unattested-manager-export"),
            new SpecAttestInterchangeScope(
                entries.Where(static entry => !string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
                    .Select(static entry => entry.StableId)
                    .ToArray(),
                entries.Where(static entry => string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
                    .Select(static entry => entry.StableId)
                    .ToArray(),
                RetiredLedgerIncluded: retiredLedgerIncluded),
            new SpecAttestInterchangePayload(
                SpecAttestInterchangeContract.PayloadMediaType,
                payloadSha256,
                entryManifestSha256,
                SpecAttestInterchangeContract.Canonicalization),
            new SpecAttestInterchangeValidation(
                CanonicalProfilePassed: false,
                ValidatedAt: null,
                SpecAttestCanonicalJson.Sha256("specattest.validation.not-attested")),
            new SpecAttestInterchangeProvenance(
                options.ProviderId,
                export.UpdatedAt,
                ApprovalReference: null));

        return new SpecAttestInterchangePackage(
            manifest,
            entries,
            retiredLedger,
            externalReferences,
            evidence,
            auditReferences);
    }

    public static string ComputePackageSha256(SpecAttestInterchangePackage package)
    {
        JsonElement element = JsonSerializer.SerializeToElement(
            package,
            SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        return SpecAttestCanonicalJson.Sha256(element);
    }

    public static string ComputeEntryManifestSha256(IReadOnlyList<SpecAttestInterchangeEntry> entries)
    {
        return SpecAttestCanonicalJson.Sha256(JsonSerializer.SerializeToElement(CreateEntryManifest(entries)));
    }

    public static string ComputePayloadSha256(
        IReadOnlyList<SpecAttestInterchangeEntry> entries,
        JsonElement retiredIdLedger,
        IReadOnlyList<JsonElement> externalReferences,
        IReadOnlyList<JsonElement> evidenceDescriptors,
        IReadOnlyList<SpecAttestAuditReference> auditReferences)
    {
        return Convert.ToHexStringLower(SHA256.HashData(BuildPayloadArchive(
            entries,
            retiredIdLedger,
            externalReferences,
            evidenceDescriptors,
            auditReferences)));
    }

    public static byte[] BuildPayloadArchive(SpecAttestInterchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return BuildPayloadArchive(
            package.Entries,
            package.RetiredIdLedger,
            package.ExternalReferences,
            package.EvidenceDescriptors,
            package.AuditReferences);
    }

    private static byte[] BuildPayloadArchive(
        IReadOnlyList<SpecAttestInterchangeEntry> entries,
        JsonElement retiredIdLedger,
        IReadOnlyList<JsonElement> externalReferences,
        IReadOnlyList<JsonElement> evidenceDescriptors,
        IReadOnlyList<SpecAttestAuditReference> auditReferences)
    {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteArchiveEntry(
                archive,
                "entry-manifest.json",
                SpecAttestCanonicalJson.Serialize(JsonSerializer.SerializeToElement(CreateEntryManifest(entries))));
            foreach (SpecAttestInterchangeEntry entry in entries.OrderBy(static item => item.Path, StringComparer.Ordinal))
            {
                ValidateArchiveEntry(entry);
                WriteArchiveEntry(archive, entry.Path, SpecAttestCanonicalJson.Serialize(entry.CanonicalContent));
            }

            WriteArchiveEntry(archive, "retired-id-ledger.json", SpecAttestCanonicalJson.Serialize(retiredIdLedger));
            WriteArchiveEntry(
                archive,
                "external-references.json",
                SpecAttestCanonicalJson.Serialize(JsonSerializer.SerializeToElement(ToArray(externalReferences))));
            WriteArchiveEntry(
                archive,
                "audit-references.json",
                SpecAttestCanonicalJson.Serialize(JsonSerializer.SerializeToElement(
                    auditReferences,
                    SpecAttestProviderJsonContext.Default.IReadOnlyListSpecAttestAuditReference)));
            int evidenceIndex = 0;
            foreach (JsonElement evidence in evidenceDescriptors)
            {
                evidenceIndex++;
                WriteArchiveEntry(
                    archive,
                    $"evidence/{evidenceIndex:D6}-{SpecAttestCanonicalJson.Sha256(evidence)}.json",
                    SpecAttestCanonicalJson.Serialize(evidence));
            }
        }

        return output.ToArray();
    }

    private static void AddEntries(
        JsonElement root,
        string propertyName,
        string idProperty,
        Func<JsonElement, string> kind,
        string pathSegment,
        ICollection<SpecAttestInterchangeEntry> entries)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Manager canonical export is missing array '{propertyName}'.");
        }

        foreach (JsonElement value in values.EnumerateArray())
        {
            string stableId = RequiredText(value, idProperty);
            if (!SpecAttestInterchangeValidator.IsStableIdentifier(stableId))
            {
                throw new JsonException($"Canonical entry ID '{stableId}' is not a safe SpecAttest stable identifier.");
            }

            JsonElement clone = value.Clone();
            entries.Add(new SpecAttestInterchangeEntry(
                stableId,
                kind(value),
                $"{pathSegment}/{stableId}.json",
                SpecAttestCanonicalJson.Sha256(clone),
                clone));
        }
    }

    private static IReadOnlyList<SpecAttestAuditReference> BuildAuditReferences(JsonElement root)
    {
        if (!root.TryGetProperty("history", out JsonElement history) || history.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<SpecAttestAuditReference> references = [];
        int index = 0;
        foreach (JsonElement item in history.EnumerateArray())
        {
            index++;
            string auditId = item.TryGetProperty("auditEventId", out JsonElement id)
                ? id.GetString() ?? $"audit-{index}"
                : $"audit-{index}";
            DateTimeOffset? occurredAt = item.TryGetProperty("occurredAt", out JsonElement occurred)
                && occurred.TryGetDateTimeOffset(out DateTimeOffset parsed)
                ? parsed
                : null;
            references.Add(new SpecAttestAuditReference(auditId, occurredAt, SpecAttestCanonicalJson.Sha256(item)));
        }

        return references;
    }

    private static IReadOnlyList<JsonElement> CloneArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values))
        {
            return [];
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Manager canonical export property '{propertyName}' must be an array.");
        }

        return values.EnumerateArray().Select(static item => item.Clone()).ToArray();
    }

    private static string RequiredText(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new JsonException($"Canonical entry is missing string '{propertyName}'.");
    }

    private static JsonArray ToArray(IEnumerable<JsonElement> values)
    {
        return new JsonArray(values.Select(static value => JsonNode.Parse(value.GetRawText())).ToArray());
    }

    private static JsonArray CreateEntryManifest(IReadOnlyList<SpecAttestInterchangeEntry> entries)
    {
        JsonArray manifest = [];
        foreach (SpecAttestInterchangeEntry entry in entries.OrderBy(static item => item.StableId, StringComparer.Ordinal))
        {
            manifest.Add(new JsonObject
            {
                ["stable_id"] = entry.StableId,
                ["entry_kind"] = entry.EntryKind,
                ["path"] = entry.Path,
                ["sha256"] = entry.Sha256
            });
        }

        return manifest;
    }

    private static void WriteArchiveEntry(ZipArchive archive, string path, ReadOnlySpan<byte> content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using Stream stream = entry.Open();
        stream.Write(content);
    }

    private static void ValidateArchiveEntry(SpecAttestInterchangeEntry entry)
    {
        if (!SpecAttestInterchangeValidator.IsStableIdentifier(entry.StableId))
        {
            throw new InvalidDataException($"Interchange entry ID '{entry.StableId}' is not a safe stable identifier.");
        }

        string segment = string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal)
            ? "requirements"
            : "artifacts";
        string expectedPath = $"{segment}/{entry.StableId}.json";
        if (!string.Equals(entry.Path, expectedPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Interchange entry path '{entry.Path}' must be the canonical path '{expectedPath}'.");
        }
    }

    private static string CreateDeterministicTransferId(string setId, string payloadSha256)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{setId}\n{payloadSha256}"));
        Span<byte> guidBytes = hash.AsSpan(0, 16);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes).ToString();
    }
}

public static partial class SpecAttestInterchangeValidator
{
    public static IReadOnlyList<ProviderDiagnostic> ValidateForAcceptance(
        SpecAttestInterchangePackage package,
        ResourceCompatibilityPin expectedPin,
        string expectedSetId,
        string expectedAuthorityMode)
    {
        List<ProviderDiagnostic> diagnostics = [];
        if (!string.Equals(package.Manifest.Format, SpecAttestInterchangeContract.Format, StringComparison.Ordinal))
        {
            Error(diagnostics, "specattest.interchange.format.unsupported", "Unsupported interchange format.", "manifest.format");
        }

        if (!string.Equals(package.Manifest.Transition, "snapshot-export", StringComparison.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.transition.unsupported",
                "This adapter accepts only the explicit snapshot-export transition.",
                "manifest.transition");
        }

        if (!Guid.TryParse(package.Manifest.TransferId, out _))
        {
            Error(diagnostics, "specattest.interchange.transfer-id.invalid", "Transfer ID must be a UUID.", "manifest.transfer_id");
        }

        if (!string.Equals(package.Manifest.SetId, expectedSetId, StringComparison.Ordinal)
            || !string.Equals(package.Manifest.AuthorityMode, expectedAuthorityMode, StringComparison.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.authority.conflict",
                "Set ID or authority mode does not match the configured authority domain.",
                "manifest");
        }

        if (!PinsEqual(package.Manifest.Standard, expectedPin))
        {
            Error(
                diagnostics,
                "specattest.interchange.standard-pin.conflict",
                "Interchange Standard pin does not exactly match the configured pin.",
                "manifest.standard");
        }

        if (!string.Equals(
                package.Manifest.Payload.MediaType,
                SpecAttestInterchangeContract.PayloadMediaType,
                StringComparison.Ordinal)
            || !string.Equals(
                package.Manifest.Payload.Canonicalization,
                SpecAttestInterchangeContract.Canonicalization,
                StringComparison.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.payload.contract-mismatch",
                "Payload media type or canonicalization contract is unsupported.",
                "manifest.payload");
        }

        ValidateSource(package.Manifest, diagnostics);

        HashSet<string> stableIds = new(StringComparer.Ordinal);
        HashSet<string> entryPaths = new(StringComparer.Ordinal);
        foreach (SpecAttestInterchangeEntry entry in package.Entries)
        {
            if (!stableIds.Add(entry.StableId))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.id.duplicate",
                    $"Stable ID '{entry.StableId}' is duplicated.",
                    entry.Path);
            }

            if (!entryPaths.Add(entry.Path))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.path.duplicate",
                    $"Entry path '{entry.Path}' is duplicated.",
                    entry.Path);
            }

            if (!IsStableIdentifier(entry.StableId))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.id.invalid",
                    $"Stable ID '{entry.StableId}' is not a safe portable identifier.",
                    entry.Path);
            }

            string actualEntrySha256 = SpecAttestCanonicalJson.Sha256(entry.CanonicalContent);
            if (!string.Equals(entry.Sha256, actualEntrySha256, StringComparison.Ordinal))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.entry.digest-mismatch",
                    $"Entry '{entry.StableId}' digest does not match its canonical content.",
                    entry.Path);
            }


            string contentIdProperty = string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal)
                ? "id"
                : "artifact_id";
            if (!entry.CanonicalContent.TryGetProperty(contentIdProperty, out JsonElement contentId)
                || contentId.ValueKind != JsonValueKind.String
                || !string.Equals(contentId.GetString(), entry.StableId, StringComparison.Ordinal))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.id.content-mismatch",
                    $"Entry '{entry.StableId}' does not match its canonical content identifier.",
                    entry.Path);
            }
        }

        string entryManifestSha256 = SpecAttestInterchange.ComputeEntryManifestSha256(package.Entries);
        if (!string.Equals(package.Manifest.Payload.EntryManifestSha256, entryManifestSha256, StringComparison.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.entry-manifest.digest-mismatch",
                "Entry manifest digest does not match the package entries.",
                "manifest.payload.entry_manifest_sha256");
        }

        string payloadSha256 = SpecAttestInterchange.ComputePayloadSha256(
            package.Entries,
            package.RetiredIdLedger,
            package.ExternalReferences,
            package.EvidenceDescriptors,
            package.AuditReferences);
        if (!string.Equals(package.Manifest.Payload.Sha256, payloadSha256, StringComparison.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.payload.digest-mismatch",
                "Payload digest does not match the deterministic package payload.",
                "manifest.payload.sha256");
        }

        string[] artifactIds = package.Entries
            .Where(static entry => !string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
            .Select(static entry => entry.StableId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] requirementIds = package.Entries
            .Where(static entry => string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
            .Select(static entry => entry.StableId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!artifactIds.SequenceEqual(package.Manifest.Scope.ArtifactIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !requirementIds.SequenceEqual(package.Manifest.Scope.RequirementIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            Error(
                diagnostics,
                "specattest.interchange.scope.mismatch",
                "Manifest scope does not exactly match the package entries.",
                "manifest.scope");
        }


        if (!package.Manifest.Scope.RetiredLedgerIncluded)
        {
            Error(
                diagnostics,
                "specattest.interchange.retired-ledger.required",
                "Accepted packages must include the retired-ID ledger.",
                "manifest.scope.retired_ledger_included");
        }


        if (!IsValidRetiredLedger(package.RetiredIdLedger))
        {
            Error(
                diagnostics,
                "specattest.interchange.retired-ledger.invalid",
                "The retired-ID ledger must be an object containing a retired_requirements array of IDs or requirement_id objects.",
                "retiredIdLedger");
        }

        HashSet<string> retiredIds = new(StringComparer.Ordinal);
        foreach (string retiredId in RetiredIds(package.RetiredIdLedger))
        {
            if (!retiredIds.Add(retiredId))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.retired-id.duplicate",
                    $"Retired ID '{retiredId}' appears more than once.",
                    "retiredIdLedger");
            }

            if (stableIds.Contains(retiredId))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.retired-id.active",
                    $"Retired ID '{retiredId}' is also active in the package.",
                    "retiredIdLedger");
            }
        }

        if (!package.Manifest.Validation.CanonicalProfilePassed)
        {
            Error(
                diagnostics,
                "specattest.interchange.validation.not-attested",
                "Package has not passed validation under its exact Standard pin and canonical profile.",
                "manifest.validation");
        }
        else if (package.Manifest.Validation.ValidatedAt is null
            || !Sha256().IsMatch(package.Manifest.Validation.FindingsSha256))
        {
            Error(
                diagnostics,
                "specattest.interchange.validation.coordinates.invalid",
                "Passing validation requires a validation time and findings digest.",
                "manifest.validation");
        }

        return diagnostics;
    }

    public static void ValidatePin(ResourceCompatibilityPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (!string.Equals(pin.Contract, SpecAttestInterchangeContract.StandardPinContract, StringComparison.Ordinal)
            || !string.Equals(pin.Repository, SpecAttestInterchangeContract.StandardRepository, StringComparison.Ordinal)
            || !string.Equals(pin.Edition, SpecAttestInterchangeContract.StandardEdition, StringComparison.Ordinal)
            || !string.Equals(pin.Revision, SpecAttestInterchangeContract.StandardCommit, StringComparison.Ordinal)
            || !string.Equals(pin.SchemaPath, SpecAttestInterchangeContract.StandardSchemaPath, StringComparison.Ordinal)
            || !string.Equals(pin.SchemaSha256, SpecAttestInterchangeContract.StandardSchemaSha256, StringComparison.Ordinal)
            || !string.Equals(pin.EditionManifestSha256, SpecAttestInterchangeContract.EditionManifestSha256, StringComparison.Ordinal)
            || !string.Equals(pin.EditionManifestSchemaPath, SpecAttestInterchangeContract.EditionManifestSchemaPath, StringComparison.Ordinal)
            || !string.Equals(pin.EditionManifestSchemaSha256, SpecAttestInterchangeContract.EditionManifestSchemaSha256, StringComparison.Ordinal)
            || !string.Equals(pin.PayloadSha256, SpecAttestInterchangeContract.StandardPayloadSha256, StringComparison.Ordinal)
            || !string.Equals(pin.ConformanceCorpusSha256, SpecAttestInterchangeContract.ConformanceCorpusSha256, StringComparison.Ordinal)
            || !string.Equals(pin.SignatureReference, SpecAttestInterchangeContract.SignatureReference, StringComparison.Ordinal)
            || !string.Equals(pin.SignatureSha256, SpecAttestInterchangeContract.SignatureSha256, StringComparison.Ordinal)
            || !string.Equals(pin.Canonicalization, SpecAttestInterchangeContract.Canonicalization, StringComparison.Ordinal)
            || pin.Profile is not ("core" or "traceable" or "auditable")
            || pin.CanonicalProfiles is null
            || !pin.CanonicalProfiles.SequenceEqual(["core", "traceable", "auditable"], StringComparer.Ordinal)
            || !string.Equals(pin.ProofProfile, SpecAttestInterchangeContract.ProofProfile, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Standard pin must exactly match immutable SpecAttest Standard edition 0.1.0-draft.1, including its manifest, schemas, payload, corpus, signed tag, canonicalization, and profiles.",
                nameof(pin));
        }
    }

    private static bool PinsEqual(ResourceCompatibilityPin left, ResourceCompatibilityPin right)
    {
        return string.Equals(left.Contract, right.Contract, StringComparison.Ordinal)
            && string.Equals(left.Repository, right.Repository, StringComparison.Ordinal)
            && string.Equals(left.Edition, right.Edition, StringComparison.Ordinal)
            && string.Equals(left.Revision, right.Revision, StringComparison.Ordinal)
            && string.Equals(left.SchemaPath, right.SchemaPath, StringComparison.Ordinal)
            && string.Equals(left.SchemaSha256, right.SchemaSha256, StringComparison.Ordinal)
            && string.Equals(left.Canonicalization, right.Canonicalization, StringComparison.Ordinal)
            && string.Equals(left.Profile, right.Profile, StringComparison.Ordinal)
            && string.Equals(left.EditionManifestSha256, right.EditionManifestSha256, StringComparison.Ordinal)
            && string.Equals(left.EditionManifestSchemaPath, right.EditionManifestSchemaPath, StringComparison.Ordinal)
            && string.Equals(left.EditionManifestSchemaSha256, right.EditionManifestSchemaSha256, StringComparison.Ordinal)
            && string.Equals(left.PayloadSha256, right.PayloadSha256, StringComparison.Ordinal)
            && string.Equals(left.ConformanceCorpusSha256, right.ConformanceCorpusSha256, StringComparison.Ordinal)
            && string.Equals(left.SignatureReference, right.SignatureReference, StringComparison.Ordinal)
            && string.Equals(left.SignatureSha256, right.SignatureSha256, StringComparison.Ordinal)
            && (left.CanonicalProfiles ?? []).SequenceEqual(right.CanonicalProfiles ?? [], StringComparer.Ordinal)
            && string.Equals(left.ProofProfile, right.ProofProfile, StringComparison.Ordinal);
    }

    private static void ValidateSource(
        SpecAttestInterchangeManifest manifest,
        ICollection<ProviderDiagnostic> diagnostics)
    {
        if (string.Equals(manifest.Source.System, "git", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(manifest.Source.Repository)
                || manifest.Source.Commit is null
                || !CommitSha().IsMatch(manifest.Source.Commit))
            {
                Error(
                    diagnostics,
                    "specattest.interchange.source.git-coordinate.invalid",
                    "Git sources require a repository and exact 40-hex commit.",
                    "manifest.source");
            }

            return;
        }

        if (string.Equals(manifest.Source.System, "manager", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(manifest.Source.ManagerProjectId)
                || string.IsNullOrWhiteSpace(manifest.Source.ManagerPackageVersion)
                || manifest.Source.ManagerRecordVersion is null or <= 0)
            {
                Error(
                    diagnostics,
                    "specattest.interchange.source.manager-coordinate.invalid",
                    "Manager sources require exact project, package, and record-version coordinates.",
                    "manifest.source");
            }

            return;
        }

        Error(
            diagnostics,
            "specattest.interchange.source.system.unsupported",
            $"Source system '{manifest.Source.System}' is unsupported.",
            "manifest.source.system");
    }

    private static IEnumerable<string> RetiredIds(JsonElement ledger)
    {
        if (ledger.ValueKind != JsonValueKind.Object
            || !ledger.TryGetProperty("retired_requirements", out JsonElement retired)
            || retired.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement item in retired.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                yield return item.GetString()!;
            }
            else if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("requirement_id", out JsonElement id)
                && id.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(id.GetString()))
            {
                yield return id.GetString()!;
            }
        }
    }

    internal static bool IsStableIdentifier(string value)
    {
        return StableId().IsMatch(value);
    }

    private static bool IsValidRetiredLedger(JsonElement ledger)
    {
        if (ledger.ValueKind != JsonValueKind.Object
            || !ledger.TryGetProperty("retired_requirements", out JsonElement retired)
            || retired.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement item in retired.EnumerateArray())
        {
            string? id = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("requirement_id", out JsonElement requirementId)
                && requirementId.ValueKind == JsonValueKind.String)
            {
                id = requirementId.GetString();
            }

            if (string.IsNullOrWhiteSpace(id) || !IsStableIdentifier(id))
            {
                return false;
            }
        }

        return true;
    }

    private static void Error(
        ICollection<ProviderDiagnostic> diagnostics,
        string code,
        string message,
        string target)
    {
        diagnostics.Add(new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, target));
    }

    [GeneratedRegex(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,254}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex StableId();

    [GeneratedRegex(
        "^[0-9a-fA-F]{40}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommitSha();

    [GeneratedRegex(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256();
}

#pragma warning restore MA0048
