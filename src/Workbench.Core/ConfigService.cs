namespace Workbench;

public static class ConfigService
{
    public static WorkbenchConfig SetConfigValue(
        WorkbenchConfig config,
        string path,
        string rawValue,
        bool parseJson,
        out bool changed)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Config path is required.");
        }

        var node = JsonSerializer.SerializeToNode(config, WorkbenchJsonContext.Default.WorkbenchConfig) as JsonObject
            ?? throw new InvalidOperationException("Failed to serialize config.");

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Config path is required.");
        }

        var current = node;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            if (!current.TryGetPropertyValue(segment, out var next) || next is null)
            {
                throw new InvalidOperationException($"Unknown config path segment: {segment}");
            }

            if (next is not JsonObject nextObject)
            {
                throw new InvalidOperationException($"Config path segment is not an object: {segment}");
            }

            current = nextObject;
        }

        var leaf = segments[^1];
        if (!current.TryGetPropertyValue(leaf, out var existing))
        {
            throw new InvalidOperationException($"Unknown config path segment: {leaf}");
        }

        var valueNode = ParseValue(rawValue, parseJson);
        if (existing is JsonObject && valueNode is not JsonObject)
        {
            throw new InvalidOperationException($"Config path {path} expects an object value.");
        }

        changed = !JsonNode.DeepEquals(existing, valueNode);
        if (!changed)
        {
            return config;
        }

        current[leaf] = valueNode;

        var updated = node.Deserialize(WorkbenchJsonContext.Default.WorkbenchConfig);
        if (updated is null)
        {
            throw new InvalidOperationException("Failed to parse updated config.");
        }

        return updated;
    }

    public static void SaveConfig(string repoRoot, WorkbenchConfig config)
    {
        var path = WorkbenchConfig.GetConfigPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? repoRoot);
        var json = JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig);
        File.WriteAllText(path, json + "\n");
    }

    private static JsonNode ParseValue(string rawValue, bool parseJson)
    {
        if (!parseJson)
        {
            return JsonValue.Create(rawValue) ?? throw new InvalidOperationException("Invalid config value.");
        }

        try
        {
            return JsonNode.Parse(rawValue) ?? throw new InvalidOperationException("Invalid JSON value.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON value: {ex.Message}", ex);
        }
    }
}
