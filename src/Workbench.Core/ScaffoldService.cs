// Repository scaffolding for Workbench layout.
// Invariants: uses default config paths; never overwrites unless force is true.
#pragma warning disable S1144
namespace Workbench.Core;

public static class ScaffoldService
{
    /// <summary>
    /// Result payload returned by scaffold operations.
    /// </summary>
    /// <param name="Created">Paths created during scaffolding.</param>
    /// <param name="Skipped">Paths skipped because they already existed.</param>
    /// <param name="ConfigPath">Path to the config file.</param>
    public sealed record ScaffoldResult(IList<string> Created, IList<string> Skipped, string ConfigPath);

    public static ScaffoldResult Scaffold(string repoRoot, bool force)
    {
        var config = WorkbenchConfig.Default;
        var created = new List<string>();
        var skipped = new List<string>();

        void EnsureDir(string relativePath)
        {
            var path = Path.Combine(repoRoot, relativePath);
            Directory.CreateDirectory(path);
        }

        void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(repoRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? repoRoot);
            if (File.Exists(path) && !force)
            {
                skipped.Add(relativePath);
                return;
            }
            File.WriteAllText(path, content);
            created.Add(relativePath);
        }

        EnsureDir(config.Paths.SpecsRoot);
        EnsureDir(config.Paths.ArchitectureDir);
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "requirements"));
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "verification"));
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "work-items"));
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "generated"));
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "templates"));
        EnsureDir(Path.Combine(config.Paths.SpecsRoot, "schemas"));
        EnsureDir("runbooks");
        EnsureDir("tracking");

        EnsureDir(config.Paths.ItemsDir);
        WriteFile(Path.Combine("runbooks", "README.md"), BuildRunbooksReadmeTemplate());
        WriteFile(Path.Combine("tracking", "README.md"), BuildTrackingReadmeTemplate());
        WriteFile(Path.Combine(config.Paths.SpecsRoot, "requirements", "_index.md"), BuildRequirementsIndexTemplate());
        WriteFile(Path.Combine(config.Paths.ArchitectureDir, "WB", "_index.md"), BuildArchitectureIndexTemplate());
        WriteFile(Path.Combine(config.Paths.SpecsRoot, "verification", "WB", "_index.md"), BuildVerificationIndexTemplate());
        WriteFile(Path.Combine(config.Paths.SpecsRoot, "work-items", "WB", "_index.md"), BuildWorkItemsIndexTemplate());

        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        var configJson = JsonSerializer.Serialize(config, Workbench.Core.WorkbenchJsonContext.Default.WorkbenchConfig);
        WriteFile(Path.Combine(".workbench", "config.json"), configJson + "\n");

        return new ScaffoldResult(created, skipped, configPath);
    }

    private static string BuildRunbooksReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Runbooks",
            string.Empty,
            "Operational procedures, troubleshooting, and release playbooks.",
            string.Empty,
            "## Include",
            string.Empty,
            "- `runbooks/*.md`",
            string.Empty);
    }

    private static string BuildTrackingReadmeTemplate()
    {
        return string.Join(
            "\n",
            "# Tracking",
            string.Empty,
            "Milestones, progress tracking, and delivery notes.",
            string.Empty,
            "## Include",
            string.Empty,
            "- Milestone summaries and delivery notes.",
            "- Status updates or weekly rollups.",
            "- Links back to work items and related artifacts.",
            string.Empty);
    }

    private static string BuildRequirementsIndexTemplate()
    {
        return string.Join(
            "\n",
            "# Requirements",
            string.Empty,
            "Canonical specification artifacts live under `specs/requirements/<domain>/`.",
            string.Empty,
            "## Index",
            string.Empty,
            "- Add domain folders here as requirements grow.",
            string.Empty);
    }

    private static string BuildArchitectureIndexTemplate()
    {
        return string.Join(
            "\n",
            "# Architecture",
            string.Empty,
            "Canonical architecture artifacts live under `specs/architecture/<domain>/`.",
            string.Empty,
            "## Index",
            string.Empty,
            "- Add domain folders here as architecture grows.",
            string.Empty,
            "## Notes",
            string.Empty,
            "- Domain indexes are optional navigation aids.",
            string.Empty);
    }

    private static string BuildVerificationIndexTemplate()
    {
        return string.Join(
            "\n",
            "# Verification",
            string.Empty,
            "Canonical verification artifacts live under `specs/verification/<domain>/`.",
            string.Empty,
            "## Index",
            string.Empty,
            "- Add verification artifacts here as coverage grows.",
            string.Empty);
    }

    private static string BuildWorkItemsIndexTemplate()
    {
        return string.Join(
            "\n",
            "# Work Items",
            string.Empty,
            "Canonical work-item artifacts live under `specs/work-items/<domain>/`.",
            string.Empty,
            "## Index",
            string.Empty,
            "- Add work items here as implementation work grows.",
            string.Empty);
    }
}
