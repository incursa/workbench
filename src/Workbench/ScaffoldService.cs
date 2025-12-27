using System.Text.Json;

namespace Workbench;

public static class ScaffoldService
{
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

        EnsureDir(config.Paths.DocsRoot);
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "00-overview"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "10-product"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "20-architecture"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "30-contracts"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "40-decisions"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "50-runbooks"));
        EnsureDir(Path.Combine(config.Paths.DocsRoot, "60-tracking"));

        EnsureDir(config.Paths.WorkRoot);
        EnsureDir(config.Paths.ItemsDir);
        EnsureDir(config.Paths.DoneDir);
        EnsureDir(config.Paths.TemplatesDir);

        WriteFile(Path.Combine(config.Paths.DocsRoot, "README.md"),
            "# Docs\n\nDocumentation for product, architecture, decisions, and operational guidance.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "00-overview", "README.md"),
            "# Overview\n\nHigh-level product summaries, vision, and specifications.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "10-product", "README.md"),
            "# Product\n\nProduct requirements, feature specs, and user-facing behavior.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "20-architecture", "README.md"),
            "# Architecture\n\nSystem architecture, data flows, and major components.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "30-contracts", "README.md"),
            "# Contracts\n\nAPIs, CLI interfaces, schemas, and external contracts.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "40-decisions", "README.md"),
            "# Decisions\n\nArchitecture Decision Records (ADRs) and tradeoff history.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "50-runbooks", "README.md"),
            "# Runbooks\n\nOperational procedures, troubleshooting, and release playbooks.\n");
        WriteFile(Path.Combine(config.Paths.DocsRoot, "60-tracking", "README.md"),
            "# Tracking\n\nMilestones, progress tracking, and delivery notes.\n");

        WriteFile(Path.Combine(config.Paths.WorkRoot, "README.md"),
            "# Work\n\nActive work items live in `work/items`. Completed items may move to `work/done`.\nTemplates for new work items are stored in `work/templates`.\n");
        WriteFile(config.Paths.WorkboardFile,
            "# Workboard\n\n## Now (in-progress)\n\n## Next (ready)\n\n## Blocked\n\n## Draft\n");
        WriteFile(Path.Combine(config.Paths.ItemsDir, "README.md"),
            "# Items\n\nActive work items live here as Markdown files named `<ID>-<slug>.md`.\n");
        WriteFile(Path.Combine(config.Paths.DoneDir, "README.md"),
            "# Done\n\nCompleted work items may be moved here when closed.\n");
        WriteFile(Path.Combine(config.Paths.TemplatesDir, "README.md"),
            "# Templates\n\nWork item templates used by `workbench item new`.\n");

        WriteFile(Path.Combine(config.Paths.TemplatesDir, "work-item.bug.md"), DefaultBugTemplate);
        WriteFile(Path.Combine(config.Paths.TemplatesDir, "work-item.task.md"), DefaultTaskTemplate);
        WriteFile(Path.Combine(config.Paths.TemplatesDir, "work-item.spike.md"), DefaultSpikeTemplate);

        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        var configJson = JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig);
        WriteFile(Path.Combine(".workbench", "config.json"), configJson + "\n");

        return new ScaffoldResult(created, skipped, configPath);
    }

    private const string DefaultBugTemplate = """
        ---
        id: BUG-0000
        type: bug
        status: draft
        priority: medium
        owner: null
        created: 0000-00-00
        updated: null
        tags: []
        related:
          specs: []
          adrs: []
          files: []
          prs: []
          issues: []
          branches: []
        ---

        # BUG-0000 - <title>

        ## Summary

        ## Steps to reproduce

        ## Expected behavior

        ## Actual behavior

        ## Acceptance criteria
        -
        """;

    private const string DefaultTaskTemplate = """
        ---
        id: TASK-0000
        type: task
        status: draft
        priority: medium
        owner: null
        created: 0000-00-00
        updated: null
        tags: []
        related:
          specs: []
          adrs: []
          files: []
          prs: []
          issues: []
          branches: []
        ---

        # TASK-0000 - <title>

        ## Summary

        ## Acceptance criteria
        -
        """;

    private const string DefaultSpikeTemplate = """
        ---
        id: SPIKE-0000
        type: spike
        status: draft
        priority: medium
        owner: null
        created: 0000-00-00
        updated: null
        tags: []
        related:
          specs: []
          adrs: []
          files: []
          prs: []
          issues: []
          branches: []
        ---

        # SPIKE-0000 - <title>

        ## Summary

        ## Research notes

        ## Acceptance criteria
        -
        """;
}
