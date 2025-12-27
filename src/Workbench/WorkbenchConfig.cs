using System.Text.Json;

namespace Workbench;

public sealed record WorkbenchConfig(
    PathsConfig Paths,
    IdsConfig Ids,
    GitConfig Git,
    GithubConfig Github)
{
    public static WorkbenchConfig Default => new(
        new PathsConfig(),
        new IdsConfig(),
        new GitConfig(),
        new GithubConfig());

    public static WorkbenchConfig Load(string repoRoot, out string? error)
    {
        error = null;
        var configPath = GetConfigPath(repoRoot);
        if (!File.Exists(configPath))
        {
            return Default;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, WorkbenchJsonContext.Default.WorkbenchConfig);
            if (config is null)
            {
                error = "Failed to parse config.";
                return Default;
            }
            return config;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return Default;
        }
    }

    public static string GetConfigPath(string repoRoot)
    {
        return Path.Combine(repoRoot, ".workbench", "config.json");
    }

    public string GetPrefix(string type)
    {
        return type switch
        {
            "bug" => Ids.Prefixes.Bug,
            "task" => Ids.Prefixes.Task,
            "spike" => Ids.Prefixes.Spike,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
