// Repository configuration model and loader for Workbench.
// Invariants: load failures return defaults and surface error text; config file is JSON at .workbench/config.json.
namespace Workbench.Core;

/// <summary>
/// Root configuration for Workbench, combining path, ID, git, GitHub, and validation settings.
/// </summary>
/// <param name="Paths">Repo-relative directories and file locations used by Workbench.</param>
/// <param name="Ids">ID prefixes and allocation rules for work items.</param>
/// <param name="Git">Git defaults for branches and commits.</param>
/// <param name="Github">GitHub integration settings and defaults.</param>
/// <param name="Validation">Validation options and defaults for repo checks.</param>
public sealed record WorkbenchConfig(
    PathsConfig Paths,
    IdsConfig Ids,
    GitConfig Git,
    GithubConfig Github,
    ValidationConfig Validation,
    TuiConfig Tui)
{
    /// <summary>
    /// Default configuration used when no config file exists or load fails.
    /// </summary>
    public static WorkbenchConfig Default => new(
        new PathsConfig(),
        new IdsConfig(),
        new GitConfig(),
        new GithubConfig(),
        new ValidationConfig(),
        new TuiConfig());

    /// <summary>
    /// Loads configuration from the repo, falling back to defaults on error.
    /// </summary>
    /// <param name="repoRoot">Repository root path.</param>
    /// <param name="error">Populated with a load/parse error message when applicable.</param>
    /// <returns>Resolved configuration or defaults.</returns>
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
            var config = JsonSerializer.Deserialize(json, Workbench.Core.WorkbenchJsonContext.Default.WorkbenchConfig);
            if (config is null)
            {
                error = "Failed to parse config.";
                return Default;
            }
            if (config.Validation is null)
            {
                config = config with { Validation = new ValidationConfig() };
            }
            if (config.Github is not null && config.Github.Sync is null)
            {
                config = config with { Github = config.Github with { Sync = new GithubSyncConfig() } };
            }
            if (config.Tui is null)
            {
                config = config with { Tui = new TuiConfig() };
            }
            return config;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return Default;
        }
    }

    /// <summary>
    /// Resolves the expected config file path for a repo.
    /// </summary>
    public static string GetConfigPath(string repoRoot)
    {
        return Path.Combine(repoRoot, ".workbench", "config.json");
    }

    /// <summary>
    /// Returns the configured ID prefix for a work item type.
    /// </summary>
    /// <param name="type">Work item type (bug, task, spike).</param>
    /// <returns>Prefix configured for the type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown types.</exception>
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
