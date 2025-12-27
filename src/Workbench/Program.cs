using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Linq;
using Workbench;

static string ResolveRepo(string? repoArg)
{
    var envRepo = Environment.GetEnvironmentVariable("WORKBENCH_REPO");
    var candidate = repoArg ?? envRepo ?? Directory.GetCurrentDirectory();
    var repoRoot = Repository.FindRepoRoot(candidate);
    if (repoRoot is null)
    {
        throw new InvalidOperationException("Not a git repository.");
    }
    return repoRoot;
}

static string ResolveFormat(string formatArg)
{
    var envFormat = Environment.GetEnvironmentVariable("WORKBENCH_FORMAT");
    return string.IsNullOrWhiteSpace(envFormat) ? formatArg : envFormat;
}

static void WriteJson<T>(T payload, JsonTypeInfo<T> typeInfo)
{
    Console.WriteLine(JsonSerializer.Serialize(payload, typeInfo));
}

static WorkItemPayload ItemToPayload(WorkItem item, bool includeBody = false)
{
    return new WorkItemPayload(
        item.Id,
        item.Type,
        item.Status,
        item.Title,
        item.Priority,
        item.Owner,
        item.Created,
        item.Updated,
        item.Tags,
        new RelatedLinksPayload(
            item.Related.Specs,
            item.Related.Adrs,
            item.Related.Files,
            item.Related.Prs,
            item.Related.Issues),
        item.Slug,
        item.Path,
        includeBody ? item.Body : null);
}

static void SetExitCode(int code) => Environment.ExitCode = code;

static string NormalizeRepoLink(string repoRoot, string link)
{
    var trimmed = link.Trim();
    if (trimmed.Length == 0)
    {
        return trimmed;
    }
    var normalized = trimmed.Replace('\\', '/');
    if (Path.IsPathRooted(trimmed))
    {
        var full = Path.GetFullPath(trimmed);
        var repoFull = Path.GetFullPath(repoRoot);
        if (full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
            return "/" + relative;
        }
        return full.Replace('\\', '/');
    }
    return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
}

static void HandleDocCreate(
    string? repo,
    string format,
    string type,
    string title,
    string? path,
    string[] workItems,
    string[] codeRefs,
    bool force)
{
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

        var result = DocService.CreateDoc(
            repoRoot,
            config,
            type,
            title,
            path,
            workItems.ToList(),
            codeRefs.ToList(),
            force);

        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new DocCreateOutput(
                true,
                new DocCreateData(result.Path, result.Type, result.WorkItems));
            WriteJson(payload, WorkbenchJsonContext.Default.DocCreateOutput);
        }
        else
        {
            Console.WriteLine($"Doc created at {result.Path}");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.ToString());
        SetExitCode(2);
    }
}

static void HandleDocLink(
    string? repo,
    string format,
    string docType,
    string docPath,
    string[] workItems,
    bool add,
    bool dryRun)
{
    try
    {
        if (workItems.Length == 0)
        {
            Console.WriteLine("No work items provided.");
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

        var normalizedDoc = NormalizeRepoLink(repoRoot, docPath);
        var itemsUpdated = 0;
        var docUpdated = false;

        foreach (var workItemId in workItems)
        {
            var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
            var key = docType.Equals("adr", StringComparison.OrdinalIgnoreCase) ? "adrs" : "specs";
            var updated = add
                ? WorkItemService.AddRelatedLink(itemPath, key, normalizedDoc, apply: !dryRun)
                : WorkItemService.RemoveRelatedLink(itemPath, key, normalizedDoc, apply: !dryRun);
            if (updated)
            {
                itemsUpdated++;
            }

            if (DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalizedDoc, workItemId, add, apply: !dryRun))
            {
                docUpdated = true;
            }
        }

        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new DocLinkOutput(
                true,
                new DocLinkData(
                    normalizedDoc,
                    docType,
                    workItems.ToList(),
                    itemsUpdated,
                    docUpdated));
            WriteJson(payload, WorkbenchJsonContext.Default.DocLinkOutput);
        }
        else
        {
            var action = add ? "linked" : "unlinked";
            Console.WriteLine($"{docType.ToUpperInvariant()} {action}: {normalizedDoc}");
            Console.WriteLine($"Work items updated: {itemsUpdated}");
            Console.WriteLine($"Doc updated: {(docUpdated ? "yes" : "no")}");
            if (dryRun)
            {
                Console.WriteLine("Dry run: no files were modified.");
            }
        }

        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
}

static string ApplyPattern(string pattern, WorkItem item)
{
    return pattern
        .Replace("{id}", item.Id)
        .Replace("{slug}", item.Slug)
        .Replace("{title}", item.Title);
}

static string CreatePr(
    string repoRoot,
    WorkbenchConfig config,
    WorkItem item,
    string? baseBranch,
    bool draft,
    bool fill)
{
    var prTitle = $"{item.Id}: {item.Title}";
    var prBody = fill ? PullRequestBuilder.BuildBody(item) : $"Work item: /{Path.GetRelativePath(repoRoot, item.Path).Replace('\\', '/')}";
    var isDraft = draft || config.Github.DefaultDraft;
    var prUrl = GithubService.CreatePullRequest(repoRoot, prTitle, prBody, baseBranch ?? config.Git.DefaultBaseBranch, isDraft);
    WorkItemService.AddPrLink(item.Path, prUrl);
    return prUrl;
}

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

var root = new RootCommand("Bravellian Workbench CLI");
root.Options.Add(repoOption);
root.Options.Add(formatOption);
root.Options.Add(noColorOption);
root.Options.Add(quietOption);

var versionCommand = new Command("version", "Print CLI version.");
versionCommand.SetAction(parseResult =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    Console.WriteLine(version);
    SetExitCode(0);
});
root.Subcommands.Add(versionCommand);

var doctorCommand = new Command("doctor", "Check git, config, and expected paths.");
doctorCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
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
        }

        var ghStatus = GithubService.CheckAuthStatus(repoRoot);
        if (string.Equals(ghStatus.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new DoctorCheck(
                "gh",
                "ok",
                new DoctorCheckDetails(
                    Version: ghStatus.Version,
                    Error: null,
                    Reason: null,
                    Path: null,
                    Missing: null,
                    SchemaErrors: null)));
        }
        else
        {
            checks.Add(new DoctorCheck(
                "gh",
                ghStatus.Status,
                new DoctorCheckDetails(
                    Version: null,
                    Error: null,
                    Reason: ghStatus.Reason,
                    Path: null,
                    Missing: null,
                    SchemaErrors: null)));
            hasWarnings = true;
        }

        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new DoctorOutput(
                !hasError,
                new DoctorData(repoRoot, checks));
            WriteJson(payload, WorkbenchJsonContext.Default.DoctorOutput);
        }
        else
        {
            Console.WriteLine($"Repo: {repoRoot}");
            Console.WriteLine("Checks:");
            foreach (var check in checks)
            {
                Console.WriteLine($"- {check}");
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
root.Subcommands.Add(doctorCommand);

var scaffoldForceOption = new Option<bool>("--force")
{
    Description = "Overwrite existing files."
};
var scaffoldCommand = new Command("scaffold", "Create the default folder structure, templates, and config.");
scaffoldCommand.Options.Add(scaffoldForceOption);
scaffoldCommand.Aliases.Add("init");
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
            WriteJson(payload, WorkbenchJsonContext.Default.ScaffoldOutput);
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
root.Subcommands.Add(scaffoldCommand);

var configCommand = new Command("config", "Configuration commands.");
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
            WriteJson(payload, WorkbenchJsonContext.Default.ConfigOutput);
        }
        else
        {
            Console.WriteLine($"Config path: {WorkbenchConfig.GetConfigPath(repoRoot)}");
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
            }
            Console.WriteLine(JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig));
        }
        SetExitCode(configError is null ? 0 : 2);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
configCommand.Subcommands.Add(configShowCommand);
root.Subcommands.Add(configCommand);

var itemCommand = new Command("item", "Work item commands.");

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
        Description = "Work item status"
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

var itemNewCommand = new Command("new", "Create a new work item in work/items using templates and ID allocation.");
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemCreateOutput);
        }
        else
        {
            Console.WriteLine($"{result.Id} created at {result.Path}");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
itemCommand.Subcommands.Add(itemNewCommand);

var itemListCommand = new Command("list", "List work items.");
var listTypeOption = new Option<string>("--type")
{
    Description = "Filter by type"
};
listTypeOption.CompletionSources.Add("bug", "task", "spike");
var listStatusOption = new Option<string>("--status")
{
    Description = "Filter by status"
};
listStatusOption.CompletionSources.Add("draft", "ready", "in-progress", "blocked", "done", "dropped");
var includeDoneOption = new Option<bool>("--include-done")
{
    Description = "Include items from work/done."
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemListOutput);
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
        Console.WriteLine(ex);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemShowOutput);
        }
        else
        {
            Console.WriteLine($"{item.Id} - {item.Title}");
            Console.WriteLine($"Type: {item.Type}");
            Console.WriteLine($"Status: {item.Status}");
            Console.WriteLine($"Path: {item.Path}");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
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
    Description = "New status."
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemStatusOutput);
        }
        else
        {
            Console.WriteLine($"{updated.Id} status updated to {updated.Status}.");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
itemCommand.Subcommands.Add(itemStatusCommand);

var itemCloseCommand = new Command("close", "Set status to done; optionally move to work/done.");
var closeIdArg = new Argument<string>("id")
{
    Description = "Work item ID."
};
var moveOption = new Option<bool>("--move")
{
    Description = "Move to work/done."
};
itemCloseCommand.Arguments.Add(closeIdArg);
itemCloseCommand.Options.Add(moveOption);
itemCloseCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var id = parseResult.GetValue(closeIdArg) ?? string.Empty;
        var move = parseResult.GetValue(moveOption);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemCloseOutput);
        }
        else
        {
            Console.WriteLine($"{updated.Id} closed.");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemMoveOutput);
        }
        else
        {
            Console.WriteLine($"{updated.Id} moved to {updated.Path}.");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemRenameOutput);
        }
        else
        {
            Console.WriteLine($"{updated.Id} renamed to {updated.Path}.");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
itemCommand.Subcommands.Add(itemRenameCommand);

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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemShowOutput);
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
        Console.WriteLine(ex);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ItemShowOutput);
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
itemCommand.Subcommands.Add(itemUnlinkCommand);

root.Subcommands.Add(itemCommand);

var addCommand = new Command("add", "Shorthand for item creation.");
Command BuildAddCommand(string typeName)
{
    var cmd = new Command(typeName, $"Alias for workbench item new --type {typeName}.");
    var titleOption = CreateTitleOption();
    var statusOption = CreateStatusOption();
    var priorityOption = CreatePriorityOption();
    var ownerOption = CreateOwnerOption();
    cmd.Options.Add(titleOption);
    cmd.Options.Add(statusOption);
    cmd.Options.Add(priorityOption);
    cmd.Options.Add(ownerOption);
    cmd.SetAction(parseResult =>
    {
        try
        {
            var repo = parseResult.GetValue(repoOption);
            var format = parseResult.GetValue(formatOption) ?? "table";
            var title = parseResult.GetValue(titleOption) ?? string.Empty;
            var status = parseResult.GetValue(statusOption);
            var priority = parseResult.GetValue(priorityOption);
            var owner = parseResult.GetValue(ownerOption);
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
                SetExitCode(2);
                return;
            }
            var result = WorkItemService.CreateItem(repoRoot, config, typeName, title, status, priority, owner);
            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new ItemCreateOutput(
                    true,
                    new ItemCreateData(result.Id, result.Slug, result.Path));
                WriteJson(payload, WorkbenchJsonContext.Default.ItemCreateOutput);
            }
            else
            {
                Console.WriteLine($"{result.Id} created at {result.Path}");
            }
            SetExitCode(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            SetExitCode(2);
        }
    });
    return cmd;
}
addCommand.Subcommands.Add(BuildAddCommand("task"));
addCommand.Subcommands.Add(BuildAddCommand("bug"));
addCommand.Subcommands.Add(BuildAddCommand("spike"));
root.Subcommands.Add(addCommand);

var boardCommand = new Command("board", "Workboard commands.");
var boardRegenCommand = new Command("regen", "Regenerate work/WORKBOARD.md.");
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
            WriteJson(payload, WorkbenchJsonContext.Default.BoardOutput);
        }
        else
        {
            Console.WriteLine($"Workboard regenerated: {result.Path}");
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
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
promoteCommand.SetAction(parseResult =>
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
            prUrl = CreatePr(repoRoot, config, item, baseBranch, useDraft, fill: true);
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
            WriteJson(payload, WorkbenchJsonContext.Default.PromoteOutput);
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
root.Subcommands.Add(promoteCommand);

var prCommand = new Command("pr", "Pull request commands.");
var prCreateCommand = new Command("create", "Create a GitHub PR via gh and backlink the PR URL.");
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
prCreateCommand.Arguments.Add(prIdArg);
prCreateCommand.Options.Add(prBaseOption);
prCreateCommand.Options.Add(prDraftOption);
prCreateCommand.Options.Add(prFillOption);
prCreateCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var id = parseResult.GetValue(prIdArg) ?? string.Empty;
        var baseBranch = parseResult.GetValue(prBaseOption);
        var draft = parseResult.GetValue(prDraftOption);
        var fill = parseResult.GetValue(prFillOption);
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
        var prUrl = CreatePr(repoRoot, config, item, baseBranch, draft, fill);

        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new PrOutput(
                true,
                new PrData(prUrl, item.Id));
            WriteJson(payload, WorkbenchJsonContext.Default.PrOutput);
        }
        else
        {
            Console.WriteLine(prUrl);
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
prCommand.Subcommands.Add(prCreateCommand);
root.Subcommands.Add(prCommand);

var createCommand = new Command("create", "Create resources.");
var createPrCommand = new Command("pr", "Alias for workbench pr create.");
createPrCommand.Arguments.Add(prIdArg);
createPrCommand.Options.Add(prBaseOption);
createPrCommand.Options.Add(prDraftOption);
createPrCommand.Options.Add(prFillOption);
createPrCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var id = parseResult.GetValue(prIdArg) ?? string.Empty;
        var baseBranch = parseResult.GetValue(prBaseOption);
        var draft = parseResult.GetValue(prDraftOption);
        var fill = parseResult.GetValue(prFillOption);
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
        var prUrl = CreatePr(repoRoot, config, item, baseBranch, draft, fill);

        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new PrOutput(
                true,
                new PrData(prUrl, item.Id));
            WriteJson(payload, WorkbenchJsonContext.Default.PrOutput);
        }
        else
        {
            Console.WriteLine(prUrl);
        }
        SetExitCode(0);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
createCommand.Subcommands.Add(createPrCommand);
root.Subcommands.Add(createCommand);

var docCommand = new Command("doc", "Documentation commands.");

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

var docSyncCommand = new Command("sync", "Sync doc/work item backlinks.");
var docSyncAllOption = new Option<bool>("--all")
{
    Description = "Add Workbench front matter to all docs."
};
var docSyncDryRunOption = new Option<bool>("--dry-run")
{
    Description = "Report changes without writing files."
};
docSyncCommand.Options.Add(docSyncAllOption);
docSyncCommand.Options.Add(docSyncDryRunOption);
docSyncCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var all = parseResult.GetValue(docSyncAllOption);
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

        var result = DocService.SyncLinks(repoRoot, config, all, dryRun);
        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new DocSyncOutput(
                true,
                new DocSyncData(
                    result.DocsUpdated,
                    result.ItemsUpdated,
                    result.MissingDocs,
                    result.MissingItems));
            WriteJson(payload, WorkbenchJsonContext.Default.DocSyncOutput);
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});

docCommand.Subcommands.Add(docNewCommand);
docCommand.Subcommands.Add(docSyncCommand);
root.Subcommands.Add(docCommand);

var specCommand = new Command("spec", "Spec documentation commands.");
var specNewCommand = new Command("new", "Create a spec document and auto-link work items.");
specNewCommand.Options.Add(docTitleOption);
specNewCommand.Options.Add(docPathOption);
specNewCommand.Options.Add(docWorkItemOption);
specNewCommand.Options.Add(docCodeRefOption);
specNewCommand.Options.Add(docForceOption);
specNewCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var title = parseResult.GetValue(docTitleOption) ?? string.Empty;
    var path = parseResult.GetValue(docPathOption);
    var workItems = parseResult.GetValue(docWorkItemOption) ?? Array.Empty<string>();
    var codeRefs = parseResult.GetValue(docCodeRefOption) ?? Array.Empty<string>();
    var force = parseResult.GetValue(docForceOption);
    HandleDocCreate(repo, format, "spec", title, path, workItems, codeRefs, force);
});
specCommand.Subcommands.Add(specNewCommand);
var specLinkCommand = new Command("link", "Link a spec document to work items.");
var specLinkPathOption = new Option<string>("--path")
{
    Description = "Spec path.",
    Required = true
};
var specLinkWorkItemOption = new Option<string[]>("--work-item")
{
    Description = "Work item ID(s) to link.",
    AllowMultipleArgumentsPerToken = true
};
var specLinkDryRunOption = new Option<bool>("--dry-run")
{
    Description = "Report changes without writing files."
};
specLinkCommand.Options.Add(specLinkPathOption);
specLinkCommand.Options.Add(specLinkWorkItemOption);
specLinkCommand.Options.Add(specLinkDryRunOption);
specLinkCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var path = parseResult.GetValue(specLinkPathOption) ?? string.Empty;
    var workItems = parseResult.GetValue(specLinkWorkItemOption) ?? Array.Empty<string>();
    var dryRun = parseResult.GetValue(specLinkDryRunOption);
    HandleDocLink(repo, format, "spec", path, workItems, add: true, dryRun: dryRun);
});
specCommand.Subcommands.Add(specLinkCommand);

var specUnlinkCommand = new Command("unlink", "Unlink a spec document from work items.");
var specUnlinkPathOption = new Option<string>("--path")
{
    Description = "Spec path.",
    Required = true
};
var specUnlinkWorkItemOption = new Option<string[]>("--work-item")
{
    Description = "Work item ID(s) to unlink.",
    AllowMultipleArgumentsPerToken = true
};
var specUnlinkDryRunOption = new Option<bool>("--dry-run")
{
    Description = "Report changes without writing files."
};
specUnlinkCommand.Options.Add(specUnlinkPathOption);
specUnlinkCommand.Options.Add(specUnlinkWorkItemOption);
specUnlinkCommand.Options.Add(specUnlinkDryRunOption);
specUnlinkCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var path = parseResult.GetValue(specUnlinkPathOption) ?? string.Empty;
    var workItems = parseResult.GetValue(specUnlinkWorkItemOption) ?? Array.Empty<string>();
    var dryRun = parseResult.GetValue(specUnlinkDryRunOption);
    HandleDocLink(repo, format, "spec", path, workItems, add: false, dryRun: dryRun);
});
specCommand.Subcommands.Add(specUnlinkCommand);
root.Subcommands.Add(specCommand);

var adrCommand = new Command("adr", "ADR documentation commands.");
var adrNewCommand = new Command("new", "Create an ADR document and auto-link work items.");
adrNewCommand.Options.Add(docTitleOption);
adrNewCommand.Options.Add(docPathOption);
adrNewCommand.Options.Add(docWorkItemOption);
adrNewCommand.Options.Add(docCodeRefOption);
adrNewCommand.Options.Add(docForceOption);
adrNewCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var title = parseResult.GetValue(docTitleOption) ?? string.Empty;
    var path = parseResult.GetValue(docPathOption);
    var workItems = parseResult.GetValue(docWorkItemOption) ?? Array.Empty<string>();
    var codeRefs = parseResult.GetValue(docCodeRefOption) ?? Array.Empty<string>();
    var force = parseResult.GetValue(docForceOption);
    HandleDocCreate(repo, format, "adr", title, path, workItems, codeRefs, force);
});
adrCommand.Subcommands.Add(adrNewCommand);
var adrLinkCommand = new Command("link", "Link an ADR document to work items.");
var adrLinkPathOption = new Option<string>("--path")
{
    Description = "ADR path.",
    Required = true
};
var adrLinkWorkItemOption = new Option<string[]>("--work-item")
{
    Description = "Work item ID(s) to link.",
    AllowMultipleArgumentsPerToken = true
};
var adrLinkDryRunOption = new Option<bool>("--dry-run")
{
    Description = "Report changes without writing files."
};
adrLinkCommand.Options.Add(adrLinkPathOption);
adrLinkCommand.Options.Add(adrLinkWorkItemOption);
adrLinkCommand.Options.Add(adrLinkDryRunOption);
adrLinkCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var path = parseResult.GetValue(adrLinkPathOption) ?? string.Empty;
    var workItems = parseResult.GetValue(adrLinkWorkItemOption) ?? Array.Empty<string>();
    var dryRun = parseResult.GetValue(adrLinkDryRunOption);
    HandleDocLink(repo, format, "adr", path, workItems, add: true, dryRun: dryRun);
});
adrCommand.Subcommands.Add(adrLinkCommand);

var adrUnlinkCommand = new Command("unlink", "Unlink an ADR document from work items.");
var adrUnlinkPathOption = new Option<string>("--path")
{
    Description = "ADR path.",
    Required = true
};
var adrUnlinkWorkItemOption = new Option<string[]>("--work-item")
{
    Description = "Work item ID(s) to unlink.",
    AllowMultipleArgumentsPerToken = true
};
var adrUnlinkDryRunOption = new Option<bool>("--dry-run")
{
    Description = "Report changes without writing files."
};
adrUnlinkCommand.Options.Add(adrUnlinkPathOption);
adrUnlinkCommand.Options.Add(adrUnlinkWorkItemOption);
adrUnlinkCommand.Options.Add(adrUnlinkDryRunOption);
adrUnlinkCommand.SetAction(parseResult =>
{
    var repo = parseResult.GetValue(repoOption);
    var format = parseResult.GetValue(formatOption) ?? "table";
    var path = parseResult.GetValue(adrUnlinkPathOption) ?? string.Empty;
    var workItems = parseResult.GetValue(adrUnlinkWorkItemOption) ?? Array.Empty<string>();
    var dryRun = parseResult.GetValue(adrUnlinkDryRunOption);
    HandleDocLink(repo, format, "adr", path, workItems, add: false, dryRun: dryRun);
});
adrCommand.Subcommands.Add(adrUnlinkCommand);
root.Subcommands.Add(adrCommand);

var validateCommand = new Command("validate", "Validate work items, links, and schemas.");
var strictOption = new Option<bool>("--strict")
{
    Description = "Treat warnings as errors."
};
var verboseOption = new Option<bool>("--verbose")
{
    Description = "Show detailed validation output."
};
validateCommand.Options.Add(strictOption);
validateCommand.Options.Add(verboseOption);
validateCommand.Aliases.Add("verify");
validateCommand.SetAction(parseResult =>
{
    try
    {
        var repo = parseResult.GetValue(repoOption);
        var format = parseResult.GetValue(formatOption) ?? "table";
        var strict = parseResult.GetValue(strictOption);
        var verbose = parseResult.GetValue(verboseOption);
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            SetExitCode(2);
            return;
        }
        var result = ValidationService.ValidateRepo(repoRoot, config);
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
            WriteJson(payload, WorkbenchJsonContext.Default.ValidateOutput);
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
        Console.WriteLine(ex);
        SetExitCode(2);
    }
});
root.Subcommands.Add(validateCommand);

var exitCode = await root.Parse(args).InvokeAsync().ConfigureAwait(false);
return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
