using System.Globalization;
using System.Text;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static async Task<MigrationData> RunCoherentMigrationAsync(string repoRoot, WorkbenchConfig config, bool dryRun)
    {
        var movedToDone = new List<string>();
        var movedToItems = new List<string>();

        var normalizedConfig = NormalizeMigrationConfig(config);
        if (!dryRun && !Equals(config, normalizedConfig))
        {
            ConfigService.SaveConfig(repoRoot, normalizedConfig);
        }

        MoveItemsByStatus(repoRoot, normalizedConfig, dryRun, movedToDone, movedToItems);
        StripDocMetadataFromWorkItems(repoRoot, normalizedConfig, dryRun);

        var itemsNormalized = WorkItemService.NormalizeItems(repoRoot, normalizedConfig, includeDone: true, dryRun);
        var docSync = await DocService.SyncLinksAsync(
                repoRoot,
                normalizedConfig,
                includeAllDocs: true,
                syncIssues: false,
                includeDone: true,
                dryRun)
            .ConfigureAwait(false);

        var navSync = await NavigationService.SyncNavigationAsync(
                repoRoot,
                normalizedConfig,
                includeDone: true,
                syncIssues: false,
                force: true,
                syncWorkboard: true,
                dryRun,
                syncDocs: false)
            .ConfigureAwait(false);

        string? reportPath = null;
        if (!dryRun)
        {
            reportPath = WriteMigrationReport(
                repoRoot,
                movedToDone,
                movedToItems,
                itemsNormalized,
                docSync,
                navSync);
        }

        return new MigrationData(
            movedToDone,
            movedToItems,
            itemsNormalized,
            docSync.DocsUpdated + navSync.DocsUpdated,
            docSync.ItemsUpdated + navSync.ItemsUpdated,
            navSync.IndexFilesUpdated,
            navSync.WorkboardUpdated,
            reportPath,
            dryRun);
    }

    static WorkbenchConfig NormalizeMigrationConfig(WorkbenchConfig config)
    {
        var sync = config.Github.Sync;
        var normalizedMode = (sync.Mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedMode is not ("manual" or "generate" or "active"))
        {
            normalizedMode = "generate";
        }

        var normalizedConflict = (sync.ConflictDefault ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedConflict is not ("fail" or "local" or "github"))
        {
            normalizedConflict = "fail";
        }

        return config with
        {
            Github = config.Github with
            {
                Sync = sync with
                {
                    Mode = normalizedMode,
                    ConflictDefault = normalizedConflict,
                },
            },
        };
    }

    static void MoveItemsByStatus(
        string repoRoot,
        WorkbenchConfig config,
        bool dryRun,
        List<string> movedToDone,
        List<string> movedToItems)
    {
        var itemsDir = Path.Combine(repoRoot, config.Paths.ItemsDir);
        var doneDir = Path.Combine(repoRoot, config.Paths.DoneDir);

        if (!dryRun)
        {
            Directory.CreateDirectory(itemsDir);
            Directory.CreateDirectory(doneDir);
        }

        MoveDirectoryItems(
            sourceDir: itemsDir,
            targetDir: doneDir,
            shouldMove: item => IsTerminalStatus(item.Status),
            repoRoot: repoRoot,
            dryRun: dryRun,
            moved: movedToDone);

        MoveDirectoryItems(
            sourceDir: doneDir,
            targetDir: itemsDir,
            shouldMove: item => !IsTerminalStatus(item.Status),
            repoRoot: repoRoot,
            dryRun: dryRun,
            moved: movedToItems);
    }

    static void MoveDirectoryItems(
        string sourceDir,
        string targetDir,
        Func<WorkItem, bool> shouldMove,
        string repoRoot,
        bool dryRun,
        List<string> moved)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(sourceDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = WorkItemService.LoadItem(path);
            if (item is null || !shouldMove(item))
            {
                continue;
            }

            var destination = Path.Combine(targetDir, Path.GetFileName(path));
            if (File.Exists(destination))
            {
                throw new InvalidOperationException($"Cannot migrate {path}; destination already exists: {destination}");
            }

            moved.Add($"{NormalizeRepoPath(repoRoot, path)} -> {NormalizeRepoPath(repoRoot, destination)}");
            if (dryRun)
            {
                continue;
            }

            File.Move(path, destination, overwrite: false);
            LinkUpdater.UpdateLinks(repoRoot, path, destination);
        }
    }

    static void StripDocMetadataFromWorkItems(string repoRoot, WorkbenchConfig config, bool dryRun)
    {
        foreach (var directory in new[]
                 {
                     Path.Combine(repoRoot, config.Paths.ItemsDir),
                     Path.Combine(repoRoot, config.Paths.DoneDir),
                     Path.Combine(repoRoot, config.Paths.TemplatesDir),
                 })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = File.ReadAllText(path);
                if (!FrontMatter.TryParse(content, out var frontMatter, out _))
                {
                    continue;
                }

                if (!frontMatter!.Data.Remove("workbench"))
                {
                    continue;
                }

                if (!dryRun)
                {
                    File.WriteAllText(path, frontMatter.Serialize());
                }
            }
        }
    }

    static string WriteMigrationReport(
        string repoRoot,
        IList<string> movedToDone,
        IList<string> movedToItems,
        int itemsNormalized,
        DocService.DocSyncResult docSync,
        NavigationService.NavigationSyncResult navSync)
    {
        var reportDir = Path.Combine(repoRoot, "docs", "60-tracking");
        Directory.CreateDirectory(reportDir);
        var fileName = $"migration-coherent-v1-{DateTime.UtcNow:yyyy-MM-dd}.md";
        var reportPath = Path.Combine(reportDir, fileName);

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("workbench:");
        builder.AppendLine("  type: doc");
        builder.AppendLine("  workItems: []");
        builder.AppendLine("  codeRefs: []");
        builder.AppendLine("owner: platform");
        builder.AppendLine("status: active");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"updated: {DateTime.UtcNow:yyyy-MM-dd}"));
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Migration Report: coherent-v1");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Moved to done: {movedToDone.Count}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Moved to active: {movedToItems.Count}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Items normalized: {itemsNormalized}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Docs updated: {docSync.DocsUpdated + navSync.DocsUpdated}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Item links updated: {docSync.ItemsUpdated + navSync.ItemsUpdated}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Index files updated: {navSync.IndexFilesUpdated}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Workboard updated: {navSync.WorkboardUpdated}"));

        if (movedToDone.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Moved to done");
            builder.AppendLine();
            foreach (var entry in movedToDone)
            {
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {entry}"));
            }
        }

        if (movedToItems.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Moved to active");
            builder.AppendLine();
            foreach (var entry in movedToItems)
            {
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {entry}"));
            }
        }

        File.WriteAllText(reportPath, builder.ToString());
        return NormalizeRepoPath(repoRoot, reportPath);
    }
}
