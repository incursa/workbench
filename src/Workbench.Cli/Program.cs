// CLI command surface: defines the command tree, dispatches to core services, and normalizes exit codes.
// Invariants: avoid mutating repo state unless a command explicitly does so; keep output format consistent.
// Does not implement business logic; delegates to Workbench.Core for IO, validation, and persistence.
using Workbench.Core;
using Workbench.Core.Voice;

namespace Workbench.Cli;

using System.CommandLine;
using System.Globalization;
using System.Linq;
using System.Reflection;

public partial class Program
{
    /// <summary>
    /// Executes the CLI command tree and returns the process exit code.
    /// </summary>
    /// <param name="args">Raw command-line arguments provided by the host process.</param>
    /// <returns>Exit code to return to the host process.</returns>
    public async static Task<int> RunAsync(string[] args)
    {
        args = NormalizeGlobalOptions(args);
        InitializeRuntimeContext(args);

        var repoOption = new Option<string?>("--repo")
        {
            Description = "Target repo (defaults to current dir)"
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Output format (table|json)",
            DefaultValueFactory = _ => "table"
        };
        formatOption.CompletionSources.Add("table", "json");

        var noColorOption = new Option<bool>("--no-color")
        {
            Description = "Disable colored output"
        };

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress non-error output"
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Print full exception diagnostics on failure."
        };

        var root = new RootCommand("Incursa Workbench CLI");
        root.Options.Add(repoOption);
        root.Options.Add(formatOption);
        root.Options.Add(noColorOption);
        root.Options.Add(quietOption);
        root.Options.Add(debugOption);

        var versionCommand = new Command("version", "Print CLI version.");
        versionCommand.SetAction(parseResult =>
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(Program).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            var version = informationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";
            Console.WriteLine(version);
            SetExitCode(0);
        });
        root.Subcommands.Add(versionCommand);

        var doctorCommand = new Command("doctor", "Check git, config, and expected paths.");
        var doctorJsonOption = new Option<bool>("--json")
        {
            Description = "Output machine-readable JSON."
        };
        doctorCommand.Options.Add(doctorJsonOption);
        doctorCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var jsonOutput = parseResult.GetValue(doctorJsonOption);
                var repoRoot = ResolveRepo(repo);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                var schemaErrors = SchemaValidationService.ValidateConfig(repoRoot);
                var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
                var paths = new[]
                {
            config.Paths.DocsRoot,
            config.Paths.WorkRoot,
            config.Paths.ItemsDir,
            config.Paths.TemplatesDir
                };
                var missing = paths.Where(p => !Directory.Exists(Path.Combine(repoRoot, p))).ToList();
                var checks = new List<DoctorCheck>();
                var hasError = configError is not null || schemaErrors.Count > 0;
                var hasWarnings = false;
                var gitIssue = false;
                var configIssue = false;
                var pathIssue = false;
                var githubIssue = false;

                try
                {
                    var gitResult = GitService.Run(repoRoot, "--version");
                    if (gitResult.ExitCode == 0)
                    {
                        checks.Add(new DoctorCheck(
                            "git",
                            "ok",
                            new DoctorCheckDetails(
                                Version: gitResult.StdOut,
                                Error: null,
                                Reason: null,
                                Path: null,
                                Missing: null,
                                SchemaErrors: null)));
                    }
                    else
                    {
                        checks.Add(new DoctorCheck(
                            "git",
                            "warn",
                            new DoctorCheckDetails(
                                Version: null,
                                Error: gitResult.StdErr,
                                Reason: null,
                                Path: null,
                                Missing: null,
                                SchemaErrors: null)));
                        hasWarnings = true;
                        gitIssue = true;
                    }
                }
#pragma warning disable ERP022
                catch (Exception)
                {
                    checks.Add(new DoctorCheck(
                        "git",
                        "warn",
                        new DoctorCheckDetails(
                            Version: null,
                            Error: "git not installed or not on PATH.",
                            Reason: null,
                            Path: null,
                            Missing: null,
                            SchemaErrors: null)));
                    hasWarnings = true;
                    gitIssue = true;
                }
#pragma warning restore ERP022

                checks.Add(new DoctorCheck(
                    "repo",
                    "ok",
                    new DoctorCheckDetails(
                        Version: null,
                        Error: null,
                        Reason: null,
                        Path: repoRoot,
                        Missing: null,
                        SchemaErrors: null)));

                if (File.Exists(configPath) && configError is null && schemaErrors.Count == 0)
                {
                    checks.Add(new DoctorCheck(
                        "config",
                        "ok",
                        new DoctorCheckDetails(
                            Version: null,
                            Error: null,
                            Reason: null,
                            Path: configPath,
                            Missing: null,
                            SchemaErrors: null)));
                }
                else
                {
                    checks.Add(new DoctorCheck(
                        "config",
                        "warn",
                        new DoctorCheckDetails(
                            Version: null,
                            Error: configError,
                            Reason: null,
                            Path: configPath,
                            Missing: null,
                            SchemaErrors: schemaErrors)));
                    hasWarnings = true;
                    configIssue = true;
                }

                if (missing.Count == 0)
                {
                    checks.Add(new DoctorCheck(
                        "paths",
                        "ok",
                        null));
                }
                else
                {
                    checks.Add(new DoctorCheck(
                        "paths",
                        "warn",
                        new DoctorCheckDetails(
                            Version: null,
                            Error: null,
                            Reason: null,
                            Path: null,
                            Missing: missing,
                            SchemaErrors: null)));
                    hasWarnings = true;
                    pathIssue = true;
                }

                var githubProvider = string.IsNullOrWhiteSpace(config.Github.Provider) ? "octokit" : config.Github.Provider;
                var githubStatus = await GithubService.CheckAuthStatusAsync(repoRoot, config).ConfigureAwait(false);
                if (string.Equals(githubStatus.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    checks.Add(new DoctorCheck(
                        "github",
                        "ok",
                        new DoctorCheckDetails(
                            Version: githubStatus.Version,
                            Error: null,
                            Reason: null,
                            Path: null,
                            Missing: null,
                            SchemaErrors: null)));
                }
                else
                {
                    checks.Add(new DoctorCheck(
                        "github",
                        githubStatus.Status,
                        new DoctorCheckDetails(
                            Version: null,
                            Error: null,
                            Reason: githubStatus.Reason,
                            Path: null,
                            Missing: null,
                            SchemaErrors: null)));
                    hasWarnings = true;
                    githubIssue = true;
                }

                if (jsonOutput)
                {
                    var payload = new DoctorOutput(
                        !hasError,
                        new DoctorData(repoRoot, checks));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.DoctorOutput);
                }
                else
                {
                    Console.WriteLine($"Repo: {repoRoot}");
                    Console.WriteLine("Checks:");
                    foreach (var check in checks)
                    {
                        Console.WriteLine($"- {check}");
                    }
                    if (hasError || hasWarnings)
                    {
                        Console.WriteLine("Next steps:");
                        if (gitIssue)
                        {
                            Console.WriteLine("- Install git and ensure it is on PATH.");
                        }
                        if (configIssue)
                        {
                            Console.WriteLine($"- Fix config at {configPath} or re-run `workbench init`.");
                        }
                        if (pathIssue)
                        {
                            Console.WriteLine("- Run `workbench init` to scaffold missing paths.");
                        }
                        if (githubIssue)
                        {
                            if (string.Equals(githubProvider, "gh", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("- Run `gh auth login` and ensure GitHub CLI is installed.");
                            }
                            else
                            {
                                Console.WriteLine("- Set WORKBENCH_GITHUB_TOKEN or GITHUB_TOKEN for Octokit.");
                            }
                        }
                    }
                }

                if (hasError)
                {
                    SetExitCode(2);
                    return;
                }
                SetExitCode(hasWarnings ? 1 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(doctorCommand);

        var scaffoldForceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing files."
        };
        var initNonInteractiveOption = new Option<bool>("--non-interactive")
        {
            Description = "Run init without prompts (use flags to enable steps)."
        };
        var initSkipGuideOption = new Option<bool>("--skip-guide")
        {
            Description = "Skip launching the interactive guide after init."
        };
        var initFrontMatterOption = new Option<bool>("--front-matter")
        {
            Description = "Add Workbench front matter to docs (non-interactive)."
        };
        var initConfigureOpenAiOption = new Option<bool>("--configure-openai")
        {
            Description = "Configure OpenAI settings (non-interactive)."
        };
        var initCredentialStoreOption = new Option<string?>("--credential-store")
        {
            Description = "Credential storage: local, external, skip."
        };
        initCredentialStoreOption.CompletionSources.Add("local", "external", "skip");
        var initCredentialPathOption = new Option<string?>("--credential-path")
        {
            Description = "Credentials file path for local or external storage."
        };
        var initOpenAiProviderOption = new Option<string?>("--openai-provider")
        {
            Description = "AI provider (openai|none)."
        };
        initOpenAiProviderOption.CompletionSources.Add("openai", "none");
        var initOpenAiKeyOption = new Option<string?>("--openai-key")
        {
            Description = "OpenAI API key (stored in credentials file)."
        };
        var initOpenAiModelOption = new Option<string?>("--openai-model")
        {
            Description = "OpenAI model (default: gpt-4o-mini)."
        };

        var initCommand = new Command("init", "Interactive setup for Workbench (scaffold + guidance + guide).");
        initCommand.Options.Add(scaffoldForceOption);
        initCommand.Options.Add(initNonInteractiveOption);
        initCommand.Options.Add(initSkipGuideOption);
        initCommand.Options.Add(initFrontMatterOption);
        initCommand.Options.Add(initConfigureOpenAiOption);
        initCommand.Options.Add(initCredentialStoreOption);
        initCommand.Options.Add(initCredentialPathOption);
        initCommand.Options.Add(initOpenAiProviderOption);
        initCommand.Options.Add(initOpenAiKeyOption);
        initCommand.Options.Add(initOpenAiModelOption);
        initCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var repoRoot = ResolveRepo(repo);
                var options = new InitWorkflowOptions(
                    Force: parseResult.GetValue(scaffoldForceOption),
                    NonInteractive: parseResult.GetValue(initNonInteractiveOption),
                    SkipWizard: parseResult.GetValue(initSkipGuideOption),
                    SyncFrontMatter: parseResult.GetValue(initFrontMatterOption),
                    ConfigureOpenAi: parseResult.GetValue(initConfigureOpenAiOption),
                    CredentialStore: parseResult.GetValue(initCredentialStoreOption),
                    CredentialPath: parseResult.GetValue(initCredentialPathOption),
                    OpenAiProvider: parseResult.GetValue(initOpenAiProviderOption),
                    OpenAiKey: parseResult.GetValue(initOpenAiKeyOption),
                    OpenAiModel: parseResult.GetValue(initOpenAiModelOption));
                var result = await RunInitWorkflowAsync(repoRoot, options).ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    SetExitCode(result.ExitCode);
                    return;
                }
                if (result.ShouldRunWizard)
                {
                    var guideExit = RunGuide(repoRoot);
                    SetExitCode(guideExit);
                    return;
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(initCommand);

        var scaffoldCommand = new Command("scaffold", "Create the default folder structure, templates, and config.");
        scaffoldCommand.Options.Add(scaffoldForceOption);
        scaffoldCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var force = parseResult.GetValue(scaffoldForceOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var result = ScaffoldService.Scaffold(repoRoot, force);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ScaffoldOutput(
                        true,
                        new ScaffoldData(result.Created, result.Skipped, result.ConfigPath));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ScaffoldOutput);
                }
                else
                {
                    Console.WriteLine("Scaffold complete.");
                    if (result.Created.Count > 0)
                    {
                        Console.WriteLine("Created:");
                        foreach (var path in result.Created)
                        {
                            Console.WriteLine($"- {path}");
                        }
                    }
                    if (result.Skipped.Count > 0)
                    {
                        Console.WriteLine("Skipped:");
                        foreach (var path in result.Skipped)
                        {
                            Console.WriteLine($"- {path}");
                        }
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(scaffoldCommand);

        var configCommand = new Command("config", "Group: configuration commands.");
        var configShowCommand = new Command("show", "Print effective config (defaults + repo config + CLI overrides).");
        configShowCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ConfigOutput(
                        configError is null,
                        new ConfigData(
                            config,
                            new ConfigSources(true, WorkbenchConfig.GetConfigPath(repoRoot))));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ConfigOutput);
                }
                else
                {
                    Console.WriteLine($"Config path: {WorkbenchConfig.GetConfigPath(repoRoot)}");
                    if (configError is not null)
                    {
                        Console.WriteLine($"Config error: {configError}");
                    }
                    Console.WriteLine(JsonSerializer.Serialize(config, Core.WorkbenchJsonContext.Default.WorkbenchConfig));
                }
                SetExitCode(configError is null ? 0 : 2);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        configCommand.Subcommands.Add(configShowCommand);

        var configSetCommand = new Command("set", "Write or update config values in .workbench/config.json.");
        var configSetPathOption = new Option<string>("--path")
        {
            Description = "Config path in dot notation (e.g., paths.docsRoot).",
            Required = true
        };
        var configSetValueOption = new Option<string>("--value")
        {
            Description = "Config value (string by default).",
            Required = true
        };
        var configSetJsonOption = new Option<bool>("--json")
        {
            Description = "Parse the value as JSON (for booleans, numbers, or objects)."
        };
        configSetCommand.Options.Add(configSetPathOption);
        configSetCommand.Options.Add(configSetValueOption);
        configSetCommand.Options.Add(configSetJsonOption);
        configSetCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var path = parseResult.GetValue(configSetPathOption) ?? string.Empty;
                var value = parseResult.GetValue(configSetValueOption) ?? string.Empty;
                var parseJson = parseResult.GetValue(configSetJsonOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var updatedConfig = ConfigService.SetConfigValue(config, path, value, parseJson, out var changed);
                var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
                if (changed || !File.Exists(configPath))
                {
                    ConfigService.SaveConfig(repoRoot, updatedConfig);
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ConfigSetOutput(
                        true,
                        new ConfigSetData(configPath, updatedConfig, changed));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ConfigSetOutput);
                }
                else
                {
                    var changeLabel = changed ? "Updated" : "No change";
                    Console.WriteLine($"{changeLabel} config at {configPath}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        configCommand.Subcommands.Add(configSetCommand);

        var configCredentialsCommand = new Command("credentials", "Manage credentials.env entries.");
        var credentialsPathOption = new Option<string?>("--path")
        {
            Description = "Credentials file path (defaults to .workbench/credentials.env)."
        };

        var credentialsSetCommand = new Command("set", "Set an entry in credentials.env.");
        var credentialsSetKeyOption = new Option<string>("--key")
        {
            Description = "Environment variable name.",
            Required = true
        };
        var credentialsSetValueOption = new Option<string>("--value")
        {
            Description = "Environment variable value.",
            Required = true
        };
        credentialsSetCommand.Options.Add(credentialsSetKeyOption);
        credentialsSetCommand.Options.Add(credentialsSetValueOption);
        credentialsSetCommand.Options.Add(credentialsPathOption);
        credentialsSetCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var key = parseResult.GetValue(credentialsSetKeyOption) ?? string.Empty;
                var value = parseResult.GetValue(credentialsSetValueOption) ?? string.Empty;
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var path = parseResult.GetValue(credentialsPathOption);
                var targetPath = string.IsNullOrWhiteSpace(path)
                    ? Path.Combine(repoRoot, ".workbench", "credentials.env")
                    : path!;

                var result = EnvFileService.SetValue(targetPath, key, value);
                if (IsPathInsideRepo(repoRoot, targetPath))
                {
                    EnsureGitignoreEntry(repoRoot, NormalizeRepoPath(repoRoot, targetPath));
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new CredentialUpdateOutput(
                        true,
                        new CredentialUpdateData(result.Path, result.Key, result.Created, result.Updated, result.Removed));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.CredentialUpdateOutput);
                }
                else
                {
                    var message = result.Updated || result.Created ? "updated" : "no change";
                    Console.WriteLine($"{key} {message} in {result.Path}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        var credentialsUnsetCommand = new Command("unset", "Remove an entry from credentials.env.");
        var credentialsUnsetKeyOption = new Option<string>("--key")
        {
            Description = "Environment variable name.",
            Required = true
        };
        credentialsUnsetCommand.Options.Add(credentialsUnsetKeyOption);
        credentialsUnsetCommand.Options.Add(credentialsPathOption);
        credentialsUnsetCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var key = parseResult.GetValue(credentialsUnsetKeyOption) ?? string.Empty;
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var path = parseResult.GetValue(credentialsPathOption);
                var targetPath = string.IsNullOrWhiteSpace(path)
                    ? Path.Combine(repoRoot, ".workbench", "credentials.env")
                    : path!;

                var result = EnvFileService.UnsetValue(targetPath, key);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new CredentialUpdateOutput(
                        true,
                        new CredentialUpdateData(result.Path, result.Key, result.Created, result.Updated, result.Removed));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.CredentialUpdateOutput);
                }
                else
                {
                    var message = result.Removed ? "removed" : "not found";
                    Console.WriteLine($"{key} {message} in {result.Path}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        configCredentialsCommand.Subcommands.Add(credentialsSetCommand);
        configCredentialsCommand.Subcommands.Add(credentialsUnsetCommand);
        configCommand.Subcommands.Add(configCredentialsCommand);
        root.Subcommands.Add(configCommand);

        var itemCommand = new Command("item", "Group: work item commands.");

        var itemTypeOption = new Option<string>("--type")
        {
            Description = "Work item type: bug, task, spike",
            Required = true
        };
        itemTypeOption.CompletionSources.Add("bug", "task", "spike");

        static Option<string> CreateTitleOption()
        {
            return new Option<string>("--title")
            {
                Description = "Work item title",
                Required = true
            };
        }

        static Option<string?> CreateStatusOption()
        {
            var option = new Option<string?>("--status")
            {
                Description = "Work item status: draft, ready, in-progress, blocked, done, dropped"
            };
            option.CompletionSources.Add("draft", "ready", "in-progress", "blocked", "done", "dropped");
            return option;
        }

        static Option<string?> CreatePriorityOption()
        {
            var option = new Option<string?>("--priority")
            {
                Description = "Work item priority"
            };
            option.CompletionSources.Add("low", "medium", "high", "critical");
            return option;
        }

        static Option<string?> CreateOwnerOption()
        {
            return new Option<string?>("--owner")
            {
                Description = "Work item owner"
            };
        }

        var itemNewCommand = new Command("new", "Create a new work item in docs/70-work/items using templates and ID allocation.");
        var itemTitleOption = CreateTitleOption();
        var itemStatusOption = CreateStatusOption();
        var itemPriorityOption = CreatePriorityOption();
        var itemOwnerOption = CreateOwnerOption();
        itemNewCommand.Options.Add(itemTypeOption);
        itemNewCommand.Options.Add(itemTitleOption);
        itemNewCommand.Options.Add(itemStatusOption);
        itemNewCommand.Options.Add(itemPriorityOption);
        itemNewCommand.Options.Add(itemOwnerOption);
        itemNewCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var type = parseResult.GetValue(itemTypeOption) ?? string.Empty;
                var title = parseResult.GetValue(itemTitleOption) ?? string.Empty;
                var status = parseResult.GetValue(itemStatusOption);
                var priority = parseResult.GetValue(itemPriorityOption);
                var owner = parseResult.GetValue(itemOwnerOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var result = WorkItemService.CreateItem(repoRoot, config, type, title, status, priority, owner);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemCreateOutput(
                        true,
                        new ItemCreateData(result.Id, result.Slug, result.Path));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemCreateOutput);
                }
                else
                {
                    Console.WriteLine($"{result.Id} created at {result.Path}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemNewCommand);

        var itemGenerateCommand = new Command("generate", "Generate a work item draft with AI and create it.");
        var itemGenerateTypeOption = new Option<string?>("--type")
        {
            Description = "Work item type: bug, task, spike (defaults to AI choice)."
        };
        itemGenerateTypeOption.CompletionSources.Add("bug", "task", "spike");
        var itemGeneratePromptOption = new Option<string[]>("--prompt")
        {
            Description = "Freeform description for the AI-generated work item.",
            Required = true,
            AllowMultipleArgumentsPerToken = true
        };
        var itemGenerateStatusOption = CreateStatusOption();
        var itemGeneratePriorityOption = CreatePriorityOption();
        var itemGenerateOwnerOption = CreateOwnerOption();
        itemGenerateCommand.Options.Add(itemGeneratePromptOption);
        itemGenerateCommand.Options.Add(itemGenerateTypeOption);
        itemGenerateCommand.Options.Add(itemGenerateStatusOption);
        itemGenerateCommand.Options.Add(itemGeneratePriorityOption);
        itemGenerateCommand.Options.Add(itemGenerateOwnerOption);
        itemGenerateCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var promptParts = parseResult.GetValue(itemGeneratePromptOption) ?? Array.Empty<string>();
                var typeOverride = parseResult.GetValue(itemGenerateTypeOption);
                var status = parseResult.GetValue(itemGenerateStatusOption);
                var priority = parseResult.GetValue(itemGeneratePriorityOption);
                var owner = parseResult.GetValue(itemGenerateOwnerOption);

                var prompt = string.Join(" ", promptParts).Trim();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    Console.WriteLine("Prompt is required.");
                    SetExitCode(2);
                    return;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                if (!AiWorkItemClient.TryCreate(out var client, out var reason))
                {
                    Console.WriteLine($"AI work item generation disabled: {reason}");
                    SetExitCode(2);
                    return;
                }

                var draft = await client!.GenerateDraftAsync(prompt).ConfigureAwait(false);
                if (draft == null || string.IsNullOrWhiteSpace(draft.Title))
                {
                    Console.WriteLine("AI did not return a valid work item draft.");
                    SetExitCode(2);
                    return;
                }

                var type = ResolveWorkItemType(typeOverride, draft.Type);
                var result = WorkItemService.CreateItem(repoRoot, config, type, draft.Title, status, priority, owner);
                WorkItemService.ApplyDraft(result.Path, draft);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemCreateOutput(
                        true,
                        new ItemCreateData(result.Id, result.Slug, result.Path));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemCreateOutput);
                }
                else
                {
                    Console.WriteLine($"{result.Id} created at {result.Path}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemGenerateCommand);

        var itemImportCommand = new Command("import", "Import GitHub issues into work items.");
        var importIssueOption = new Option<string[]>("--issue")
        {
            Description = "Issue numbers or URLs to import.",
            Required = true
        };
        var importTypeOption = new Option<string?>("--type")
        {
            Description = "Work item type: bug, task, spike (defaults based on labels)."
        };
        importTypeOption.CompletionSources.Add("bug", "task", "spike");
        var importStatusOption = CreateStatusOption();
        var importPriorityOption = CreatePriorityOption();
        var importOwnerOption = CreateOwnerOption();
        itemImportCommand.Options.Add(importIssueOption);
        itemImportCommand.Options.Add(importTypeOption);
        itemImportCommand.Options.Add(importStatusOption);
        itemImportCommand.Options.Add(importPriorityOption);
        itemImportCommand.Options.Add(importOwnerOption);
        itemImportCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var issueInputs = parseResult.GetValue(importIssueOption) ?? Array.Empty<string>();
                var typeOverride = parseResult.GetValue(importTypeOption);
                var statusOverride = parseResult.GetValue(importStatusOption);
                var priority = parseResult.GetValue(importPriorityOption);
                var owner = parseResult.GetValue(importOwnerOption);
                if (issueInputs.Length == 0)
                {
                    Console.WriteLine("No issues provided.");
                    SetExitCode(2);
                    return;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var defaultRepo = GithubService.ResolveRepo(repoRoot, config);
                var imported = new List<ItemImportEntry>();

                foreach (var input in issueInputs)
                {
                    var issueRef = GithubService.ParseIssueReference(input, defaultRepo);
                    var issue = await GithubService.FetchIssueAsync(repoRoot, config, issueRef).ConfigureAwait(false);
                    var type = ResolveIssueType(issue, typeOverride);
                    var status = ResolveIssueStatus(issue, statusOverride);
                    var item = WorkItemService.CreateItemFromGithubIssue(repoRoot, config, issue, type, status, priority, owner);
                    var issuePayload = new GithubIssuePayload(
                        issue.Repo.Display,
                        issue.Number,
                        issue.Url,
                        issue.Title,
                        issue.State,
                        issue.Labels,
                        issue.PullRequests);
                    imported.Add(new ItemImportEntry(issuePayload, ItemToPayload(item)));
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemImportOutput(
                        true,
                        new ItemImportData(imported));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemImportOutput);
                }
                else
                {
                    foreach (var entry in imported)
                    {
                        Console.WriteLine($"Imported {entry.Issue.Repo}#{entry.Issue.Number}: {entry.Item.Id} - {entry.Item.Title}");
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemImportCommand);

        var itemSyncCommand = new Command("sync", "Sync work items with GitHub issues and branches.");
        var syncIdOption = new Option<string[]>("--id")
        {
            Description = "Work item IDs to sync (limits local-to-GitHub and branch creation)."
        };
        var syncIssueOption = new Option<string[]>("--issue")
        {
            Description = "Issue numbers or URLs to import (limits GitHub-to-local)."
        };
        var syncPreferOption = new Option<string?>("--prefer")
        {
            Description = "When descriptions differ, prefer 'local' or 'github'."
        };
        syncPreferOption.CompletionSources.Add("local", "github");
        var itemSyncDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing."
        };
        var itemSyncImportIssuesOption = new Option<bool>("--import-issues")
        {
            Description = "List GitHub issues and import ones not yet linked (slower)."
        };
        itemSyncCommand.Options.Add(syncIdOption);
        itemSyncCommand.Options.Add(syncIssueOption);
        itemSyncCommand.Options.Add(syncPreferOption);
        itemSyncCommand.Options.Add(itemSyncDryRunOption);
        itemSyncCommand.Options.Add(itemSyncImportIssuesOption);
        itemSyncCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var ids = parseResult.GetValue(syncIdOption) ?? Array.Empty<string>();
                var issueInputs = parseResult.GetValue(syncIssueOption) ?? Array.Empty<string>();
                var prefer = parseResult.GetValue(syncPreferOption);
                var dryRun = parseResult.GetValue(itemSyncDryRunOption);
                var importIssues = parseResult.GetValue(itemSyncImportIssuesOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var data = await RunItemSyncAsync(repoRoot, config, ids, issueInputs, importIssues, prefer, dryRun, syncIssues: true).ConfigureAwait(false);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemSyncOutput(
                        true,
                        data);
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemSyncOutput);
                }
                else
                {
                    foreach (var entry in data.Imported)
                    {
                        if (entry.Item is null)
                        {
                            Console.WriteLine($"Would import {entry.Issue.Repo}#{entry.Issue.Number}: {entry.Issue.Title}");
                        }
                        else
                        {
                            Console.WriteLine($"Imported {entry.Issue.Repo}#{entry.Issue.Number}: {entry.Item.Id} - {entry.Item.Title}");
                        }
                    }

                    foreach (var issue in data.IssuesCreated)
                    {
                        var suffix = string.IsNullOrWhiteSpace(issue.IssueUrl) ? "Would create issue" : issue.IssueUrl;
                        Console.WriteLine($"{suffix} for {issue.ItemId}.");
                    }

                    foreach (var issue in data.IssuesUpdated)
                    {
                        var suffix = string.IsNullOrWhiteSpace(issue.IssueUrl) ? "Would update issue" : issue.IssueUrl;
                        Console.WriteLine($"{suffix} for {issue.ItemId}.");
                    }

                    foreach (var item in data.ItemsUpdated)
                    {
                        var suffix = string.IsNullOrWhiteSpace(item.IssueUrl) ? "Would update item" : item.IssueUrl;
                        Console.WriteLine($"{suffix} for {item.ItemId}.");
                    }

                    foreach (var branch in data.BranchesCreated)
                    {
                        var suffix = data.DryRun ? "Would create branch" : "Created and pushed branch";
                        Console.WriteLine($"{suffix} {branch.Branch} for {branch.ItemId}.");
                    }

                    if (data.Conflicts.Count > 0)
                    {
                        Console.WriteLine("Conflicts:");
                        foreach (var conflict in data.Conflicts)
                        {
                            Console.WriteLine($"- {conflict.ItemId}: {conflict.Reason} ({conflict.IssueUrl})");
                        }
                    }
                }

                SetExitCode(data.Conflicts.Count > 0 ? 2 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemSyncCommand);

        var itemListCommand = new Command("list", "List work items.");
        var listTypeOption = new Option<string>("--type")
        {
            Description = "Filter by type"
        };
        listTypeOption.CompletionSources.Add("bug", "task", "spike");
        var listStatusOption = new Option<string>("--status")
        {
            Description = "Filter by status: draft, ready, in-progress, blocked, done, dropped"
        };
        listStatusOption.CompletionSources.Add("draft", "ready", "in-progress", "blocked", "done", "dropped");
        var includeDoneOption = new Option<bool>("--include-done")
        {
            Description = "Include items from docs/70-work/done."
        };
        itemListCommand.Options.Add(listTypeOption);
        itemListCommand.Options.Add(listStatusOption);
        itemListCommand.Options.Add(includeDoneOption);
        itemListCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var type = parseResult.GetValue(listTypeOption);
                var status = parseResult.GetValue(listStatusOption);
                var includeDone = parseResult.GetValue(includeDoneOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var list = WorkItemService.ListItems(repoRoot, config, includeDone);
                var items = list.Items;
                if (!string.IsNullOrWhiteSpace(type))
                {
                    items = items.Where(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                if (!string.IsNullOrWhiteSpace(status))
                {
                    items = items.Where(item => item.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payloadItems = items
                        .Select(item => new ItemSummary(item.Id, item.Type, item.Status, item.Title, item.Path))
                        .ToList();
                    var payload = new ItemListOutput(
                        true,
                        new ItemListData(payloadItems));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemListOutput);
                }
                else
                {
                    foreach (var item in items.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"{item.Id}\t{item.Status}\t{item.Title}");
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemListCommand);

        var itemShowCommand = new Command("show", "Show metadata and resolved path for an item.");
        var itemIdArg = new Argument<string>("id")
        {
            Description = "Work item ID (e.g., TASK-0042)."
        };
        itemShowCommand.Arguments.Add(itemIdArg);
        itemShowCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(itemIdArg) ?? string.Empty;
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemShowOutput(
                        true,
                        new ItemShowData(ItemToPayload(item)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemShowOutput);
                }
                else
                {
                    Console.WriteLine($"{item.Id} - {item.Title}");
                    Console.WriteLine($"Type: {item.Type}");
                    Console.WriteLine($"Status: {item.Status}");
                    Console.WriteLine($"Path: {item.Path}");
                    PrintRelatedLinks("Specs", item.Related.Specs);
                    PrintRelatedLinks("ADRs", item.Related.Adrs);
                    PrintRelatedLinks("Files", item.Related.Files);
                    PrintRelatedLinks("PRs", item.Related.Prs);
                    PrintRelatedLinks("Issues", item.Related.Issues);
                    PrintRelatedLinks("Branches", item.Related.Branches);
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemShowCommand);

        var itemStatusCommand = new Command("status", "Update status and updated date.");
        var statusIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var statusValueArg = new Argument<string>("status")
        {
            Description = "New status: draft, ready, in-progress, blocked, done, dropped."
        };
        var noteOption = new Option<string?>("--note")
        {
            Description = "Append a note."
        };
        itemStatusCommand.Arguments.Add(statusIdArg);
        itemStatusCommand.Arguments.Add(statusValueArg);
        itemStatusCommand.Options.Add(noteOption);
        itemStatusCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(statusIdArg) ?? string.Empty;
                var status = parseResult.GetValue(statusValueArg) ?? string.Empty;
                var note = parseResult.GetValue(noteOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = WorkItemService.UpdateStatus(path, status, note);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemStatusOutput(
                        true,
                        new ItemStatusData(ItemToPayload(updated)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemStatusOutput);
                }
                else
                {
                    Console.WriteLine($"{updated.Id} status updated to {updated.Status}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemStatusCommand);

        var itemCloseCommand = new Command("close", "Set status to done and move to docs/70-work/done.");
        var closeIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var noMoveOption = new Option<bool>("--no-move")
        {
            Description = "Do not move the item to docs/70-work/done."
        };
        itemCloseCommand.Arguments.Add(closeIdArg);
        itemCloseCommand.Options.Add(noMoveOption);
        itemCloseCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(closeIdArg) ?? string.Empty;
                var move = !parseResult.GetValue(noMoveOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = WorkItemService.Close(path, move, config, repoRoot);
                if (move)
                {
                    var oldPath = path;
                    var newPath = updated.Path;
                    if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        LinkUpdater.UpdateLinks(repoRoot, oldPath, newPath);
                    }
                }
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemCloseOutput(
                        true,
                        new ItemCloseData(ItemToPayload(updated), move));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemCloseOutput);
                }
                else
                {
                    Console.WriteLine($"{updated.Id} closed.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemCloseCommand);

        var itemMoveCommand = new Command("move", "Move a work item file and update inbound links where possible.");
        var moveIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var moveToOption = new Option<string>("--to")
        {
            Description = "Destination path.",
            Required = true
        };
        itemMoveCommand.Arguments.Add(moveIdArg);
        itemMoveCommand.Options.Add(moveToOption);
        itemMoveCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(moveIdArg) ?? string.Empty;
                var to = parseResult.GetValue(moveToOption) ?? string.Empty;
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = WorkItemService.Move(path, to, repoRoot);
                LinkUpdater.UpdateLinks(repoRoot, path, updated.Path);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemMoveOutput(
                        true,
                        new ItemMoveData(ItemToPayload(updated)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemMoveOutput);
                }
                else
                {
                    Console.WriteLine($"{updated.Id} moved to {updated.Path}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemMoveCommand);

        var itemRenameCommand = new Command("rename", "Regenerate slug from title, rename the file, and update inbound links.");
        var renameIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var renameTitleOption = new Option<string>("--title")
        {
            Description = "New title.",
            Required = true
        };
        itemRenameCommand.Arguments.Add(renameIdArg);
        itemRenameCommand.Options.Add(renameTitleOption);
        itemRenameCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(renameIdArg) ?? string.Empty;
                var title = parseResult.GetValue(renameTitleOption) ?? string.Empty;
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = WorkItemService.Rename(path, title, config, repoRoot);
                LinkUpdater.UpdateLinks(repoRoot, path, updated.Path);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemRenameOutput(
                        true,
                        new ItemRenameData(ItemToPayload(updated)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemRenameOutput);
                }
                else
                {
                    Console.WriteLine($"{updated.Id} renamed to {updated.Path}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemRenameCommand);

        var itemNormalizeCommand = new Command("normalize", "Normalize work item front matter lists.");
        var normalizeItemsIncludeDoneOption = new Option<bool>("--include-done")
        {
            Description = "Include docs/70-work/done items."
        };
        var normalizeAllDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        itemNormalizeCommand.Options.Add(normalizeItemsIncludeDoneOption);
        itemNormalizeCommand.Options.Add(normalizeAllDryRunOption);
        itemNormalizeCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var includeDone = parseResult.GetValue(normalizeItemsIncludeDoneOption);
                var dryRun = parseResult.GetValue(normalizeAllDryRunOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                TryMigrateLegacyWorkLayout(repoRoot, config, dryRun);
                var updated = WorkItemService.NormalizeItems(repoRoot, config, includeDone, dryRun);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemNormalizeOutput(
                        true,
                        new ItemNormalizeData(updated, dryRun));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemNormalizeOutput);
                }
                else
                {
                    var mode = dryRun ? "would update" : "updated";
                    Console.WriteLine($"{updated} work item(s) {mode}.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemNormalizeCommand);

        var itemDeleteCommand = new Command("delete", "Delete a work item file and update doc backlinks.");
        var deleteIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var keepLinksOption = new Option<bool>("--keep-links")
        {
            Description = "Skip removing doc backlinks."
        };
        itemDeleteCommand.Arguments.Add(deleteIdArg);
        itemDeleteCommand.Options.Add(keepLinksOption);
        itemDeleteCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(deleteIdArg) ?? string.Empty;
                var keepLinks = parseResult.GetValue(keepLinksOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var path = WorkItemService.GetItemPathById(repoRoot, config, id);
                var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Work item not found.");
                var docsUpdated = 0;
                if (!keepLinks)
                {
                    foreach (var link in item.Related.Specs)
                    {
                        if (DocService.TryUpdateDocWorkItemLink(repoRoot, config, link, item.Id, add: false, apply: true))
                        {
                            docsUpdated++;
                        }
                    }
                    foreach (var link in item.Related.Adrs)
                    {
                        if (DocService.TryUpdateDocWorkItemLink(repoRoot, config, link, item.Id, add: false, apply: true))
                        {
                            docsUpdated++;
                        }
                    }
                }

                File.Delete(path);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemDeleteOutput(
                        true,
                        new ItemDeleteData(ItemToPayload(item), docsUpdated));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemDeleteOutput);
                }
                else
                {
                    Console.WriteLine($"{item.Id} deleted.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemDeleteCommand);

        var itemLinkCommand = new Command("link", "Link specs, ADRs, files, PRs, or issues to a work item.");
        var linkIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var linkSpecOption = new Option<string[]>("--spec")
        {
            Description = "Spec path(s) to link.",
            AllowMultipleArgumentsPerToken = true
        };
        var linkAdrOption = new Option<string[]>("--adr")
        {
            Description = "ADR path(s) to link.",
            AllowMultipleArgumentsPerToken = true
        };
        var linkFileOption = new Option<string[]>("--file")
        {
            Description = "File path(s) to link.",
            AllowMultipleArgumentsPerToken = true
        };
        var linkPrOption = new Option<string[]>("--pr")
        {
            Description = "PR URL(s) to link.",
            AllowMultipleArgumentsPerToken = true
        };
        var linkIssueOption = new Option<string[]>("--issue")
        {
            Description = "Issue URL(s) or IDs to link.",
            AllowMultipleArgumentsPerToken = true
        };
        itemLinkCommand.Arguments.Add(linkIdArg);
        itemLinkCommand.Options.Add(linkSpecOption);
        itemLinkCommand.Options.Add(linkAdrOption);
        itemLinkCommand.Options.Add(linkFileOption);
        itemLinkCommand.Options.Add(linkPrOption);
        itemLinkCommand.Options.Add(linkIssueOption);
        var linkDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        itemLinkCommand.Options.Add(linkDryRunOption);
        itemLinkCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(linkIdArg) ?? string.Empty;
                var specs = parseResult.GetValue(linkSpecOption) ?? Array.Empty<string>();
                var adrs = parseResult.GetValue(linkAdrOption) ?? Array.Empty<string>();
                var files = parseResult.GetValue(linkFileOption) ?? Array.Empty<string>();
                var prs = parseResult.GetValue(linkPrOption) ?? Array.Empty<string>();
                var issues = parseResult.GetValue(linkIssueOption) ?? Array.Empty<string>();
                var dryRun = parseResult.GetValue(linkDryRunOption);

                if (specs.Length + adrs.Length + files.Length + prs.Length + issues.Length == 0)
                {
                    Console.WriteLine("No links provided.");
                    SetExitCode(2);
                    return;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = false;

                foreach (var spec in specs)
                {
                    var normalized = NormalizeRepoLink(repoRoot, spec);
                    if (WorkItemService.AddRelatedLink(itemPath, "specs", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: true, apply: !dryRun);
                }

                foreach (var adr in adrs)
                {
                    var normalized = NormalizeRepoLink(repoRoot, adr);
                    if (WorkItemService.AddRelatedLink(itemPath, "adrs", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: true, apply: !dryRun);
                }

                foreach (var file in files)
                {
                    var normalized = NormalizeRepoLink(repoRoot, file);
                    if (WorkItemService.AddRelatedLink(itemPath, "files", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: true, apply: !dryRun);
                }

                if (prs.Any(pr => WorkItemService.AddRelatedLink(itemPath, "prs", pr, apply: !dryRun)))
                {
                    updated = true;
                }

                if (issues.Any(issue => WorkItemService.AddRelatedLink(itemPath, "issues", issue, apply: !dryRun)))
                {
                    updated = true;
                }

                var item = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Invalid work item.");
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemShowOutput(
                        true,
                        new ItemShowData(ItemToPayload(item)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemShowOutput);
                }
                else if (updated)
                {
                    Console.WriteLine($"{item.Id} links updated.");
                    if (dryRun)
                    {
                        Console.WriteLine("Dry run: no files were modified.");
                    }
                }
                else
                {
                    Console.WriteLine($"{item.Id} already had the requested links.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemLinkCommand);

        var itemUnlinkCommand = new Command("unlink", "Remove specs, ADRs, files, PRs, or issues from a work item.");
        var unlinkIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var unlinkSpecOption = new Option<string[]>("--spec")
        {
            Description = "Spec path(s) to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        var unlinkAdrOption = new Option<string[]>("--adr")
        {
            Description = "ADR path(s) to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        var unlinkFileOption = new Option<string[]>("--file")
        {
            Description = "File path(s) to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        var unlinkPrOption = new Option<string[]>("--pr")
        {
            Description = "PR URL(s) to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        var unlinkIssueOption = new Option<string[]>("--issue")
        {
            Description = "Issue URL(s) or IDs to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        itemUnlinkCommand.Arguments.Add(unlinkIdArg);
        itemUnlinkCommand.Options.Add(unlinkSpecOption);
        itemUnlinkCommand.Options.Add(unlinkAdrOption);
        itemUnlinkCommand.Options.Add(unlinkFileOption);
        itemUnlinkCommand.Options.Add(unlinkPrOption);
        itemUnlinkCommand.Options.Add(unlinkIssueOption);
        var unlinkDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        itemUnlinkCommand.Options.Add(unlinkDryRunOption);
        itemUnlinkCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var id = parseResult.GetValue(unlinkIdArg) ?? string.Empty;
                var specs = parseResult.GetValue(unlinkSpecOption) ?? Array.Empty<string>();
                var adrs = parseResult.GetValue(unlinkAdrOption) ?? Array.Empty<string>();
                var files = parseResult.GetValue(unlinkFileOption) ?? Array.Empty<string>();
                var prs = parseResult.GetValue(unlinkPrOption) ?? Array.Empty<string>();
                var issues = parseResult.GetValue(unlinkIssueOption) ?? Array.Empty<string>();
                var dryRun = parseResult.GetValue(unlinkDryRunOption);

                if (specs.Length + adrs.Length + files.Length + prs.Length + issues.Length == 0)
                {
                    Console.WriteLine("No links provided.");
                    SetExitCode(2);
                    return;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, id);
                var updated = false;

                foreach (var spec in specs)
                {
                    var normalized = NormalizeRepoLink(repoRoot, spec);
                    if (WorkItemService.RemoveRelatedLink(itemPath, "specs", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: false, apply: !dryRun);
                }

                foreach (var adr in adrs)
                {
                    var normalized = NormalizeRepoLink(repoRoot, adr);
                    if (WorkItemService.RemoveRelatedLink(itemPath, "adrs", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: false, apply: !dryRun);
                }

                foreach (var file in files)
                {
                    var normalized = NormalizeRepoLink(repoRoot, file);
                    if (WorkItemService.RemoveRelatedLink(itemPath, "files", normalized, apply: !dryRun))
                    {
                        updated = true;
                    }
                    DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalized, id, add: false, apply: !dryRun);
                }

                if (prs.Any(pr => WorkItemService.RemoveRelatedLink(itemPath, "prs", pr, apply: !dryRun)))
                {
                    updated = true;
                }

                if (issues.Any(issue => WorkItemService.RemoveRelatedLink(itemPath, "issues", issue, apply: !dryRun)))
                {
                    updated = true;
                }

                var item = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Invalid work item.");
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemShowOutput(
                        true,
                        new ItemShowData(ItemToPayload(item)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemShowOutput);
                }
                else if (updated)
                {
                    Console.WriteLine($"{item.Id} links updated.");
                    if (dryRun)
                    {
                        Console.WriteLine("Dry run: no files were modified.");
                    }
                }
                else
                {
                    Console.WriteLine($"{item.Id} already lacked the requested links.");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        itemCommand.Subcommands.Add(itemUnlinkCommand);

        root.Subcommands.Add(itemCommand);

        var normalizeCommand = new Command("normalize", "Normalize work item and doc front matter.");
        var normalizeItemsOption = new Option<bool>("--items")
        {
            Description = "Normalize work item front matter."
        };
        var normalizeDocsOption = new Option<bool>("--docs")
        {
            Description = "Normalize doc front matter."
        };
        var normalizeAllDocsOption = new Option<bool>("--all-docs")
        {
            Description = "Add Workbench front matter to all docs (default)."
        };
        normalizeCommand.Options.Add(normalizeItemsOption);
        normalizeCommand.Options.Add(normalizeDocsOption);
        normalizeCommand.Options.Add(normalizeItemsIncludeDoneOption);
        normalizeCommand.Options.Add(normalizeAllDocsOption);
        normalizeCommand.Options.Add(normalizeAllDryRunOption);
        normalizeCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var includeDone = parseResult.GetValue(normalizeItemsIncludeDoneOption);
                var includeAllDocs = parseResult.GetValue(normalizeAllDocsOption);
                if (parseResult.GetResult(normalizeAllDocsOption) is null)
                {
                    includeAllDocs = true;
                }
                var dryRun = parseResult.GetValue(normalizeAllDryRunOption);
                var normalizeItems = parseResult.GetValue(normalizeItemsOption);
                var normalizeDocs = parseResult.GetValue(normalizeDocsOption);
                if (!normalizeItems && !normalizeDocs)
                {
                    normalizeItems = true;
                    normalizeDocs = true;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                TryMigrateLegacyWorkLayout(repoRoot, config, dryRun);
                var itemsUpdated = normalizeItems
                    ? WorkItemService.NormalizeItems(repoRoot, config, includeDone, dryRun)
                    : 0;
                var docsUpdated = normalizeDocs
                    ? DocService.NormalizeDocs(repoRoot, config, includeAllDocs, dryRun)
                    : 0;

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new NormalizeOutput(
                        true,
                        new NormalizeData(itemsUpdated, docsUpdated, dryRun, normalizeItems, normalizeDocs));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.NormalizeOutput);
                }
                else
                {
                    var mode = dryRun ? "would update" : "updated";
                    if (normalizeItems)
                    {
                        Console.WriteLine($"Work items {mode}: {itemsUpdated}");
                    }
                    if (normalizeDocs)
                    {
                        Console.WriteLine($"Docs {mode}: {docsUpdated}");
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(normalizeCommand);

        var boardCommand = new Command("board", "Group: workboard commands.");
        var boardRegenCommand = new Command("regen", "Regenerate docs/70-work/README.md.");
        boardRegenCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var result = WorkboardService.Regenerate(repoRoot, config);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new BoardOutput(
                        true,
                        new BoardData(result.Path, result.Counts));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.BoardOutput);
                }
                else
                {
                    Console.WriteLine($"Workboard regenerated: {result.Path}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        boardCommand.Subcommands.Add(boardRegenCommand);
        root.Subcommands.Add(boardCommand);

        var promoteCommand = new Command("promote", "Create a work item, branch, and commit in one step.");
        var promoteTypeOption = new Option<string>("--type")
        {
            Description = "Work item type: bug, task, spike",
            Required = true
        };
        promoteTypeOption.CompletionSources.Add("bug", "task", "spike");
        var promoteTitleOption = new Option<string>("--title")
        {
            Description = "Work item title",
            Required = true
        };
        var promotePushOption = new Option<bool>("--push")
        {
            Description = "Push the branch to origin."
        };
        var promoteStartOption = new Option<bool>("--start")
        {
            Description = "Set status to in-progress."
        };
        var promotePrOption = new Option<bool>("--pr")
        {
            Description = "Create a GitHub PR."
        };
        var promoteBaseOption = new Option<string?>("--base")
        {
            Description = "Base branch for PR."
        };
        var promoteDraftOption = new Option<bool>("--draft")
        {
            Description = "Create a draft PR."
        };
        var promoteNoDraftOption = new Option<bool>("--no-draft")
        {
            Description = "Create a ready PR."
        };
        promoteCommand.Options.Add(promoteTypeOption);
        promoteCommand.Options.Add(promoteTitleOption);
        promoteCommand.Options.Add(promotePushOption);
        promoteCommand.Options.Add(promoteStartOption);
        promoteCommand.Options.Add(promotePrOption);
        promoteCommand.Options.Add(promoteBaseOption);
        promoteCommand.Options.Add(promoteDraftOption);
        promoteCommand.Options.Add(promoteNoDraftOption);
        promoteCommand.SetAction(async parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var type = parseResult.GetValue(promoteTypeOption) ?? string.Empty;
            var title = parseResult.GetValue(promoteTitleOption) ?? string.Empty;
            var push = parseResult.GetValue(promotePushOption);
            var start = parseResult.GetValue(promoteStartOption);
            var pr = parseResult.GetValue(promotePrOption);
            var baseBranch = parseResult.GetValue(promoteBaseOption);
            var draft = parseResult.GetValue(promoteDraftOption);
            var noDraft = parseResult.GetValue(promoteNoDraftOption);

            try
            {
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                if (config.Git.RequireCleanWorkingTree && !GitService.IsClean(repoRoot))
                {
                    Console.WriteLine("Working tree is not clean.");
                    SetExitCode(2);
                    return;
                }

                var status = start ? "in-progress" : null;
                var created = WorkItemService.CreateItem(repoRoot, config, type, title, status, null, null);
                var item = WorkItemService.LoadItem(created.Path) ?? throw new InvalidOperationException("Failed to load work item.");

                var branch = ApplyPattern(config.Git.BranchPattern, item);
                GitService.CheckoutNewBranch(repoRoot, branch);
                GitService.Add(repoRoot, created.Path);

                var commitMessage = ApplyPattern(config.Git.CommitMessagePattern, item);
                var sha = GitService.Commit(repoRoot, commitMessage);

                var shouldPush = push || pr;
                if (shouldPush)
                {
                    GitService.Push(repoRoot, branch);
                }

                string? prUrl = null;
                if (pr)
                {
                    var useDraft = draft || (!noDraft && config.Github.DefaultDraft);
                    prUrl = await CreatePrAsync(repoRoot, config, item, baseBranch, useDraft, fill: true).ConfigureAwait(false);
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new PromoteOutput(
                        true,
                        new PromoteData(
                            ItemToPayload(item),
                            branch,
                            new CommitInfo(sha, commitMessage),
                            shouldPush,
                            prUrl));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.PromoteOutput);
                }
                else
                {
                    Console.WriteLine($"{item.Id} promoted on {branch}.");
                    if (prUrl is not null)
                    {
                        Console.WriteLine($"PR: {prUrl}");
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(promoteCommand);

        var prIdArg = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var prBaseOption = new Option<string?>("--base")
        {
            Description = "Base branch for PR."
        };
        var prDraftOption = new Option<bool>("--draft")
        {
            Description = "Create as draft."
        };
        var prFillOption = new Option<bool>("--fill")
        {
            Description = "Fill PR body from work item."
        };

        var githubCommand = new Command("github", "Group: GitHub commands.");
        var githubPrCommand = new Command("pr", "Group: GitHub pull request commands.");
        var githubPrCreateCommand = new Command("create", "Create a GitHub PR via the configured provider and backlink the PR URL.");
        githubPrCreateCommand.Arguments.Add(prIdArg);
        githubPrCreateCommand.Options.Add(prBaseOption);
        githubPrCreateCommand.Options.Add(prDraftOption);
        githubPrCreateCommand.Options.Add(prFillOption);
        githubPrCreateCommand.SetAction(async parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var id = parseResult.GetValue(prIdArg) ?? string.Empty;
            var baseBranch = parseResult.GetValue(prBaseOption);
            var draft = parseResult.GetValue(prDraftOption);
            var fill = parseResult.GetValue(prFillOption);
            await HandlePrCreateAsync(
                repo,
                format,
                id,
                baseBranch,
                draft,
                fill,
                null).ConfigureAwait(false);
        });
        githubPrCommand.Subcommands.Add(githubPrCreateCommand);
        githubCommand.Subcommands.Add(githubPrCommand);
        root.Subcommands.Add(githubCommand);

        var codexCommand = new Command("codex", "Group: Codex agent commands.");
        var codexDoctorCommand = new Command("doctor", "Check whether Codex is installed and callable.");
        codexDoctorCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            HandleCodexDoctor(repo, format);
        });
        codexCommand.Subcommands.Add(codexDoctorCommand);

        var codexRunCommand = new Command("run", "Run Codex in full-auto mode with web search.");
        var codexPromptOption = new Option<string>("--prompt")
        {
            Description = "Prompt to send to Codex.",
            Required = true
        };
        var codexTerminalOption = new Option<bool>("--terminal")
        {
            Description = "Launch in a separate terminal window instead of waiting for output."
        };
        codexRunCommand.Options.Add(codexPromptOption);
        codexRunCommand.Options.Add(codexTerminalOption);
        codexRunCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var prompt = parseResult.GetValue(codexPromptOption) ?? string.Empty;
            var terminal = parseResult.GetValue(codexTerminalOption);
            HandleCodexRun(repo, format, prompt, terminal);
        });
        codexCommand.Subcommands.Add(codexRunCommand);
        root.Subcommands.Add(codexCommand);

        var worktreeCommand = new Command("worktree", "Group: git worktree commands.");
        var worktreeStartCommand = new Command("start", "Create or reuse a task worktree.");
        var worktreeSlugOption = new Option<string>("--slug")
        {
            Description = "Short task slug used for branch and directory naming.",
            Required = true
        };
        var worktreeTicketOption = new Option<int?>("--ticket")
        {
            Description = "Optional numeric ticket to prefix the branch."
        };
        var worktreeBaseOption = new Option<string?>("--base")
        {
            Description = "Base branch for new branches (defaults to config git.defaultBaseBranch)."
        };
        var worktreeRootOption = new Option<string?>("--root")
        {
            Description = "Root directory for worktrees (defaults to <repo>.worktrees)."
        };
        var worktreePromptOption = new Option<string?>("--prompt")
        {
            Description = "Prompt to send when launching Codex."
        };
        var worktreeStartCodexOption = new Option<bool>("--start-codex")
        {
            Description = "Launch Codex after creating/reusing the worktree."
        };
        var worktreeCodexTerminalOption = new Option<bool>("--codex-terminal")
        {
            Description = "When launching Codex, use a separate terminal window."
        };
        worktreeCodexTerminalOption.DefaultValueFactory = _ => true;
        worktreeStartCommand.Options.Add(worktreeSlugOption);
        worktreeStartCommand.Options.Add(worktreeTicketOption);
        worktreeStartCommand.Options.Add(worktreeBaseOption);
        worktreeStartCommand.Options.Add(worktreeRootOption);
        worktreeStartCommand.Options.Add(worktreePromptOption);
        worktreeStartCommand.Options.Add(worktreeStartCodexOption);
        worktreeStartCommand.Options.Add(worktreeCodexTerminalOption);
        worktreeStartCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var slug = parseResult.GetValue(worktreeSlugOption) ?? string.Empty;
            var ticket = parseResult.GetValue(worktreeTicketOption);
            var baseBranch = parseResult.GetValue(worktreeBaseOption);
            var rootPath = parseResult.GetValue(worktreeRootOption);
            var prompt = parseResult.GetValue(worktreePromptOption);
            var startCodex = parseResult.GetValue(worktreeStartCodexOption);
            var codexTerminal = parseResult.GetValue(worktreeCodexTerminalOption);
            HandleWorktreeStart(repo, format, slug, ticket, baseBranch, rootPath, prompt, startCodex, codexTerminal);
        });
        worktreeCommand.Subcommands.Add(worktreeStartCommand);
        root.Subcommands.Add(worktreeCommand);

        var docCommand = new Command("doc", "Group: documentation commands.");

        var docNewCommand = new Command("new", "Create a documentation file with Workbench front matter.");
        var docTypeOption = new Option<string>("--type")
        {
            Description = "Doc type: spec, adr, doc, runbook, guide",
            Required = true
        };
        docTypeOption.CompletionSources.Add("spec", "adr", "doc", "runbook", "guide");
        var docTitleOption = new Option<string>("--title")
        {
            Description = "Doc title",
            Required = true
        };
        var docPathOption = new Option<string?>("--path")
        {
            Description = "Destination path (defaults by type)."
        };
        var docWorkItemOption = new Option<string[]>("--work-item")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var docCodeRefOption = new Option<string[]>("--code-ref")
        {
            AllowMultipleArgumentsPerToken = true
        };
        docWorkItemOption.Description = "Link one or more work items.";
        docCodeRefOption.Description = "Add code reference(s) (e.g., src/Foo.cs#L10-L20).";
        var docForceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing file."
        };

        docNewCommand.Options.Add(docTypeOption);
        docNewCommand.Options.Add(docTitleOption);
        docNewCommand.Options.Add(docPathOption);
        docNewCommand.Options.Add(docWorkItemOption);
        docNewCommand.Options.Add(docCodeRefOption);
        docNewCommand.Options.Add(docForceOption);
        docNewCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var type = parseResult.GetValue(docTypeOption) ?? string.Empty;
            var title = parseResult.GetValue(docTitleOption) ?? string.Empty;
            var path = parseResult.GetValue(docPathOption);
            var workItems = parseResult.GetValue(docWorkItemOption) ?? Array.Empty<string>();
            var codeRefs = parseResult.GetValue(docCodeRefOption) ?? Array.Empty<string>();
            var force = parseResult.GetValue(docForceOption);
            HandleDocCreate(repo, format, type, title, path, workItems, codeRefs, force);
        });

        var docDeleteCommand = new Command("delete", "Delete a documentation file and update work item links.");
        var docDeletePathOption = new Option<string>("--path")
        {
            Description = "Doc path or link.",
            Required = true
        };
        var docDeleteKeepLinksOption = new Option<bool>("--keep-links")
        {
            Description = "Skip removing doc links from work items."
        };
        docDeleteCommand.Options.Add(docDeletePathOption);
        docDeleteCommand.Options.Add(docDeleteKeepLinksOption);
        docDeleteCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var link = parseResult.GetValue(docDeletePathOption) ?? string.Empty;
                var keepLinks = parseResult.GetValue(docDeleteKeepLinksOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var docPath = DocService.ResolveDocPath(repoRoot, link);
                var docFullPath = Path.GetFullPath(docPath);
                if (!File.Exists(docFullPath))
                {
                    Console.WriteLine($"Doc not found: {docFullPath}");
                    SetExitCode(2);
                    return;
                }

                var itemsUpdated = 0;
                if (!keepLinks)
                {
                    var items = WorkItemService.ListItems(repoRoot, config, includeDone: true).Items;
                    foreach (var item in items)
                    {
                        var itemChanged = false;
                        foreach (var spec in item.Related.Specs)
                        {
                            var specPath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, spec));
                            if (specPath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                                && WorkItemService.RemoveRelatedLink(item.Path, "specs", spec))
                            {
                                itemChanged = true;
                            }
                        }
                        foreach (var adr in item.Related.Adrs)
                        {
                            var adrPath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, adr));
                            if (adrPath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                                && WorkItemService.RemoveRelatedLink(item.Path, "adrs", adr))
                            {
                                itemChanged = true;
                            }
                        }
                        foreach (var file in item.Related.Files)
                        {
                            var filePath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, file));
                            if (filePath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                                && WorkItemService.RemoveRelatedLink(item.Path, "files", file))
                            {
                                itemChanged = true;
                            }
                        }
                        if (itemChanged)
                        {
                            itemsUpdated++;
                        }
                    }
                }

                File.Delete(docFullPath);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new DocDeleteOutput(
                        true,
                        new DocDeleteData(docFullPath, itemsUpdated));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.DocDeleteOutput);
                }
                else
                {
                    Console.WriteLine($"Doc deleted: {docFullPath}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        var docLinkCommand = new Command("link", "Link a doc to work items.");
        var docLinkTypeOption = new Option<string>("--type")
        {
            Description = "Doc type: spec, adr",
            Required = true
        };
        docLinkTypeOption.CompletionSources.Add("spec", "adr");
        var docLinkPathOption = new Option<string>("--path")
        {
            Description = "Doc path.",
            Required = true
        };
        var docLinkWorkItemOption = new Option<string[]>("--work-item")
        {
            Description = "Work item ID(s) to link.",
            AllowMultipleArgumentsPerToken = true
        };
        var docLinkDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        docLinkCommand.Options.Add(docLinkTypeOption);
        docLinkCommand.Options.Add(docLinkPathOption);
        docLinkCommand.Options.Add(docLinkWorkItemOption);
        docLinkCommand.Options.Add(docLinkDryRunOption);
        docLinkCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var type = parseResult.GetValue(docLinkTypeOption);
            if (!TryResolveDocLinkType(type, out var resolvedType))
            {
                Console.WriteLine("Doc type must be spec or adr.");
                SetExitCode(2);
                return;
            }
            var path = parseResult.GetValue(docLinkPathOption) ?? string.Empty;
            var workItems = parseResult.GetValue(docLinkWorkItemOption) ?? Array.Empty<string>();
            var dryRun = parseResult.GetValue(docLinkDryRunOption);
            HandleDocLink(repo, format, resolvedType, path, workItems, add: true, dryRun: dryRun);
        });

        var docUnlinkCommand = new Command("unlink", "Unlink a doc from work items.");
        var docUnlinkTypeOption = new Option<string>("--type")
        {
            Description = "Doc type: spec, adr",
            Required = true
        };
        docUnlinkTypeOption.CompletionSources.Add("spec", "adr");
        var docUnlinkPathOption = new Option<string>("--path")
        {
            Description = "Doc path.",
            Required = true
        };
        var docUnlinkWorkItemOption = new Option<string[]>("--work-item")
        {
            Description = "Work item ID(s) to unlink.",
            AllowMultipleArgumentsPerToken = true
        };
        var docUnlinkDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        docUnlinkCommand.Options.Add(docUnlinkTypeOption);
        docUnlinkCommand.Options.Add(docUnlinkPathOption);
        docUnlinkCommand.Options.Add(docUnlinkWorkItemOption);
        docUnlinkCommand.Options.Add(docUnlinkDryRunOption);
        docUnlinkCommand.SetAction(parseResult =>
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var type = parseResult.GetValue(docUnlinkTypeOption);
            if (!TryResolveDocLinkType(type, out var resolvedType))
            {
                Console.WriteLine("Doc type must be spec or adr.");
                SetExitCode(2);
                return;
            }
            var path = parseResult.GetValue(docUnlinkPathOption) ?? string.Empty;
            var workItems = parseResult.GetValue(docUnlinkWorkItemOption) ?? Array.Empty<string>();
            var dryRun = parseResult.GetValue(docUnlinkDryRunOption);
            HandleDocLink(repo, format, resolvedType, path, workItems, add: false, dryRun: dryRun);
        });

        var docSyncCommand = new Command("sync", "Sync doc/work item backlinks.");
        var docSyncAllOption = new Option<bool>("--all")
        {
            Description = "Add Workbench front matter to all docs (default)."
        };
        var docSyncIssuesOption = new Option<bool>("--issues")
        {
            Description = "Sync GitHub issue links for work items."
        };
        var docSyncIncludeDoneOption = new Option<bool>("--include-done")
        {
            Description = "Include done/dropped work items."
        };
        var docSyncDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        docSyncCommand.Options.Add(docSyncAllOption);
        docSyncCommand.Options.Add(docSyncIssuesOption);
        docSyncCommand.Options.Add(docSyncIncludeDoneOption);
        docSyncCommand.Options.Add(docSyncDryRunOption);
        docSyncCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var all = parseResult.GetValue(docSyncAllOption);
                if (parseResult.GetResult(docSyncAllOption) is null)
                {
                    all = true;
                }
                var syncIssues = parseResult.GetValue(docSyncIssuesOption);
                var includeDone = parseResult.GetValue(docSyncIncludeDoneOption);
                var dryRun = parseResult.GetValue(docSyncDryRunOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var result = await DocService.SyncLinksAsync(repoRoot, config, all, syncIssues, includeDone, dryRun).ConfigureAwait(false);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new DocSyncOutput(
                        true,
                        new DocSyncData(
                            result.DocsUpdated,
                            result.ItemsUpdated,
                            result.MissingDocs,
                            result.MissingItems));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.DocSyncOutput);
                }
                else
                {
                    Console.WriteLine($"Docs updated: {result.DocsUpdated}");
                    Console.WriteLine($"Work items updated: {result.ItemsUpdated}");
                    if (result.MissingDocs.Count > 0)
                    {
                        Console.WriteLine("Missing docs:");
                        foreach (var entry in result.MissingDocs)
                        {
                            Console.WriteLine($"- {entry}");
                        }
                    }
                    if (result.MissingItems.Count > 0)
                    {
                        Console.WriteLine("Missing work items:");
                        foreach (var entry in result.MissingItems)
                        {
                            Console.WriteLine($"- {entry}");
                        }
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        var docSummaryCommand = new Command("summarize", "Summarize doc changes with AI and append change notes.");
        var docSummaryStagedOption = new Option<bool>("--staged")
        {
            Description = "Use staged diff (default when no --path is provided)."
        };
        var docSummaryPathOption = new Option<string[]>("--path")
        {
            Description = "File path(s) to summarize (defaults to staged markdown files).",
            AllowMultipleArgumentsPerToken = true
        };
        var docSummaryDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        var docSummaryUpdateIndexOption = new Option<bool>("--update-index")
        {
            Description = "Run git add on updated files."
        };
        docSummaryCommand.Options.Add(docSummaryStagedOption);
        docSummaryCommand.Options.Add(docSummaryPathOption);
        docSummaryCommand.Options.Add(docSummaryDryRunOption);
        docSummaryCommand.Options.Add(docSummaryUpdateIndexOption);
        docSummaryCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var staged = parseResult.GetValue(docSummaryStagedOption);
                var dryRun = parseResult.GetValue(docSummaryDryRunOption);
                var updateIndex = parseResult.GetValue(docSummaryUpdateIndexOption);
                var paths = parseResult.GetValue(docSummaryPathOption) ?? Array.Empty<string>();

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var useStaged = staged;
                if (paths.Length == 0)
                {
                    useStaged = true;
                    paths = GitService.GetStagedFiles(repoRoot).ToArray();
                }

                if (paths.Length == 0)
                {
                    Console.WriteLine("No markdown files to summarize.");
                    SetExitCode(0);
                    return;
                }

                var result = await DocSummaryService.SummarizeDocsAsync(
                        repoRoot,
                        paths,
                        useStaged,
                        dryRun,
                        updateIndex).ConfigureAwait(false);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new DocSummaryOutput(
                        true,
                        new DocSummaryData(
                            result.FilesUpdated,
                            result.NotesAdded,
                            result.UpdatedFiles,
                            result.SkippedFiles,
                            result.Errors,
                            result.Warnings));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.DocSummaryOutput);
                }
                else
                {
                    Console.WriteLine($"Files updated: {result.FilesUpdated}");
                    Console.WriteLine($"Notes added: {result.NotesAdded}");
                    if (result.Warnings.Count > 0)
                    {
                        Console.WriteLine("Warnings:");
                        foreach (var warning in result.Warnings)
                        {
                            Console.WriteLine($"- {warning}");
                        }
                    }
                    if (result.Errors.Count > 0)
                    {
                        Console.WriteLine("Errors:");
                        foreach (var error in result.Errors)
                        {
                            Console.WriteLine($"- {error}");
                        }
                    }
                    if (result.SkippedFiles.Count > 0)
                    {
                        Console.WriteLine("Skipped:");
                        foreach (var skipped in result.SkippedFiles)
                        {
                            Console.WriteLine($"- {skipped}");
                        }
                    }
                }
                SetExitCode(result.Errors.Count > 0 ? 2 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        docCommand.Subcommands.Add(docNewCommand);
        docCommand.Subcommands.Add(docDeleteCommand);
        docCommand.Subcommands.Add(docLinkCommand);
        docCommand.Subcommands.Add(docUnlinkCommand);
        docCommand.Subcommands.Add(docSyncCommand);
        docCommand.Subcommands.Add(docSummaryCommand);
        root.Subcommands.Add(docCommand);

        var voiceCommand = new Command("voice", "Group: voice input commands.");

        var voiceWorkItemCommand = new Command("workitem", "Create a work item from voice input.");
        var voiceWorkItemTypeOption = new Option<string?>("--type")
        {
            Description = "Work item type: bug, task, spike (defaults to AI choice)."
        };
        voiceWorkItemTypeOption.CompletionSources.Add("bug", "task", "spike");
        var voiceWorkItemStatusOption = CreateStatusOption();
        var voiceWorkItemPriorityOption = CreatePriorityOption();
        var voiceWorkItemOwnerOption = CreateOwnerOption();
        voiceWorkItemCommand.Options.Add(voiceWorkItemTypeOption);
        voiceWorkItemCommand.Options.Add(voiceWorkItemStatusOption);
        voiceWorkItemCommand.Options.Add(voiceWorkItemPriorityOption);
        voiceWorkItemCommand.Options.Add(voiceWorkItemOwnerOption);
        voiceWorkItemCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var typeOverride = parseResult.GetValue(voiceWorkItemTypeOption);
                var status = parseResult.GetValue(voiceWorkItemStatusOption);
                var priority = parseResult.GetValue(voiceWorkItemPriorityOption);
                var owner = parseResult.GetValue(voiceWorkItemOwnerOption);

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var voiceConfig = VoiceConfig.Load();
                var transcript = await CaptureVoiceTranscriptAsync(voiceConfig, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    SetExitCode(2);
                    return;
                }

                if (!AiWorkItemClient.TryCreate(out var client, out var reason))
                {
                    Console.WriteLine($"AI work item generation disabled: {reason}");
                    SetExitCode(2);
                    return;
                }

                var draft = await client!.GenerateDraftAsync(transcript).ConfigureAwait(false);
                if (draft == null || string.IsNullOrWhiteSpace(draft.Title))
                {
                    Console.WriteLine("AI did not return a valid work item draft.");
                    SetExitCode(2);
                    return;
                }

                var type = ResolveWorkItemType(typeOverride, draft.Type);
                var result = WorkItemService.CreateItem(repoRoot, config, type, draft.Title, status, priority, owner);
                WorkItemService.ApplyDraft(result.Path, draft);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ItemCreateOutput(
                        true,
                        new ItemCreateData(result.Id, result.Slug, result.Path));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ItemCreateOutput);
                }
                else
                {
                    Console.WriteLine($"{result.Id} created at {result.Path}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        voiceCommand.Subcommands.Add(voiceWorkItemCommand);

        var voiceDocCommand = new Command("doc", "Create a documentation file from voice input.");
        var voiceDocTypeOption = new Option<string>("--type")
        {
            Description = "Doc type: spec, adr, doc, runbook, guide",
            Required = true
        };
        voiceDocTypeOption.CompletionSources.Add("spec", "adr", "doc", "runbook", "guide");
        var voiceDocOutOption = new Option<string?>("--out")
        {
            Description = "Output path (defaults by type)."
        };
        var voiceDocTitleOption = new Option<string?>("--title")
        {
            Description = "Doc title (optional)."
        };
        voiceDocCommand.Options.Add(voiceDocTypeOption);
        voiceDocCommand.Options.Add(voiceDocOutOption);
        voiceDocCommand.Options.Add(voiceDocTitleOption);
        voiceDocCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var type = parseResult.GetValue(voiceDocTypeOption) ?? string.Empty;
                var outPath = parseResult.GetValue(voiceDocOutOption);
                var titleOverride = parseResult.GetValue(voiceDocTitleOption);

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var voiceConfig = VoiceConfig.Load();
                var transcript = await CaptureVoiceTranscriptAsync(voiceConfig, CancellationToken.None).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    SetExitCode(2);
                    return;
                }

                if (!AiDocClient.TryCreate(out var client, out var reason))
                {
                    Console.WriteLine($"AI doc generation disabled: {reason}");
                    SetExitCode(2);
                    return;
                }

                var draft = await client!.GenerateDraftAsync(type, transcript, titleOverride).ConfigureAwait(false);
                if (draft == null)
                {
                    Console.WriteLine("AI did not return a valid doc draft.");
                    SetExitCode(2);
                    return;
                }

                string title;
                if (!string.IsNullOrWhiteSpace(titleOverride))
                {
                    title = titleOverride.Trim();
                }
                else
                {
                    title = !string.IsNullOrWhiteSpace(draft.Title)
                        ? draft.Title
                        : DocTitleHelper.FromTranscript(transcript);
                }

                var body = !string.IsNullOrWhiteSpace(draft.Body)
                    ? draft.Body
                    : DocBodyBuilder.BuildSkeleton(type, title);

                var excerpt = DocFrontMatterBuilder.BuildTranscriptExcerpt(transcript, voiceConfig.TranscriptExcerptMaxChars);
                var source = new DocSourceInfo(
                    "voice",
                    string.IsNullOrWhiteSpace(excerpt) ? null : excerpt,
                    new DocAudioInfo(voiceConfig.Format.SampleRateHz, voiceConfig.Format.Channels, "wav"));

                var created = DocService.CreateGeneratedDoc(
                    repoRoot,
                    config,
                    type,
                    title,
                    body,
                    outPath,
                    new List<string>(),
                    new List<string>(),
                    new List<string>(),
                    new List<string>(),
                    "draft",
                    source,
                    force: false);

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new DocCreateOutput(
                        true,
                        new DocCreateData(created.Path, created.Type, created.WorkItems));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.DocCreateOutput);
                }
                else
                {
                    Console.WriteLine($"Doc created at {created.Path}");
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        voiceCommand.Subcommands.Add(voiceDocCommand);
        root.Subcommands.Add(voiceCommand);

        var navCommand = new Command("nav", "Group: navigation/index commands.");
        var navSyncCommand = new Command("sync", "Sync links and navigation indexes.");
        var navSyncIssuesOption = new Option<bool>("--issues")
        {
            Description = "Sync GitHub issue links for work items.",
            DefaultValueFactory = _ => true
        };
        var navSyncForceOption = new Option<bool>("--force")
        {
            Description = "Rewrite index sections even if content is unchanged."
        };
        var navSyncWorkboardOption = new Option<bool>("--workboard")
        {
            Description = "Regenerate the workboard.",
            DefaultValueFactory = _ => true
        };
        var navSyncIncludeDoneOption = new Option<bool>("--include-done")
        {
            Description = "Include done/dropped work items in indexes."
        };
        var navSyncDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        navSyncCommand.Options.Add(navSyncIssuesOption);
        navSyncCommand.Options.Add(navSyncForceOption);
        navSyncCommand.Options.Add(navSyncWorkboardOption);
        navSyncCommand.Options.Add(navSyncIncludeDoneOption);
        navSyncCommand.Options.Add(navSyncDryRunOption);
        navSyncCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var includeDone = parseResult.GetValue(navSyncIncludeDoneOption);
                var syncIssues = parseResult.GetValue(navSyncIssuesOption);
                var force = parseResult.GetValue(navSyncForceOption);
                var syncWorkboard = parseResult.GetValue(navSyncWorkboardOption);
                var dryRun = parseResult.GetValue(navSyncDryRunOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var result = await NavigationService.SyncNavigationAsync(repoRoot, config, includeDone, syncIssues, force, syncWorkboard, dryRun).ConfigureAwait(false);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new NavSyncOutput(
                        true,
                        new NavSyncData(
                            result.DocsUpdated,
                            result.ItemsUpdated,
                            result.IndexFilesUpdated,
                            result.WorkboardUpdated,
                            result.MissingDocs,
                            result.MissingItems,
                            result.Warnings));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.NavSyncOutput);
                }
                else
                {
                    Console.WriteLine($"Docs updated: {result.DocsUpdated}");
                    Console.WriteLine($"Work items updated: {result.ItemsUpdated}");
                    Console.WriteLine($"Index files updated: {result.IndexFilesUpdated}");
                    Console.WriteLine($"Workboard updated: {result.WorkboardUpdated}");
                    if (result.Warnings.Count > 0)
                    {
                        Console.WriteLine("Warnings:");
                        foreach (var warning in result.Warnings)
                        {
                            Console.WriteLine($"- {warning}");
                        }
                    }
                    if (result.MissingDocs.Count > 0)
                    {
                        Console.WriteLine("Missing docs:");
                        foreach (var entry in result.MissingDocs)
                        {
                            Console.WriteLine($"- {entry}");
                        }
                    }
                    if (result.MissingItems.Count > 0)
                    {
                        Console.WriteLine("Missing work items:");
                        foreach (var entry in result.MissingItems)
                        {
                            Console.WriteLine($"- {entry}");
                        }
                    }
                }
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        navCommand.Subcommands.Add(navSyncCommand);
        root.Subcommands.Add(navCommand);

        var syncCommand = new Command("sync", "Sync work items, docs, and navigation.");
        var syncItemsOption = new Option<bool>("--items")
        {
            Description = "Run work item sync with GitHub issues and branches."
        };
        var syncDocsOption = new Option<bool>("--docs")
        {
            Description = "Sync doc/work item backlinks and front matter."
        };
        var syncNavOption = new Option<bool>("--nav")
        {
            Description = "Sync navigation indexes."
        };
        var syncIssuesOption = new Option<bool>("--issues")
        {
            Description = "Sync GitHub issue links for docs and navigation.",
            DefaultValueFactory = _ => true
        };
        var syncImportIssuesOption = new Option<bool>("--import-issues")
        {
            Description = "List GitHub issues and import ones not yet linked (slower)."
        };
        var syncIncludeDoneOption = new Option<bool>("--include-done")
        {
            Description = "Include done/dropped work items in docs and navigation."
        };
        var syncForceOption = new Option<bool>("--force")
        {
            Description = "Rewrite index sections even if content is unchanged."
        };
        var syncWorkboardOption = new Option<bool>("--workboard")
        {
            Description = "Regenerate the workboard when syncing navigation.",
            DefaultValueFactory = _ => true
        };
        var repoSyncDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report changes without writing files."
        };
        var repoSyncPreferOption = new Option<string?>("--prefer")
        {
            Description = "When syncing work items, prefer 'local' or 'github'."
        };
        repoSyncPreferOption.CompletionSources.Add("local", "github");
        syncCommand.Options.Add(syncItemsOption);
        syncCommand.Options.Add(syncDocsOption);
        syncCommand.Options.Add(syncNavOption);
        syncCommand.Options.Add(syncIssuesOption);
        syncCommand.Options.Add(syncImportIssuesOption);
        syncCommand.Options.Add(syncIncludeDoneOption);
        syncCommand.Options.Add(syncForceOption);
        syncCommand.Options.Add(syncWorkboardOption);
        syncCommand.Options.Add(repoSyncDryRunOption);
        syncCommand.Options.Add(repoSyncPreferOption);
        syncCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var runItems = parseResult.GetValue(syncItemsOption);
                var runDocs = parseResult.GetValue(syncDocsOption);
                var runNav = parseResult.GetValue(syncNavOption);
                var syncIssues = parseResult.GetValue(syncIssuesOption);
                var importIssues = parseResult.GetValue(syncImportIssuesOption);
                var includeDone = parseResult.GetValue(syncIncludeDoneOption);
                var force = parseResult.GetValue(syncForceOption);
                var syncWorkboard = parseResult.GetValue(syncWorkboardOption);
                var dryRun = parseResult.GetValue(repoSyncDryRunOption);
                var prefer = parseResult.GetValue(repoSyncPreferOption);

                if (!runItems && !runDocs && !runNav)
                {
                    runItems = true;
                    runDocs = true;
                    runNav = true;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                ItemSyncData? itemData = null;
                DocSyncData? docData = null;
                NavSyncData? navData = null;
                var hasConflicts = false;

                if (runItems)
                {
                    itemData = await RunItemSyncAsync(repoRoot, config, Array.Empty<string>(), Array.Empty<string>(), importIssues, prefer, dryRun, syncIssues).ConfigureAwait(false);
                    hasConflicts = itemData.Conflicts.Count > 0;
                }

                if (runDocs)
                {
                    var result = await DocService.SyncLinksAsync(repoRoot, config, includeAllDocs: true, syncIssues, includeDone, dryRun).ConfigureAwait(false);
                    docData = new DocSyncData(
                        result.DocsUpdated,
                        result.ItemsUpdated,
                        result.MissingDocs,
                        result.MissingItems);
                }

                if (runNav)
                {
                    var result = await NavigationService.SyncNavigationAsync(
                        repoRoot,
                        config,
                        includeDone,
                        syncIssues,
                        force,
                        syncWorkboard,
                        dryRun,
                        syncDocs: !runDocs).ConfigureAwait(false);
                    navData = new NavSyncData(
                        result.DocsUpdated,
                        result.ItemsUpdated,
                        result.IndexFilesUpdated,
                        result.WorkboardUpdated,
                        result.MissingDocs,
                        result.MissingItems,
                        result.Warnings);
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new RepoSyncOutput(
                        true,
                        new RepoSyncData(itemData, docData, navData, dryRun));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.RepoSyncOutput);
                }
                else
                {
                    if (runItems && itemData is not null)
                    {
                        Console.WriteLine("Item sync:");
                        Console.WriteLine($"Imported: {itemData.Imported.Count}");
                        Console.WriteLine($"Issues created: {itemData.IssuesCreated.Count}");
                        Console.WriteLine($"Issues updated: {itemData.IssuesUpdated.Count}");
                        Console.WriteLine($"Items updated: {itemData.ItemsUpdated.Count}");
                        Console.WriteLine($"Branches created: {itemData.BranchesCreated.Count}");
                        if (itemData.Conflicts.Count > 0)
                        {
                            Console.WriteLine($"Conflicts: {itemData.Conflicts.Count}");
                            foreach (var conflict in itemData.Conflicts)
                            {
                                Console.WriteLine($"- {conflict.ItemId}: {conflict.Reason} ({conflict.IssueUrl})");
                            }
                        }
                        Console.WriteLine();
                    }

                    if (runDocs && docData is not null)
                    {
                        Console.WriteLine("Doc sync:");
                        Console.WriteLine($"Docs updated: {docData.DocsUpdated}");
                        Console.WriteLine($"Work items updated: {docData.ItemsUpdated}");
                        if (docData.MissingDocs.Count > 0)
                        {
                            Console.WriteLine("Missing docs:");
                            foreach (var entry in docData.MissingDocs)
                            {
                                Console.WriteLine($"- {entry}");
                            }
                        }
                        if (docData.MissingItems.Count > 0)
                        {
                            Console.WriteLine("Missing work items:");
                            foreach (var entry in docData.MissingItems)
                            {
                                Console.WriteLine($"- {entry}");
                            }
                        }
                        Console.WriteLine();
                    }

                    if (runNav && navData is not null)
                    {
                        Console.WriteLine("Nav sync:");
                        Console.WriteLine($"Docs updated: {navData.DocsUpdated}");
                        Console.WriteLine($"Work items updated: {navData.ItemsUpdated}");
                        Console.WriteLine($"Index files updated: {navData.IndexFilesUpdated}");
                        Console.WriteLine($"Workboard updated: {navData.WorkboardUpdated}");
                        if (navData.Warnings.Count > 0)
                        {
                            Console.WriteLine("Warnings:");
                            foreach (var warning in navData.Warnings)
                            {
                                Console.WriteLine($"- {warning}");
                            }
                        }
                        if (navData.MissingDocs.Count > 0)
                        {
                            Console.WriteLine("Missing docs:");
                            foreach (var entry in navData.MissingDocs)
                            {
                                Console.WriteLine($"- {entry}");
                            }
                        }
                        if (navData.MissingItems.Count > 0)
                        {
                            Console.WriteLine("Missing work items:");
                            foreach (var entry in navData.MissingItems)
                            {
                                Console.WriteLine($"- {entry}");
                            }
                        }
                    }
                }

                SetExitCode(hasConflicts ? 2 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(syncCommand);

        var guideCommand = new Command("guide", "Run the interactive guide for common tasks.");
        guideCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var repoRoot = ResolveRepo(repo);
                var result = RunGuide(repoRoot);
                SetExitCode(result);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(guideCommand);

        var migrateCommand = new Command("migrate", "Run repository migrations.");
        var migrateTargetArg = new Argument<string>("target")
        {
            Description = "Migration target (coherent-v1)."
        };
        var migrateDryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report migration changes without writing files."
        };
        migrateCommand.Arguments.Add(migrateTargetArg);
        migrateCommand.Options.Add(migrateDryRunOption);
        migrateCommand.SetAction(async parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var target = parseResult.GetValue(migrateTargetArg) ?? string.Empty;
                var dryRun = parseResult.GetValue(migrateDryRunOption);
                if (!string.Equals(target, "coherent-v1", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Unknown migration target '{target}'. Supported targets: coherent-v1.");
                    SetExitCode(2);
                    return;
                }

                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var data = await RunCoherentMigrationAsync(repoRoot, config, dryRun).ConfigureAwait(false);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new MigrationOutput(true, data);
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.MigrationOutput);
                }
                else
                {
                    Console.WriteLine("Migration coherent-v1 complete.");
                    Console.WriteLine($"Moved to done: {data.MovedToDone.Count}");
                    Console.WriteLine($"Moved to items: {data.MovedToItems.Count}");
                    Console.WriteLine($"Items normalized: {data.ItemsNormalized}");
                    Console.WriteLine($"Docs updated: {data.DocsUpdated}");
                    Console.WriteLine($"Item links updated: {data.ItemLinksUpdated}");
                    Console.WriteLine($"Index files updated: {data.IndexFilesUpdated}");
                    Console.WriteLine($"Workboard updated: {data.WorkboardUpdated}");
                    if (!string.IsNullOrWhiteSpace(data.ReportPath))
                    {
                        Console.WriteLine($"Report: {data.ReportPath}");
                    }
                }

                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(migrateCommand);

        var validateCommand = new Command("validate", "Validate work items, links, and schemas.");
        var strictOption = new Option<bool>("--strict")
        {
            Description = "Treat warnings as errors."
        };
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Show detailed validation output."
        };
        var linkIncludeOption = new Option<string[]>("--link-include")
        {
            Description = "Repo-relative path prefixes to include in link validation.",
            AllowMultipleArgumentsPerToken = true
        };
        var linkExcludeOption = new Option<string[]>("--link-exclude")
        {
            Description = "Repo-relative path prefixes to exclude from link validation.",
            AllowMultipleArgumentsPerToken = true
        };
        var skipDocSchemaOption = new Option<bool>("--skip-doc-schema")
        {
            Description = "Skip doc front matter schema validation."
        };
        validateCommand.Options.Add(strictOption);
        validateCommand.Options.Add(verboseOption);
        validateCommand.Options.Add(linkIncludeOption);
        validateCommand.Options.Add(linkExcludeOption);
        validateCommand.Options.Add(skipDocSchemaOption);
        validateCommand.Aliases.Add("verify");
        validateCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var strict = parseResult.GetValue(strictOption);
                var verbose = parseResult.GetValue(verboseOption);
                var linkInclude = parseResult.GetValue(linkIncludeOption) ?? Array.Empty<string>();
                var linkExclude = parseResult.GetValue(linkExcludeOption) ?? Array.Empty<string>();
                var skipDocSchema = parseResult.GetValue(skipDocSchemaOption);
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }
                var options = new ValidationOptions(linkInclude, linkExclude, skipDocSchema);
                var result = ValidationService.ValidateRepo(repoRoot, config, options);
                int exit;
                if (result.Errors.Count > 0)
                {
                    exit = 2;
                }
                else
                {
                    if (result.Warnings.Count > 0)
                    {
                        if (strict)
                        {
                            exit = 2;
                        }
                        else
                        {
                            exit = 1;
                        }
                    }
                    else
                    {
                        exit = 0;
                    }
                }

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new ValidateOutput(
                        result.Errors.Count == 0,
                        new ValidateData(
                            result.Errors,
                            result.Warnings,
                            new ValidateCounts(
                                result.Errors.Count,
                                result.Warnings.Count,
                                result.WorkItemCount,
                                result.MarkdownFileCount)));
                    WriteJson(payload, Core.WorkbenchJsonContext.Default.ValidateOutput);
                }
                else
                {
                    if (verbose)
                    {
                        Console.WriteLine($"Work items scanned: {result.WorkItemCount}");
                        Console.WriteLine($"Markdown files scanned: {result.MarkdownFileCount}");
                    }
                    if (result.Errors.Count > 0)
                    {
                        Console.WriteLine("Errors:");
                        foreach (var error in result.Errors)
                        {
                            Console.WriteLine($"- {error}");
                        }
                    }
                    if (result.Warnings.Count > 0)
                    {
                        Console.WriteLine("Warnings:");
                        foreach (var warning in result.Warnings)
                        {
                            Console.WriteLine($"- {warning}");
                        }
                    }
                    if (result.Errors.Count == 0 && result.Warnings.Count == 0)
                    {
                        Console.WriteLine("Validation passed.");
                    }
                }
                SetExitCode(exit);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        root.Subcommands.Add(validateCommand);

        var llmCommand = new Command("llm", "Group: AI-oriented help and guidance commands.");
        llmCommand.Aliases.Add("llms");
        var llmHelpCommand = new Command("help", "Print a comprehensive CLI reference for AI agents.");
        llmHelpCommand.SetAction(parseResult =>
        {
            WriteLlmHelp(root);
            SetExitCode(0);
        });
        llmCommand.Subcommands.Add(llmHelpCommand);
        llmCommand.SetAction(parseResult =>
        {
            WriteLlmHelp(root);
            SetExitCode(0);
        });
        root.Subcommands.Add(llmCommand);

        if (args.Length == 0)
        {
            try
            {
                var repoRoot = ResolveRepo(null);
                if (IsFirstRun(repoRoot))
                {
                    var result = await RunInitWorkflowAsync(repoRoot, new InitWorkflowOptions(
                        Force: false,
                        NonInteractive: false,
                        SkipWizard: false,
                        SyncFrontMatter: false,
                        ConfigureOpenAi: false,
                        CredentialStore: null,
                        CredentialPath: null,
                        OpenAiProvider: null,
                        OpenAiKey: null,
                        OpenAiModel: null)).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                    {
                        return result.ExitCode;
                    }
                    if (result.ShouldRunWizard)
                    {
                        return RunGuide(repoRoot);
                    }
                    return 0;
                }
            }
#pragma warning disable RCS1075
            catch (Exception)
#pragma warning restore RCS1075
            {
                // Fall back to help output if repo detection fails or we are not in a repo.
#pragma warning disable ERP022
            }
#pragma warning restore ERP022

            args = new[] { "--help" };
        }

        var exitCode = await root.Parse(args).InvokeAsync().ConfigureAwait(false);
        return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
    }
}
