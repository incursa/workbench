using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Workbench.SpecAttestProvider;

#pragma warning disable MA0002 // Protocol comparisons deliberately use exact ordinal collections.
#pragma warning disable MA0006 // Protocol literals use exact ordinal equality throughout.
#pragma warning disable MA0009 // Generated, anchored protocol regexes operate on small JSON fields.
#pragma warning disable MA0048 // The report is the result DTO for this runner.
#pragma warning disable S3358 // The maturity mapping mirrors the normative three-way rule.

public sealed record SpecAttestConformanceReport(string Result, int Cases);

public static partial class SpecAttestConformanceCorpusRunner
{
    private static readonly HashSet<string> pinFields =
    [
        "contract", "repository", "edition", "commit", "schemaPath", "schemaSha256",
        "editionManifestSha256", "editionManifestSchemaPath", "editionManifestSchemaSha256",
        "payloadSha256", "conformanceCorpusSha256", "signatureReference", "signatureSha256",
        "canonicalization", "canonicalProfiles", "proofProfile"
    ];

    private static readonly string[] pinHashFields =
    [
        "schemaSha256", "editionManifestSha256", "editionManifestSchemaSha256",
        "payloadSha256", "conformanceCorpusSha256", "signatureSha256"
    ];

    public static SpecAttestConformanceReport Run(string corpusDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusDirectory);
        string root = Path.GetFullPath(corpusDirectory);
        string[] paths = Directory.Exists(root)
            ? Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
            : throw new DirectoryNotFoundException($"Conformance corpus was not found: {root}");
        if (paths.Length == 0)
        {
            throw new InvalidOperationException("The versioning conformance corpus is empty.");
        }

        foreach (string path in paths)
        {
            JsonObject vector = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new JsonException($"Conformance vector is not a JSON object: {path}");
            bool actual = Validate(vector);
            bool expected = vector["valid"]?.GetValue<bool>()
                ?? throw new JsonException($"Conformance vector has no boolean 'valid' field: {path}");
            if (actual != expected)
            {
                throw new InvalidOperationException($"Conformance vector produced the wrong result: {Path.GetRelativePath(root, path)}");
            }
        }

        return new SpecAttestConformanceReport("passed", paths.Length);
    }

    public static bool Validate(JsonObject vector)
    {
        return Text(vector, "kind") switch
        {
            null or "versioning" => ValidateVersioning(vector),
            "edition-pin-mutation" => ValidatePinMutation(vector),
            "interchange-roundtrip" => ValidateInterchange(vector),
            "safe-mutation-protocol" => ValidateProtocol(vector),
            string kind => throw new InvalidOperationException($"Unsupported conformance kind: {kind}")
        };
    }

    private static bool ValidateVersioning(JsonObject vector)
    {
        Match match = SemVerRegex().Match(Text(vector, "edition") ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        string prerelease = match.Groups["prerelease"].Value;
        string expectedMaturity = prerelease == "draft" ? "draft" : prerelease == "rc" ? "candidate" : "stable";
        JsonObject? requirement = vector["requirement"] as JsonObject;
        bool valid = Text(vector, "maturity") == expectedMaturity
            && PositiveInteger(requirement, "revision") is not null;
        if (vector["bootstrap"]?.GetValue<bool>() != true)
        {
            valid = valid
                && !string.IsNullOrWhiteSpace(Text(vector, "previousEdition"))
                && !string.IsNullOrWhiteSpace(Text(vector, "behavioralReview"));
        }

        return valid;
    }

    private static bool ValidatePinMutation(JsonObject vector)
    {
        if (vector["trustedPin"] is not JsonObject trusted || vector["mutation"] is not JsonObject mutation || !PinShapeValid(trusted))
        {
            return false;
        }

        JsonObject candidate = trusted.DeepClone().AsObject();
        string? field = Text(mutation, "field");
        if (field is null || !pinFields.Contains(field))
        {
            return false;
        }

        switch (Text(mutation, "operation"))
        {
            case "remove":
                candidate.Remove(field);
                break;
            case "replace":
                candidate[field] = mutation["value"]?.DeepClone();
                break;
            default:
                return false;
        }

        return PinShapeValid(candidate) && CanonicalEqual(candidate, trusted);
    }

    private static bool ValidateInterchange(JsonObject vector)
    {
        if (vector["inputRequirement"] is not JsonObject input
            || vector["outputRequirement"] is not JsonObject output
            || PositiveInteger(vector, "providerRecordVersion") is not long providerRecordVersion
            || PositiveInteger(input, "revision") is not long revision)
        {
            return false;
        }

        bool preserved = new[] { "id", "revision", "trace" }
            .All(field => JsonNode.DeepEquals(input[field], output[field]));
        bool canonicalEqual = vector["canonicalA"] is null && vector["canonicalB"] is null
            || CanonicalEqual(vector["canonicalA"], vector["canonicalB"]);
        return preserved && revision != providerRecordVersion && canonicalEqual;
    }

    private static bool ValidateProtocol(JsonObject vector)
    {
        string? rest = Text(vector, "restOutcome");
        if (!string.Equals(rest, Text(vector, "mcpOutcome"), StringComparison.Ordinal))
        {
            return false;
        }

        return Text(vector, "scenario") switch
        {
            "stale-commit" or "atomic-failure" => rest == "rejected" && vector["partialMutation"]?.GetValue<bool>() == false,
            "deterministic-replay" => rest == "staged"
                && vector["serverDefaultsExcluded"]?.GetValue<bool>() == true
                && CanonicalEqual(vector["logicalRequest"], vector["replayedLogicalRequest"]),
            "cancel" => rest == "canceled" && vector["partialMutation"]?.GetValue<bool>() == false,
            "resume" => rest == "committed" && vector["atomicCommit"]?.GetValue<bool>() == true,
            _ => false
        };
    }

    private static bool PinShapeValid(JsonObject pin)
    {
        return pin.Select(item => item.Key).ToHashSet(StringComparer.Ordinal).SetEquals(pinFields)
            && Text(pin, "contract") == "specattest.standard-pin.v1"
            && Uri.TryCreate(Text(pin, "repository"), UriKind.Absolute, out Uri? repository) && repository.Scheme == Uri.UriSchemeHttps
            && SemVerRegex().IsMatch(Text(pin, "edition") ?? string.Empty)
            && CommitRegex().IsMatch(Text(pin, "commit") ?? string.Empty)
            && pinHashFields.All(field => Sha256Regex().IsMatch(Text(pin, field) ?? string.Empty))
            && TagRegex().IsMatch(Text(pin, "signatureReference") ?? string.Empty)
            && Text(pin, "canonicalization") == SpecAttestInterchangeContract.Canonicalization
            && pin["canonicalProfiles"] is JsonArray profiles
            && profiles.Select(item => item?.GetValue<string>()).SequenceEqual(["core", "traceable", "auditable"])
            && Text(pin, "proofProfile") is "core" or "traceable" or "auditable"
            && !string.IsNullOrWhiteSpace(Text(pin, "schemaPath"))
            && !string.IsNullOrWhiteSpace(Text(pin, "editionManifestSchemaPath"));
    }

    private static bool CanonicalEqual(JsonNode? left, JsonNode? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        byte[] leftCanonical = SpecAttestCanonicalJson.Serialize(JsonSerializer.SerializeToElement(left));
        byte[] rightCanonical = SpecAttestCanonicalJson.Serialize(JsonSerializer.SerializeToElement(right));
        return leftCanonical.AsSpan().SequenceEqual(rightCanonical);
    }

    private static string? Text(JsonObject? value, string propertyName) => value?[propertyName]?.GetValue<string>();

    private static long? PositiveInteger(JsonObject? value, string propertyName)
    {
        if (value?[propertyName] is not JsonValue node || !node.TryGetValue(out long result) || result < 1)
        {
            return null;
        }

        return result;
    }

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-(?<prerelease>draft|rc)\\.[1-9][0-9]*)?$")]
    private static partial Regex SemVerRegex();

    [GeneratedRegex("^[a-f0-9]{40}$")]
    private static partial Regex CommitRegex();

    [GeneratedRegex("^[a-f0-9]{64}$")]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^refs/tags/[A-Za-z0-9._/-]+$")]
    private static partial Regex TagRegex();
}
