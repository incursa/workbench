// JSON schema validation for config and front matter.
// Assumes canonical Spec Trace schemas live under specs/schemas/ and repo config schemas under schemas/.
#pragma warning disable S1144, ERP022
using System.Collections;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Workbench.Core;

public static class SchemaValidationService
{
    private const string PinnedCanonicalArtifactSchemaResourceName = "Workbench.Core.PinnedSchemas.SpecTrace.model.schema.json";
    private static readonly Lazy<JsonSchema> pinnedCanonicalArtifactSchema = new(LoadPinnedCanonicalArtifactSchema);

    public static IList<string> ValidateConfig(string repoRoot)
    {
        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        if (!File.Exists(configPath))
        {
            return new List<string>();
        }
        var schemaPath = Path.Combine(repoRoot, "schemas", "workbench-config.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"config: workbench config schema not found at {schemaPath}" };
        }
        return ValidateJsonAgainstSchema(configPath, schemaPath, "config");
    }

    public static IList<string> ValidateFrontMatter(string repoRoot, string itemPath, IDictionary<string, object?> data)
    {
        return ValidateArtifactFrontMatter(repoRoot, itemPath, data);
    }

    public static IList<string> ValidateArtifactFrontMatter(string repoRoot, string artifactPath, IDictionary<string, object?> data)
    {
        var schemaPath = Path.Combine(repoRoot, "specs", "schemas", "artifact-frontmatter.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"{artifactPath}: artifact front matter schema not found at {schemaPath}" };
        }
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, artifactPath, jsonIsContent: true);
    }

    public static IList<string> ValidateDocFrontMatter(string repoRoot, string docPath, IDictionary<string, object?> data)
    {
        return ValidateArtifactFrontMatter(repoRoot, docPath, data);
    }

    public static IList<string> ValidateRequirementClause(string repoRoot, string context, SpecTraceMarkdown.RequirementClause clause)
    {
        _ = repoRoot;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(clause.RequirementId))
        {
            errors.Add($"{context}: requirement_id is missing.");
        }

        if (string.IsNullOrWhiteSpace(clause.Title))
        {
            errors.Add($"{context}: requirement title is missing.");
        }

        if (string.IsNullOrWhiteSpace(clause.Clause))
        {
            errors.Add($"{context}: requirement clause is missing.");
        }

        if (!string.IsNullOrWhiteSpace(clause.NormativeKeyword) &&
            !new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MUST",
                "MUST NOT",
                "SHALL",
                "SHALL NOT",
                "SHOULD",
                "SHOULD NOT",
                "MAY"
            }.Contains(clause.NormativeKeyword))
        {
            errors.Add($"{context}: requirement clause uses an unsupported normative keyword '{clause.NormativeKeyword}'.");
        }

        return errors;
    }

    public static IList<string> ValidateRequirementTraceFields(string repoRoot, string context, IDictionary<string, object?> trace)
    {
        _ = repoRoot;
        var errors = new List<string>();
        var allowedLabels = SpecTraceMarkdown.CanonicalRequirementTraceLabels;

        foreach (var entry in trace)
        {
            if (!allowedLabels.Contains(entry.Key))
            {
                errors.Add($"{context}: trace label '{entry.Key}' is not canonical.");
                continue;
            }

            if (entry.Value is null)
            {
                errors.Add($"{context}: trace label '{entry.Key}' is empty.");
                continue;
            }

            if (entry.Value is string)
            {
                errors.Add($"{context}: trace label '{entry.Key}' must be an array of strings.");
                continue;
            }

            if (entry.Value is not IEnumerable enumerable)
            {
                errors.Add($"{context}: trace label '{entry.Key}' must be an array of strings.");
                continue;
            }

            var values = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is not string text || string.IsNullOrWhiteSpace(text))
                {
                    errors.Add($"{context}: trace label '{entry.Key}' contains an invalid value.");
                    continue;
                }

                values.Add(text);
            }

            if (values.Count == 0)
            {
                errors.Add($"{context}: trace label '{entry.Key}' must contain at least one value.");
            }
            else if (values.Count != values.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                errors.Add($"{context}: trace label '{entry.Key}' contains duplicate values.");
            }
        }

        return errors;
    }

    public static IList<string> ValidateWorkItemTraceFields(string repoRoot, string context, IDictionary<string, object?> trace)
    {
        var schemaPath = Path.Combine(repoRoot, "specs", "schemas", "work-item-trace-fields.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string>();
        }
        var json = JsonWriter.Serialize(trace, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, context, jsonIsContent: true);
    }

    public static IList<string> ValidateCanonicalArtifactJson(string repoRoot, string artifactPath)
    {
        return ValidateCanonicalArtifactJson(repoRoot, artifactPath, json: null);
    }

    public static IList<string> ValidateCanonicalArtifactJson(string repoRoot, string artifactPath, string? json)
    {
        _ = repoRoot;

        var jsonText = json ?? File.ReadAllText(artifactPath);
        var normalizedJson = NormalizeCanonicalArtifactJson(jsonText);

        return ValidateJsonAgainstSchema(
            normalizedJson,
            () => pinnedCanonicalArtifactSchema.Value,
            artifactPath,
            jsonIsContent: true);
    }

    public static IList<string> ValidateJsonContent(string repoRoot, string schemaRelativePath, string context, string json)
    {
        var schemaPath = Path.IsPathRooted(schemaRelativePath)
            ? schemaRelativePath
            : Path.Combine(repoRoot, schemaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return ValidateJsonAgainstSchema(json, schemaPath, context, jsonIsContent: true);
    }

    private static List<string> ValidateJsonAgainstSchema(
        string jsonOrPath,
        string schemaPath,
        string context,
        bool jsonIsContent = false)
    {
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"{context}: schema not found at {schemaPath}" };
        }

        return ValidateJsonAgainstSchema(jsonOrPath, () =>
        {
            var schemaText = File.ReadAllText(schemaPath);
            return JsonSchema.FromText(schemaText);
        }, context, jsonIsContent);
    }

    private static List<string> ValidateJsonAgainstSchema(
        string jsonOrPath,
        Func<JsonSchema> schemaFactory,
        string context,
        bool jsonIsContent = false)
    {
        var errors = new List<string>();

        try
        {
            var schema = schemaFactory();
            var jsonText = jsonIsContent ? jsonOrPath : File.ReadAllText(jsonOrPath);
            using var doc = JsonDocument.Parse(jsonText);
            var result = schema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.Hierarchical
            });

            if (!result.IsValid)
            {
                CollectErrors(result, errors, context);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{context}: schema validation error: {ex}");
        }

        return errors;
    }

    private static JsonSchema LoadPinnedCanonicalArtifactSchema()
    {
        var assembly = typeof(SchemaValidationService).Assembly;
        using var stream = assembly.GetManifestResourceStream(PinnedCanonicalArtifactSchemaResourceName)
            ?? throw new InvalidOperationException($"Pinned canonical artifact schema resource '{PinnedCanonicalArtifactSchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var schema = JsonNode.Parse(reader.ReadToEnd()) as JsonObject
            ?? throw new InvalidOperationException($"Pinned canonical artifact schema resource '{PinnedCanonicalArtifactSchemaResourceName}' did not contain a JSON object.");

        ApplyPinnedSchemaCompatibility(schema);
        return JsonSchema.FromText(schema.ToJsonString());
    }

    internal static string NormalizeCanonicalArtifactJson(string json)
    {
        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            if (root is null)
            {
                return json;
            }

            if (!TryNormalizeLandedStatus(root))
            {
                return json;
            }

            return root.ToJsonString();
        }
        catch
        {
            return json;
        }
    }

    private static void ApplyPinnedSchemaCompatibility(JsonObject schema)
    {
        if (schema["$defs"] is not JsonObject defs)
        {
            return;
        }

        if (!defs.ContainsKey("coverageStatus"))
        {
            defs["coverageStatus"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray
                {
                    "required",
                    "optional",
                    "not_applicable",
                    "deferred"
                }
            };
        }

        if (!defs.ContainsKey("requirementCoverage"))
        {
            defs["requirementCoverage"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "Expected coverage dimensions for a requirement. This is authored expectation metadata, not actual test, code, or runtime evidence.",
                ["required"] = new JsonArray
                {
                    "positive",
                    "negative",
                    "edge",
                    "fuzz"
                },
                ["properties"] = new JsonObject
                {
                    ["positive"] = new JsonObject { ["$ref"] = "#/$defs/coverageStatus" },
                    ["negative"] = new JsonObject { ["$ref"] = "#/$defs/coverageStatus" },
                    ["edge"] = new JsonObject { ["$ref"] = "#/$defs/coverageStatus" },
                    ["fuzz"] = new JsonObject { ["$ref"] = "#/$defs/coverageStatus" }
                },
                ["patternProperties"] = new JsonObject
                {
                    ["^x_[A-Za-z0-9_]+$"] = new JsonObject()
                },
                ["additionalProperties"] = false
            };
        }

        if (defs["requirement"] is JsonObject requirement &&
            requirement["properties"] is JsonObject properties &&
            !properties.ContainsKey("coverage"))
        {
            properties["coverage"] = new JsonObject
            {
                ["$ref"] = "#/$defs/requirementCoverage"
            };
        }
    }

    private static bool TryNormalizeLandedStatus(JsonObject artifact)
    {
        if (!artifact.TryGetPropertyValue("status", out var statusNode))
        {
            return false;
        }

        string? status;
        try
        {
            status = statusNode?.GetValue<string>();
        }
        catch
        {
            return false;
        }

        if (!string.Equals(status, "landed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var artifactType = artifact.TryGetPropertyValue("artifact_type", out var artifactTypeNode)
            ? artifactTypeNode?.GetValue<string>()
            : null;

        var normalizedStatus = artifactType?.ToLowerInvariant() switch
        {
            "verification" => "passed",
            "work_item" => "complete",
            "architecture" => "implemented",
            "specification" => "implemented",
            _ => "implemented"
        };

        artifact["status"] = normalizedStatus;
        return true;
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors, string context)
    {
        if (results.Errors is not null)
        {
            foreach (var error in results.Errors)
            {
                var location = results.InstanceLocation.ToString();
                errors.Add($"{context}: {location} {error.Value}");
            }
        }

        foreach (var detail in results.Details)
        {
            CollectErrors(detail, errors, context);
        }
    }

}
