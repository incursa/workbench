using System.Text.Json;

namespace Workbench.Core;

public static class CanonicalArtifactJsonLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CanonicalArtifactModel Load(string repoRoot, string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var errors = SchemaValidationService.ValidateCanonicalArtifactJson(repoRoot, jsonPath, json);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var artifact = JsonSerializer.Deserialize<CanonicalArtifactModel>(json, JsonOptions);
        return artifact ?? throw new InvalidOperationException($"canonical artifact JSON returned no payload for '{jsonPath}'.");
    }

    public static CanonicalArtifactDocument LoadDocument(string repoRoot, string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var errors = SchemaValidationService.ValidateCanonicalArtifactJson(repoRoot, jsonPath, json);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        using var document = JsonDocument.Parse(json);
        var data = JsonElementToObjectConverter.ConvertObject(document.RootElement);
        var artifact = JsonSerializer.Deserialize<CanonicalArtifactModel>(json, JsonOptions)
            ?? throw new InvalidOperationException($"canonical artifact JSON returned no payload for '{jsonPath}'.");

        return new CanonicalArtifactDocument(artifact, data, JsonWriter.Serialize(data));
    }
}
