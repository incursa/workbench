// CLI init workflow and interactive guide.
// Orchestrates scaffolding, front matter sync, and optional AI configuration without changing core behavior.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static void TryMigrateLegacyWorkLayout(string repoRoot, WorkbenchConfig config, bool dryRun)
    {
        var legacyRoot = Path.Combine(repoRoot, "work");
        var targetRoot = Path.Combine(repoRoot, config.Paths.WorkRoot);

        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        if (Directory.Exists(targetRoot))
        {
            // Avoid destructive merges; legacy content must be reconciled manually.
            Console.WriteLine($"Legacy work layout found at {legacyRoot} but {targetRoot} already exists; manual merge required.");
            return;
        }

        if (dryRun)
        {
            Console.WriteLine($"Dry run: would move legacy work layout from {legacyRoot} to {targetRoot}.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetRoot) ?? repoRoot);
        Directory.Move(legacyRoot, targetRoot);
        Console.WriteLine($"Moved legacy work layout from {legacyRoot} to {targetRoot}.");
    }

    sealed record InitWorkflowOptions(
        bool Force,
        bool NonInteractive,
        bool SkipWizard,
        bool SyncFrontMatter,
        bool ConfigureOpenAi,
        string? CredentialStore,
        string? CredentialPath,
        string? OpenAiProvider,
        string? OpenAiKey,
        string? OpenAiModel);

    sealed record InitWorkflowResult(int ExitCode, bool ShouldRunWizard, IList<string> Summary);

    static async Task<InitWorkflowResult> RunInitWorkflowAsync(string repoRoot, InitWorkflowOptions options)
    {
        var summary = new List<string>();
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            summary.Add("Config error detected; using defaults for init.");
            config = WorkbenchConfig.Default;
        }

        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        var expectedPaths = new[]
        {
            config.Paths.DocsRoot,
            config.Paths.WorkRoot,
            config.Paths.ItemsDir,
            config.Paths.DoneDir,
            config.Paths.TemplatesDir
        };
        var missingPaths = expectedPaths
            .Where(path => !Directory.Exists(Path.Combine(repoRoot, path)))
            .ToList();
        var scaffoldComplete = File.Exists(configPath) && missingPaths.Count == 0;

        Console.WriteLine("Step 1: Scaffold repo layout");
        if (scaffoldComplete)
        {
            Console.WriteLine($"- Found config at {configPath}");
            Console.WriteLine("- All expected paths already exist.");
        }
        else if (missingPaths.Count > 0)
        {
            Console.WriteLine("- Missing: " + string.Join(", ", missingPaths));
        }
        else if (!File.Exists(configPath))
        {
            Console.WriteLine("- Missing: .workbench/config.json");
        }

        var runScaffold = false;
        if (!scaffoldComplete)
        {
            runScaffold = options.NonInteractive || Confirm("Run scaffold now?", defaultYes: true);
        }
        else if (options.Force)
        {
            runScaffold = true;
        }
        else if (!options.NonInteractive)
        {
            runScaffold = Confirm("Review/repair scaffold (recreate missing files)?", defaultYes: false);
        }

        if (runScaffold)
        {
            var allowOverwrite = options.Force;
            if (!options.NonInteractive && !options.Force)
            {
                allowOverwrite = Confirm("Overwrite existing scaffold files?", defaultYes: false);
            }
            var scaffoldResult = ScaffoldService.Scaffold(repoRoot, allowOverwrite);
            summary.Add($"Scaffolded repo ({scaffoldResult.Created.Count} created, {scaffoldResult.Skipped.Count} skipped).");
        }
        else
        {
            summary.Add("Skipped scaffolding.");
        }

        Console.WriteLine("Step 2: Front matter guidance");
        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        var docFiles = Directory.Exists(docsRoot)
            ? Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).ToList()
            : new List<string>();
        var missingFrontMatter = 0;
        var invalidFrontMatter = 0;
        foreach (var file in docFiles)
        {
            var content = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            if (FrontMatter.TryParse(content, out _, out string? _))
            {
                continue;
            }
            var trimmed = content.TrimStart();
            if (trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
                trimmed.StartsWith("---\r\n", StringComparison.Ordinal))
            {
                invalidFrontMatter++;
            }
            else
            {
                missingFrontMatter++;
            }
        }

        if (docFiles.Count == 0)
        {
            Console.WriteLine("- No docs found under docs/.");
        }
        else
        {
            Console.WriteLine($"- Docs scanned: {docFiles.Count}");
            Console.WriteLine($"- Missing front matter: {missingFrontMatter}");
            if (invalidFrontMatter > 0)
            {
                Console.WriteLine($"- Invalid front matter: {invalidFrontMatter}");
            }
        }

        var runFrontMatter = options.SyncFrontMatter;
        if (!options.NonInteractive)
        {
            var defaultYes = missingFrontMatter > 0 || invalidFrontMatter > 0;
            runFrontMatter = Confirm("Add Workbench front matter to docs now?", defaultYes: defaultYes);
        }

        if (runFrontMatter)
        {
            try
            {
                var syncResult = await DocService.SyncLinksAsync(repoRoot, config, includeAllDocs: true, syncIssues: false, includeDone: false, dryRun: false).ConfigureAwait(false);
                summary.Add($"Updated front matter in {syncResult.DocsUpdated} docs.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Front matter update failed: {ex}");
                summary.Add("Front matter update failed.");
            }
        }
        else
        {
            summary.Add("Skipped front matter updates.");
        }

        Console.WriteLine("Step 3: OpenAI configuration");
        var envKey = Environment.GetEnvironmentVariable("WORKBENCH_AI_OPENAI_KEY");
        var envOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var hasAiKey = !string.IsNullOrWhiteSpace(envKey) || !string.IsNullOrWhiteSpace(envOpenAiKey);
        if (hasAiKey)
        {
            Console.WriteLine("- Detected existing OpenAI API key in environment.");
        }
        else
        {
            Console.WriteLine("- No OpenAI API key detected.");
        }

        var configureOpenAi = options.ConfigureOpenAi;
        if (!options.NonInteractive)
        {
            configureOpenAi = Confirm("Configure OpenAI settings now?", defaultYes: false);
        }

        if (configureOpenAi)
        {
            var provider = options.OpenAiProvider;
            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = options.NonInteractive
                    ? "openai"
                    : Prompt("AI provider (openai|none)", "openai");
            }
            provider = provider.Trim();
            if (string.Equals(provider, "none", StringComparison.OrdinalIgnoreCase))
            {
                summary.Add("Set AI provider to none (OpenAI disabled).");
            }
            else
            {
                var model = options.OpenAiModel;
                if (string.IsNullOrWhiteSpace(model))
                {
                    model = options.NonInteractive
                        ? "gpt-4o-mini"
                        : Prompt("OpenAI model", "gpt-4o-mini");
                }

                var key = options.OpenAiKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = options.NonInteractive
                        ? string.Empty
                        : Prompt("OpenAI API key (stored as WORKBENCH_AI_OPENAI_KEY)");
                }

                var store = options.CredentialStore;
                if (string.IsNullOrWhiteSpace(store) && !options.NonInteractive)
                {
                    var storeOptions = new List<(string Label, string Description)>
                    {
                        ("local", "Store in an ignored file inside the repo."),
                        ("external", "Store in a file outside the repo and source it."),
                        ("skip", "Skip writing a file (manual env setup).")
                    };
                    var selection = PromptSelection("Credential storage location", storeOptions);
                    store = selection >= 0 ? storeOptions[selection].Label : "skip";
                }

                store = store?.Trim().ToLowerInvariant() ?? "skip";
                if (string.Equals(store, "local", StringComparison.OrdinalIgnoreCase))
                {
                    var defaultPath = Path.Combine(repoRoot, ".workbench", "credentials.env");
                    var credentialPath = options.CredentialPath;
                    if (string.IsNullOrWhiteSpace(credentialPath))
                    {
                        credentialPath = options.NonInteractive
                            ? defaultPath
                            : Prompt("Local credentials file path", defaultPath);
                    }
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(credentialPath) ?? repoRoot);
                        var lines = new[]
                        {
                            "# Workbench AI credentials",
                            $"WORKBENCH_AI_PROVIDER={provider}",
                            $"WORKBENCH_AI_OPENAI_KEY={key}",
                            $"WORKBENCH_AI_MODEL={model}"
                        };
                        await File.WriteAllLinesAsync(credentialPath, lines).ConfigureAwait(false);
                        if (IsPathInsideRepo(repoRoot, credentialPath))
                        {
                            EnsureGitignoreEntry(repoRoot, NormalizeRepoPath(repoRoot, credentialPath));
                        }
                        summary.Add($"Wrote OpenAI credentials to {credentialPath}.");
                    }
                    else
                    {
                        Console.WriteLine("No API key provided; skipped writing credentials file.");
                        summary.Add("Skipped writing OpenAI credentials (missing key).");
                    }
                }
                else if (string.Equals(store, "external", StringComparison.OrdinalIgnoreCase))
                {
                    var credentialPath = options.CredentialPath;
                    if (string.IsNullOrWhiteSpace(credentialPath))
                    {
                        credentialPath = options.NonInteractive
                            ? "~/.workbench/credentials.env"
                            : Prompt("External credentials file path", "~/.workbench/credentials.env");
                    }
                    Console.WriteLine("Save the following in your external file and source it:");
                    Console.WriteLine($"WORKBENCH_AI_PROVIDER={provider}");
                    Console.WriteLine("WORKBENCH_AI_OPENAI_KEY=<your-key>");
                    Console.WriteLine($"WORKBENCH_AI_MODEL={model}");
                    summary.Add($"OpenAI credentials configured for external file at {credentialPath}.");
                }
                else
                {
                    Console.WriteLine("Set these environment variables to enable AI summaries:");
                    Console.WriteLine("WORKBENCH_AI_OPENAI_KEY or OPENAI_API_KEY");
                    Console.WriteLine("WORKBENCH_AI_MODEL (default: gpt-4o-mini)");
                    summary.Add("Skipped writing OpenAI credentials (manual setup).");
                }
            }
        }
        else
        {
            summary.Add("Skipped OpenAI configuration.");
        }

        Console.WriteLine("Init summary:");
        foreach (var entry in summary)
        {
            Console.WriteLine($"- {entry}");
        }

        var shouldRunWizard = !options.SkipWizard && !options.NonInteractive;
        return new InitWorkflowResult(0, shouldRunWizard, summary);
    }

    static int RunGuide(string repoRoot)
    {
        var summary = new List<string>();
        Console.WriteLine("Workbench guide");
        var options = new List<(string Label, string Description)>
        {
            ("Create work item", "Guided creation of task, bug, or spike work items."),
            ("Create document", "Create a spec, ADR, runbook, guide, or general doc."),
            ("Regenerate workboard", "Refresh docs/70-work/README.md from current items."),
            ("Exit", "Leave the guide.")
        };

        var selection = PromptSelection("Choose what you want to do", options);
        if (selection == 3 || selection < 0)
        {
            Console.WriteLine("Guide exited.");
            return 0;
        }

        try
        {
            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
                return 2;
            }

            if (selection == 0)
            {
                var itemTypes = new List<(string Label, string Description)>
                {
                    ("task", "Planned work with clear acceptance criteria."),
                    ("bug", "Defect or regression to fix."),
                    ("spike", "Research or investigation work.")
                };
                var itemSelection = PromptSelection("Work item type", itemTypes);
                if (itemSelection < 0)
                {
                    Console.WriteLine("No work item selected.");
                    return 2;
                }
                var itemType = itemTypes[itemSelection].Label;
                var title = Prompt("Title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    Console.WriteLine("Title is required.");
                    return 2;
                }
                var status = Prompt("Status (draft/ready/in-progress/blocked/done/dropped)", "draft");
                var priority = Prompt("Priority (low/medium/high/critical)", "medium");
                var owner = Prompt("Owner (optional)");
                var itemResult = WorkItemService.CreateItem(
                    repoRoot,
                    config,
                    itemType,
                    title,
                    string.IsNullOrWhiteSpace(status) ? null : status,
                    string.IsNullOrWhiteSpace(priority) ? null : priority,
                    string.IsNullOrWhiteSpace(owner) ? null : owner);
                Console.WriteLine($"{itemResult.Id} created at {itemResult.Path}");
                summary.Add($"Created work item {itemResult.Id}.");
                Console.WriteLine("Next steps: edit the work item, then use `workbench item status` when ready.");
            }
            else if (selection == 1)
            {
                var docTypes = new List<(string Label, string Description)>
                {
                    ("spec", "Product or feature specification."),
                    ("adr", "Architecture decision record."),
                    ("runbook", "Operational procedure or checklist."),
                    ("guide", "How-to or onboarding guide."),
                    ("doc", "General documentation.")
                };
                var docSelection = PromptSelection("Document type", docTypes);
                if (docSelection < 0)
                {
                    Console.WriteLine("No document type selected.");
                    return 2;
                }
                var docType = docTypes[docSelection].Label;
                var title = Prompt("Title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    Console.WriteLine("Title is required.");
                    return 2;
                }
                var path = Prompt("Custom path (optional)");
                var workItemsRaw = Prompt("Link work items (comma separated IDs, optional)");
                var workItems = workItemsRaw.Length == 0
                    ? new List<string>()
                    : workItemsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                var codeRefsRaw = Prompt("Code refs (comma separated, optional)");
                var codeRefs = codeRefsRaw.Length == 0
                    ? new List<string>()
                    : codeRefsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                var force = Confirm("Overwrite if the doc already exists?", defaultYes: false);
                var docResult = DocService.CreateDoc(
                    repoRoot,
                    config,
                    docType,
                    title,
                    string.IsNullOrWhiteSpace(path) ? null : path,
                    workItems,
                    codeRefs,
                    force);
                Console.WriteLine($"Doc created at {docResult.Path}");
                summary.Add($"Created {docResult.Type} doc at {docResult.Path}.");
                Console.WriteLine("Next steps: edit the doc and link work items as needed.");
            }
            else if (selection == 2)
            {
                var result = WorkboardService.Regenerate(repoRoot, config);
                Console.WriteLine($"Workboard regenerated: {result.Path}");
                summary.Add("Regenerated workboard.");
                Console.WriteLine("Next steps: review `docs/70-work/README.md` for updated status.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 2;
        }

        Console.WriteLine("Guide summary:");
        foreach (var entry in summary)
        {
            Console.WriteLine($"- {entry}");
        }
        Console.WriteLine("Next steps: run `workbench --help` to explore more commands.");
        return 0;
    }
}
