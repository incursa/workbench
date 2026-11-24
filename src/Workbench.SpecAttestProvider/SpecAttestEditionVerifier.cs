using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Workbench.Core;

namespace Workbench.SpecAttestProvider;

#pragma warning disable MA0006 // Edition tuple validation deliberately uses exact ordinal equality.
#pragma warning disable MA0009 // Generated, anchored protocol regexes operate on small JSON fields.
#pragma warning disable MA0048 // The report is the result DTO for this verifier.

public sealed record SpecAttestEditionVerificationReport(
    string Result,
    string Edition,
    int Specifications,
    int Requirements,
    int ConformanceCases,
    string ValidationSubjectSha256);

public static partial class SpecAttestEditionVerifier
{
    public static SpecAttestEditionVerificationReport Verify(
        string repositoryRoot,
        string manifestPath,
        ResourceCompatibilityPin? expectedPin = null)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string fullManifestPath = Path.GetFullPath(manifestPath);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(fullManifestPath))?.AsObject()
            ?? throw new JsonException("Edition manifest must be a JSON object.");
        string schemaPath = Path.Combine(root, "schemas", "standard-edition.schema.json");
        string modelSchemaPath = Path.Combine(root, "model", "model.schema.json");

        ValidateSchema(schemaPath, manifest);
        Require(Text(manifest, "format") == "specattest.standard-edition.v1", "unsupported manifest format");
        string edition = RequiredText(manifest, "edition");
        Match editionMatch = EditionRegex().Match(edition);
        Require(editionMatch.Success, "edition is not an allowed Semantic Versioning label");
        string expectedMaturity = editionMatch.Groups["prerelease"].Value switch
        {
            "draft" => "draft",
            "rc" => "candidate",
            _ => "stable"
        };
        Require(Text(manifest, "maturity") == expectedMaturity, "edition prerelease and maturity disagree");
        Require(Text(manifest, "canonicalization") == SpecAttestInterchangeContract.Canonicalization, "unsupported canonicalization profile");
        Require(HashFile(schemaPath) == Text(manifest, "schemaSha256"), "schemaSha256 does not match schemas/standard-edition.schema.json");
        Require(HashFile(modelSchemaPath) == Text(manifest, "modelSchemaSha256"), "modelSchemaSha256 does not match model/model.schema.json");

        if (expectedPin is not null)
        {
            ValidatePin(manifest, fullManifestPath, expectedPin);
        }

        JsonObject source = manifest["source"]?.AsObject() ?? throw new JsonException("Edition manifest has no source object.");
        string sourcePath = RequiredText(source, "path").Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        JsonArray order = manifest["specificationOrder"]?.AsArray() ?? throw new JsonException("Edition manifest has no specificationOrder.");
        JsonArray declaredRevisions = manifest["requirementRevisions"]?.AsArray() ?? throw new JsonException("Edition manifest has no requirementRevisions.");
        Dictionary<(string Id, long Revision), string> declared = [];
        foreach (JsonNode? item in declaredRevisions)
        {
            JsonObject coordinate = item?.AsObject() ?? throw new JsonException("Requirement revision coordinate must be an object.");
            var key = (RequiredText(coordinate, "id"), RequiredPositiveInteger(coordinate, "revision"));
            Require(declared.TryAdd(key, RequiredText(coordinate, "sha256")), $"requirement revision coordinate is duplicated: {key}");
        }

        Dictionary<(string Id, long Revision), string> discovered = [];
        using IncrementalHash payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (JsonNode? item in order)
        {
            string specificationId = item?.GetValue<string>() ?? throw new JsonException("Specification ID must be a string.");
            string path = Path.Combine(root, sourcePath, specificationId + ".json");
            byte[] raw = File.ReadAllBytes(path);
            Append(payloadHash, Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'));
            payloadHash.AppendData([0]);
            payloadHash.AppendData(raw);
            payloadHash.AppendData([0]);

            JsonObject artifact = JsonNode.Parse(raw)?.AsObject() ?? throw new JsonException($"Specification is not a JSON object: {path}");
            Require(Text(artifact, "artifact_id") == specificationId, $"artifact identity mismatch for {path}");
            foreach (JsonNode? requirementNode in artifact["requirements"]?.AsArray() ?? [])
            {
                JsonObject requirement = requirementNode?.AsObject() ?? throw new JsonException($"Requirement is not an object in {path}");
                string id = RequiredText(requirement, "id");
                long revision = RequiredPositiveInteger(requirement, "revision");
                Require(RequirementIdRegex().IsMatch(id), $"invalid requirement revision in {path}: {id}");
                Require(discovered.TryAdd((id, revision), SpecAttestCanonicalJson.Sha256(JsonSerializer.SerializeToElement(requirement))),
                    $"duplicate requirement coordinate: ({id}, {revision})");
            }
        }

        var missing = discovered.Keys.Except(declared.Keys).ToArray();
        var unexpected = declared.Keys.Except(discovered.Keys).ToArray();
        var mismatched = discovered.Keys
            .Intersect(declared.Keys)
            .Where(key => !string.Equals(discovered[key], declared[key], StringComparison.Ordinal))
            .ToArray();
        Require(missing.Length == 0 && unexpected.Length == 0 && mismatched.Length == 0,
            $"requirement revision manifest mismatch; missing={string.Join(',', missing)}, unexpected={string.Join(',', unexpected)}, digest={string.Join(',', mismatched)}");
        Require(Convert.ToHexString(payloadHash.GetHashAndReset()).ToLowerInvariant() == Text(manifest, "payloadSha256"),
            "payloadSha256 does not match the ordered canonical suite");

        JsonArray validators = manifest["validatedBy"]?.AsArray() ?? throw new JsonException("Edition manifest has no validators.");
        Require(validators.Count >= 2, "at least two validator records are required");
        HashSet<string?> roles = validators.Select(item => Text(item?.AsObject(), "role")).ToHashSet(StringComparer.Ordinal);
        bool bootstrap = manifest["bootstrap"]?.GetValue<bool>() == true;
        string[] expectedRoles = bootstrap || expectedMaturity == "draft" ? ["independent", "candidate"] : ["accepted", "candidate"];
        Require(expectedRoles.All(roles.Contains), $"validator roles must include {string.Join(", ", expectedRoles)}");
        foreach (JsonNode? item in validators)
        {
            JsonObject validator = item?.AsObject() ?? throw new JsonException("Validator record must be an object.");
            Require(Text(validator, "standardEdition") == edition, "validator standardEdition does not match the manifest edition");
            Require(Text(validator, "conformanceCorpusSha256") == Text(manifest, "conformanceCorpusSha256"),
                "validator conformance corpus digest does not match the manifest");
        }

        Require(!string.IsNullOrWhiteSpace(Text(manifest, "approvalReference")) && manifest["signature"] is JsonObject,
            "approval and signature references are required");
        VerifyCorpusArchiveDigest(root, RequiredText(source, "commit"), Text(manifest, "conformanceCorpusSha256"));
        SpecAttestConformanceReport corpus = SpecAttestConformanceCorpusRunner.Run(Path.Combine(root, "conformance", "versioning"));

        JsonObject subject = manifest.DeepClone().AsObject();
        subject.Remove("validatedBy");
        subject.Remove("approvalReference");
        subject.Remove("signature");
        return new SpecAttestEditionVerificationReport(
            "passed",
            edition,
            order.Count,
            discovered.Count,
            corpus.Cases,
            SpecAttestCanonicalJson.Sha256(JsonSerializer.SerializeToElement(subject)));
    }

    private static void ValidateSchema(string schemaPath, JsonObject manifest)
    {
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        EvaluationResults result = schema.Evaluate(JsonSerializer.SerializeToElement(manifest), new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        Require(result.IsValid, "edition manifest does not satisfy schemas/standard-edition.schema.json");
    }

    private static void ValidatePin(JsonObject manifest, string manifestPath, ResourceCompatibilityPin pin)
    {
        JsonObject source = manifest["source"]?.AsObject() ?? throw new JsonException("Edition manifest has no source object.");
        Require(Text(manifest, "edition") == pin.Edition, "manifest edition does not match the trusted pin");
        Require(Text(source, "commit") == pin.Revision, "manifest source commit does not match the trusted pin");
        Require(Text(manifest, "modelSchemaSha256") == pin.SchemaSha256, "manifest model schema digest does not match the trusted pin");
        Require(HashFile(manifestPath) == pin.EditionManifestSha256, "edition manifest bytes do not match the trusted pin");
        Require(Text(manifest, "schemaSha256") == pin.EditionManifestSchemaSha256, "manifest schema digest does not match the trusted pin");
        Require(Text(manifest, "payloadSha256") == pin.PayloadSha256, "payload digest does not match the trusted pin");
        Require(Text(manifest, "conformanceCorpusSha256") == pin.ConformanceCorpusSha256, "corpus digest does not match the trusted pin");
        JsonObject signature = manifest["signature"]?.AsObject() ?? throw new JsonException("Edition manifest has no signature object.");
        Require(Text(signature, "reference") == pin.SignatureReference && Text(signature, "sha256") == pin.SignatureSha256,
            "signature tuple does not match the trusted pin");
    }

    private static void VerifyCorpusArchiveDigest(string root, string commit, string? expectedDigest)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("archive");
        start.ArgumentList.Add("--format=tar");
        start.ArgumentList.Add(commit);
        start.ArgumentList.Add("conformance/versioning");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git to verify the corpus archive.");
        using SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(process.StandardOutput.BaseStream);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Require(process.ExitCode == 0, $"git could not archive the conformance corpus: {error.Trim()}");
        Require(Convert.ToHexString(digest).ToLowerInvariant() == expectedDigest, "conformanceCorpusSha256 does not match the source commit archive");
    }

    private static void Append(IncrementalHash hash, string value) => hash.AppendData(Encoding.UTF8.GetBytes(value));
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string? Text(JsonObject? value, string propertyName) => value?[propertyName]?.GetValue<string>();
    private static string RequiredText(JsonObject value, string propertyName) => Text(value, propertyName)
        ?? throw new JsonException($"Required string '{propertyName}' is missing.");
    private static long RequiredPositiveInteger(JsonObject value, string propertyName)
    {
        if (value[propertyName] is JsonValue node && node.TryGetValue(out long result) && result > 0)
        {
            return result;
        }

        throw new JsonException($"Required positive integer '{propertyName}' is missing.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-(?<prerelease>draft|rc)\\.[1-9][0-9]*)?$")]
    private static partial Regex EditionRegex();

    [GeneratedRegex("^REQ-[A-Z][A-Z0-9]*(?:-[A-Z][A-Z0-9]*)*-[0-9]{4,}$")]
    private static partial Regex RequirementIdRegex();
}
