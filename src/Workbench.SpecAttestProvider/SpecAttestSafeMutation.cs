using System.Text.Json;
using System.Text.Json.Nodes;
using Workbench.Core;

namespace Workbench.SpecAttestProvider;

#pragma warning disable MA0048 // The stored plan is private to the safe-mutation codec boundary.

internal static class SpecAttestSafeMutation
{
    public static JsonElement BuildStageRequest(
        SpecAttestInterchangePackage desired,
        JsonElement currentIntake,
        ResourceCompatibilityPin standardPin,
        string sourceDigest)
    {
        JsonObject package = JsonNode.Parse(currentIntake.GetRawText())?.AsObject()
            ?? throw new JsonException("Manager intake export is not an object.");
        long expectedProjectVersion = RequiredPositiveInt64(package, "expectedProjectVersion");
        JsonArray requirements = [];
        JsonArray currentRequirements = package["requirements"]?.AsArray()
            ?? throw new JsonException("Manager intake export is missing requirements.");

        foreach (SpecAttestInterchangeEntry entry in desired.Entries
                     .Where(static entry => string.Equals(entry.EntryKind, "requirement", StringComparison.Ordinal))
                     .OrderBy(static entry => entry.StableId, StringComparer.Ordinal))
        {
            JsonObject canonical = JsonNode.Parse(entry.CanonicalContent.GetRawText())?.AsObject()
                ?? throw new JsonException($"Requirement '{entry.StableId}' is not an object.");
            string id = RequiredText(canonical, "id");
            long revision = RequiredPositiveInt64(canonical, "revision");
            JsonObject record = FindCurrent(currentRequirements, id)?.DeepClone().AsObject() ?? [];
            record["requirementId"] = id;
            record["revision"] = revision;
            record["title"] = RequiredText(canonical, "title");
            record["statement"] = RequiredText(canonical, "statement");
            record["canonicalJson"] = canonical.DeepClone();
            record["trace"] = ManagerTrace(canonical["trace"] as JsonObject);
            requirements.Add(record);
        }

        package["packageVersion"] = "spec-trace-manager.intake.v1";
        package["standardPin"] = SpecAttestInterchangeContract.ManagerPin(standardPin);
        package["authorityMode"] = "manager";
        package["transition"] = "manager-apply";
        package["expectedProjectVersion"] = expectedProjectVersion;
        package["batchId"] = "";
        package["changeClassification"] = "auto";
        package["requirements"] = requirements;
        package["sourceDocuments"] = new JsonArray();
        package["requirementGroups"] = new JsonArray();
        package["sourcePackage"] = new JsonObject
        {
            ["name"] = "workbench-specattest-interchange",
            ["hash"] = sourceDigest,
            ["provenance"] = new JsonObject
            {
                ["transferId"] = desired.Manifest.TransferId,
                ["producer"] = desired.Manifest.Producer.Name
            }
        };

        JsonObject stage = new()
        {
            ["contract"] = SpecAttestInterchangeContract.SafeMutationContract,
            ["standardPin"] = SpecAttestInterchangeContract.ManagerPin(standardPin),
            ["authorityMode"] = "manager",
            ["transition"] = "manager-apply",
            ["expectedProjectVersion"] = expectedProjectVersion,
            ["package"] = package
        };
        return JsonSerializer.SerializeToElement(stage);
    }

    public static JsonElement WithIdempotencyKey(JsonElement stageRequest, string idempotencyKey)
    {
        JsonObject stage = JsonNode.Parse(stageRequest.GetRawText())?.AsObject()
            ?? throw new JsonException("Safe-mutation stage request is not an object.");
        JsonObject package = stage["package"]?.AsObject()
            ?? throw new JsonException("Safe-mutation stage request is missing its package.");
        package["batchId"] = idempotencyKey;
        return JsonSerializer.SerializeToElement(stage);
    }

    public static long ExpectedProjectVersion(JsonElement stageRequest)
        => RequiredPositiveInt64(stageRequest, "expectedProjectVersion");

    private static JsonObject ManagerTrace(JsonObject? canonicalTrace)
    {
        canonicalTrace ??= [];
        return new JsonObject
        {
            ["satisfiedBy"] = CopyArray(canonicalTrace, "satisfied_by"),
            ["implementedBy"] = CopyArray(canonicalTrace, "implemented_by"),
            ["verifiedBy"] = CopyArray(canonicalTrace, "verified_by"),
            ["derivedFrom"] = CopyArray(canonicalTrace, "derived_from"),
            ["supersedes"] = CopyArray(canonicalTrace, "supersedes"),
            ["upstreamRefs"] = CopyArray(canonicalTrace, "upstream_refs"),
            ["related"] = CopyArray(canonicalTrace, "related")
        };
    }

    private static JsonArray CopyArray(JsonObject value, string name)
        => value[name] is JsonArray array
            ? new JsonArray(array.Select(static item => item?.DeepClone()).ToArray())
            : [];

    private static JsonObject? FindCurrent(JsonArray current, string id)
        => current.OfType<JsonObject>().FirstOrDefault(item => string.Equals(
            item["requirementId"]?.GetValue<string>(),
            id,
            StringComparison.Ordinal));

    private static string RequiredText(JsonObject value, string name)
        => value[name]?.GetValue<string>() is { Length: > 0 } text
            ? text
            : throw new JsonException($"SpecAttest content is missing string '{name}'.");

    private static long RequiredPositiveInt64(JsonObject value, string name)
        => value[name]?.GetValue<long>() is > 0 and var number
            ? number
            : throw new JsonException($"SpecAttest content is missing positive integer '{name}'.");

    private static long RequiredPositiveInt64(JsonElement value, string name)
        => value.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt64(out long number)
            && number > 0
            ? number
            : throw new JsonException($"SpecAttest content is missing positive integer '{name}'.");
}

internal sealed record SpecAttestStoredPlan(
    string PlanId,
    string PlanDigest,
    string AuthorityEpoch,
    DateTimeOffset CreatedAtUtc,
    ResourceChange Change,
    JsonElement StageRequest);

#pragma warning restore MA0048
