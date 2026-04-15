using System.Text.Json;
namespace Workbench.Core;

public static class CanonicalArtifactJsonLoader
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CanonicalArtifactModel Load(string repoRoot, string jsonPath)
    {
        var load = LoadForValidation(repoRoot, jsonPath);
        return load.Artifact ?? throw new InvalidOperationException($"canonical artifact JSON returned no payload for '{jsonPath}'.");
    }

    public static CanonicalArtifactDocument LoadDocument(string repoRoot, string jsonPath)
    {
        var load = LoadForValidation(repoRoot, jsonPath);

        using var document = JsonDocument.Parse(load.Json);
        var data = JsonElementToObjectConverter.ConvertObject(document.RootElement);
        var artifact = load.Artifact
            ?? throw new InvalidOperationException($"canonical artifact JSON returned no payload for '{jsonPath}'.");

        return new CanonicalArtifactDocument(artifact, data, JsonWriter.Serialize(data));
    }

    internal static CanonicalArtifactLoadResult LoadForValidation(string repoRoot, string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var normalizedJson = SchemaValidationService.NormalizeCanonicalArtifactJson(json);
        var errors = new List<string>(SchemaValidationService.ValidateCanonicalArtifactJson(repoRoot, jsonPath, normalizedJson));

        CanonicalArtifactModel? artifact = null;
        try
        {
            artifact = JsonSerializer.Deserialize<CanonicalArtifactModel>(normalizedJson, jsonOptions);
        }
        catch (Exception ex)
        {
            errors.Add($"{jsonPath}: canonical artifact JSON deserialization error: {ex}");
        }

        if (artifact is null && errors.Count == 0)
        {
            errors.Add($"{jsonPath}: canonical artifact JSON returned no payload.");
        }

        return new CanonicalArtifactLoadResult(artifact, normalizedJson, errors);
    }

    internal sealed record CanonicalArtifactLoadResult(
        CanonicalArtifactModel? Artifact,
        string Json,
        IReadOnlyList<string> Errors);
}
