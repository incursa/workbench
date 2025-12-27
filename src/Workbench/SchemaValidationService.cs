using System.Text.Json;
using Json.Schema;

namespace Workbench;

public static class SchemaValidationService
{
    public static IList<string> ValidateConfig(string repoRoot)
    {
        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        if (!File.Exists(configPath))
        {
            return new List<string>();
        }
        var schemaPath = Path.Combine(repoRoot, "docs", "30-contracts", "workbench-config.schema.json");
        return ValidateJsonAgainstSchema(configPath, schemaPath, "config");
    }

    public static IList<string> ValidateFrontMatter(string repoRoot, string itemPath, IDictionary<string, object?> data)
    {
        var schemaPath = Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json");
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, itemPath, jsonIsContent: true);
    }

    public static IList<string> ValidateDocFrontMatter(string repoRoot, string docPath, IDictionary<string, object?> data)
    {
        var schemaPath = Path.Combine(repoRoot, "docs", "30-contracts", "doc.schema.json");
        var json = JsonWriter.Serialize(data, indented: false);
        return ValidateJsonAgainstSchema(json, schemaPath, docPath, jsonIsContent: true);
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
}
