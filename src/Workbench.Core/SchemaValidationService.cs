// JSON schema validation for config and front matter.
// Assumes schemas live under schemas/ in the repo.
using System.Collections;
using Json.Schema;

namespace Workbench.Core;

public static class SchemaValidationService
{
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
        var artifactType = GetString(data, "artifact_type");
        var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
        var isCanonical = string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase) ||
                          (!string.IsNullOrWhiteSpace(artifactId) && artifactId.StartsWith("WI-", StringComparison.OrdinalIgnoreCase));
        var schemaPath = isCanonical
            ? Path.Combine(repoRoot, "schemas", "artifact-frontmatter.schema.json")
            : Path.Combine(repoRoot, "schemas", "work-item.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"{itemPath}: work item schema not found at {schemaPath}" };
        }
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, itemPath, jsonIsContent: true);
    }

    public static IList<string> ValidateArtifactFrontMatter(string repoRoot, string artifactPath, IDictionary<string, object?> data)
    {
        var schemaPath = Path.Combine(repoRoot, "schemas", "artifact-frontmatter.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"{artifactPath}: artifact front matter schema not found at {schemaPath}" };
        }
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, artifactPath, jsonIsContent: true);
    }

    public static IList<string> ValidateDocFrontMatter(string repoRoot, string docPath, IDictionary<string, object?> data)
    {
        var schemaPath = Path.Combine(repoRoot, "schemas", "doc.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string> { $"{docPath}: doc schema not found at {schemaPath}" };
        }
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, docPath, jsonIsContent: true);
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
        var schemaPath = Path.Combine(repoRoot, "schemas", "work-item-trace-fields.schema.json");
        if (!File.Exists(schemaPath))
        {
            return new List<string>();
        }
        var json = JsonWriter.Serialize(trace, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, context, jsonIsContent: true);
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
        var errors = new List<string>();
        if (!File.Exists(schemaPath))
        {
            errors.Add($"{context}: schema not found at {schemaPath}");
            return errors;
        }

        try
        {
            var schemaText = File.ReadAllText(schemaPath);
            var schema = JsonSchema.FromText(schemaText);
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

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }
}
