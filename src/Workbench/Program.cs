using System.CommandLine;
using System.Text.Json;
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

static void WriteJson(object payload)
{
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);
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

var repoOption = new Option<string?>(
    name: "--repo",
    description: "Target repo (defaults to current dir)");

var formatOption = new Option<string>(
    name: "--format",
    getDefaultValue: () => "table",
    description: "Output format (table|json)");
formatOption.AddCompletions("table", "json");

var noColorOption = new Option<bool>(
    name: "--no-color",
    description: "Disable colored output");

var quietOption = new Option<bool>(
    name: "--quiet",
    description: "Suppress non-error output");

var root = new RootCommand("Bravellian Workbench CLI");
root.AddGlobalOption(repoOption);
root.AddGlobalOption(formatOption);
root.AddGlobalOption(noColorOption);
root.AddGlobalOption(quietOption);

var versionCommand = new Command("version", "Print CLI version.");
versionCommand.SetHandler(() =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    Console.WriteLine(version);
    return 0;
});
root.AddCommand(versionCommand);

var doctorCommand = new Command("doctor", "Check git, config, and expected paths.");
doctorCommand.SetHandler((string? repo, string format) =>
{
    try
    {
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
        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = configError is null && schemaErrors.Count == 0,
                data = new
                {
                    repoRoot,
                    configPath,
                    configError,
                    configSchemaErrors = schemaErrors,
                    missing
                }
            });
        }
        else
        {
            Console.WriteLine($"Repo: {repoRoot}");
            Console.WriteLine(File.Exists(configPath) ? "Config: ok" : "Config: missing");
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
            }
            if (schemaErrors.Count > 0)
            {
                Console.WriteLine("Config schema errors:");
                foreach (var error in schemaErrors)
                {
                    Console.WriteLine($"- {error}");
                }
            }
            if (missing.Count > 0)
            {
                Console.WriteLine("Missing paths:");
                foreach (var path in missing)
                {
                    Console.WriteLine($"- {path}");
                }
            }
        }
        return configError is null && schemaErrors.Count == 0 ? 0 : 2;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption);
root.AddCommand(doctorCommand);

var scaffoldForceOption = new Option<bool>("--force", "Overwrite existing files.");
var scaffoldCommand = new Command("scaffold", "Create the default folder structure, templates, and config.");
scaffoldCommand.AddOption(scaffoldForceOption);
scaffoldCommand.AddAlias("init");
scaffoldCommand.SetHandler((string? repo, string format, bool force) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var result = ScaffoldService.Scaffold(repoRoot, force);
        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = true,
                data = new
                {
                    created = result.Created,
                    skipped = result.Skipped,
                    configPath = result.ConfigPath
                }
            });
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
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, scaffoldForceOption);
root.AddCommand(scaffoldCommand);

var configCommand = new Command("config", "Configuration commands.");
var configShowCommand = new Command("show", "Print effective config (defaults + repo config + CLI overrides).");
configShowCommand.SetHandler((string? repo, string format) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = configError is null,
                data = new
                {
                    config,
                    sources = new
                    {
                        defaults = true,
                        repoConfig = WorkbenchConfig.GetConfigPath(repoRoot)
                    }
                }
            });
        }
        else
        {
            Console.WriteLine($"Config path: {WorkbenchConfig.GetConfigPath(repoRoot)}");
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
            }
            Console.WriteLine(JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        return configError is null ? 0 : 2;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption);
configCommand.AddCommand(configShowCommand);
root.AddCommand(configCommand);

var itemCommand = new Command("item", "Work item commands.");

var itemTypeOption = new Option<string>(
    name: "--type",
    description: "Work item type: bug, task, spike");
itemTypeOption.AddCompletions("bug", "task", "spike");
itemTypeOption.IsRequired = true;

static Option<string> CreateTitleOption()
{
    return new Option<string>(
        name: "--title",
        description: "Work item title")
    {
        IsRequired = true
    };
}

static Option<string?> CreateStatusOption()
{
    var option = new Option<string?>(
        name: "--status",
        description: "Work item status");
    option.AddCompletions("draft", "ready", "in-progress", "blocked", "done", "dropped");
    return option;
}

static Option<string?> CreatePriorityOption()
{
    var option = new Option<string?>(
        name: "--priority",
        description: "Work item priority");
    option.AddCompletions("low", "medium", "high", "critical");
    return option;
}

static Option<string?> CreateOwnerOption()
{
    return new Option<string?>(
        name: "--owner",
        description: "Work item owner");
}

var itemNewCommand = new Command("new", "Create a new work item in work/items using templates and ID allocation.");
var itemTitleOption = CreateTitleOption();
var itemStatusOption = CreateStatusOption();
var itemPriorityOption = CreatePriorityOption();
var itemOwnerOption = CreateOwnerOption();
itemNewCommand.AddOption(itemTypeOption);
itemNewCommand.AddOption(itemTitleOption);
itemNewCommand.AddOption(itemStatusOption);
itemNewCommand.AddOption(itemPriorityOption);
itemNewCommand.AddOption(itemOwnerOption);
itemNewCommand.SetHandler((string? repo, string format, string type, string title, string? status, string? priority, string? owner) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var result = WorkItemService.CreateItem(repoRoot, config, type, title, status, priority, owner);
        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = true,
                data = new
                {
                    id = result.Id,
                    slug = result.Slug,
                    path = result.Path
                }
            });
        }
        else
        {
            Console.WriteLine($"{result.Id} created at {result.Path}");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, itemTypeOption, itemTitleOption, itemStatusOption, itemPriorityOption, itemOwnerOption);
itemCommand.AddCommand(itemNewCommand);

var itemListCommand = new Command("list", "List work items.");
var listTypeOption = new Option<string>("--type", "Filter by type");
listTypeOption.AddCompletions("bug", "task", "spike");
var listStatusOption = new Option<string>("--status", "Filter by status");
listStatusOption.AddCompletions("draft", "ready", "in-progress", "blocked", "done", "dropped");
var includeDoneOption = new Option<bool>("--include-done", "Include items from work/done.");
itemListCommand.AddOption(listTypeOption);
itemListCommand.AddOption(listStatusOption);
itemListCommand.AddOption(includeDoneOption);
itemListCommand.SetHandler((string? repo, string format, string? type, string? status, bool includeDone) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
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

        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = true,
                data = new
                {
                    items = items.Select(item => new
                    {
                        id = item.Id,
                        type = item.Type,
                        status = item.Status,
                        title = item.Title,
                        path = item.Path
                    }).ToList()
                }
            });
        }
        else
        {
            foreach (var item in items.OrderBy(item => item.Id))
            {
                Console.WriteLine($"{item.Id}\t{item.Status}\t{item.Title}");
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, listTypeOption, listStatusOption, includeDoneOption);
itemCommand.AddCommand(itemListCommand);

var itemShowCommand = new Command("show", "Show metadata and resolved path for an item.");
var itemIdArg = new Argument<string>("id", "Work item ID (e.g., TASK-0042).");
itemShowCommand.AddArgument(itemIdArg);
itemShowCommand.SetHandler((string? repo, string format, string id) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = true,
                data = new
                {
                    item = new
                    {
                        id = item.Id,
                        type = item.Type,
                        status = item.Status,
                        title = item.Title,
                        priority = item.Priority,
                        owner = item.Owner,
                        created = item.Created,
                        updated = item.Updated,
                        tags = item.Tags,
                        related = item.Related,
                        path = item.Path
                    }
                }
            });
        }
        else
        {
            Console.WriteLine($"{item.Id} - {item.Title}");
            Console.WriteLine($"Type: {item.Type}");
            Console.WriteLine($"Status: {item.Status}");
            Console.WriteLine($"Path: {item.Path}");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, itemIdArg);
itemCommand.AddCommand(itemShowCommand);

var itemStatusCommand = new Command("status", "Update status and updated date.");
var statusIdArg = new Argument<string>("id", "Work item ID.");
var statusValueArg = new Argument<string>("status", "New status.");
var noteOption = new Option<string?>("--note", "Append a note.");
itemStatusCommand.AddArgument(statusIdArg);
itemStatusCommand.AddArgument(statusValueArg);
itemStatusCommand.AddOption(noteOption);
itemStatusCommand.SetHandler((string? repo, string format, string id, string status, string? note) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var updated = WorkItemService.UpdateStatus(path, status, note);
        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { item = updated } });
        }
        else
        {
            Console.WriteLine($"{updated.Id} status updated to {updated.Status}.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, statusIdArg, statusValueArg, noteOption);
itemCommand.AddCommand(itemStatusCommand);

var itemCloseCommand = new Command("close", "Set status to done; optionally move to work/done.");
var closeIdArg = new Argument<string>("id", "Work item ID.");
var moveOption = new Option<bool>("--move", "Move to work/done.");
itemCloseCommand.AddArgument(closeIdArg);
itemCloseCommand.AddOption(moveOption);
itemCloseCommand.SetHandler((string? repo, string format, string id, bool move) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
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
        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { item = updated, moved = move } });
        }
        else
        {
            Console.WriteLine($"{updated.Id} closed.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, closeIdArg, moveOption);
itemCommand.AddCommand(itemCloseCommand);

var itemMoveCommand = new Command("move", "Move a work item file and update inbound links where possible.");
var moveIdArg = new Argument<string>("id", "Work item ID.");
var moveToOption = new Option<string>("--to", "Destination path.") { IsRequired = true };
itemMoveCommand.AddArgument(moveIdArg);
itemMoveCommand.AddOption(moveToOption);
itemMoveCommand.SetHandler((string? repo, string format, string id, string to) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var updated = WorkItemService.Move(path, to, repoRoot);
        LinkUpdater.UpdateLinks(repoRoot, path, updated.Path);
        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { item = updated } });
        }
        else
        {
            Console.WriteLine($"{updated.Id} moved to {updated.Path}.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, moveIdArg, moveToOption);
itemCommand.AddCommand(itemMoveCommand);

var itemRenameCommand = new Command("rename", "Regenerate slug from title, rename the file, and update inbound links.");
var renameIdArg = new Argument<string>("id", "Work item ID.");
var renameTitleOption = new Option<string>("--title", "New title.") { IsRequired = true };
itemRenameCommand.AddArgument(renameIdArg);
itemRenameCommand.AddOption(renameTitleOption);
itemRenameCommand.SetHandler((string? repo, string format, string id, string title) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var updated = WorkItemService.Rename(path, title, config, repoRoot);
        LinkUpdater.UpdateLinks(repoRoot, path, updated.Path);
        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { item = updated } });
        }
        else
        {
            Console.WriteLine($"{updated.Id} renamed to {updated.Path}.");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, renameIdArg, renameTitleOption);
itemCommand.AddCommand(itemRenameCommand);

root.AddCommand(itemCommand);

var addCommand = new Command("add", "Shorthand for item creation.");
Command BuildAddCommand(string typeName)
{
    var cmd = new Command(typeName, $"Alias for workbench item new --type {typeName}.");
    var titleOption = CreateTitleOption();
    var statusOption = CreateStatusOption();
    var priorityOption = CreatePriorityOption();
    var ownerOption = CreateOwnerOption();
    cmd.AddOption(titleOption);
    cmd.AddOption(statusOption);
    cmd.AddOption(priorityOption);
    cmd.AddOption(ownerOption);
    cmd.SetHandler((string? repo, string format, string title, string? status, string? priority, string? owner) =>
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
                return 2;
            }
            var result = WorkItemService.CreateItem(repoRoot, config, typeName, title, status, priority, owner);
            if (resolvedFormat == "json")
            {
                WriteJson(new { ok = true, data = result });
            }
            else
            {
                Console.WriteLine($"{result.Id} created at {result.Path}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 2;
        }
    }, repoOption, formatOption, titleOption, statusOption, priorityOption, ownerOption);
    return cmd;
}
addCommand.AddCommand(BuildAddCommand("task"));
addCommand.AddCommand(BuildAddCommand("bug"));
addCommand.AddCommand(BuildAddCommand("spike"));
root.AddCommand(addCommand);

var boardCommand = new Command("board", "Workboard commands.");
var boardRegenCommand = new Command("regen", "Regenerate work/WORKBOARD.md.");
boardRegenCommand.SetHandler((string? repo, string format) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var result = WorkboardService.Regenerate(repoRoot, config);
        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = result });
        }
        else
        {
            Console.WriteLine($"Workboard regenerated: {result.Path}");
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption);
boardCommand.AddCommand(boardRegenCommand);
root.AddCommand(boardCommand);

var promoteCommand = new Command("promote", "Create a work item, branch, and commit in one step.");
var promoteTypeOption = new Option<string>("--type", "Work item type: bug, task, spike") { IsRequired = true };
promoteTypeOption.AddCompletions("bug", "task", "spike");
var promoteTitleOption = new Option<string>("--title", "Work item title") { IsRequired = true };
var promotePushOption = new Option<bool>("--push", "Push the branch to origin.");
var promoteStartOption = new Option<bool>("--start", "Set status to in-progress.");
var promotePrOption = new Option<bool>("--pr", "Create a GitHub PR.");
var promoteBaseOption = new Option<string?>("--base", "Base branch for PR.");
var promoteDraftOption = new Option<bool>("--draft", "Create a draft PR.");
var promoteNoDraftOption = new Option<bool>("--no-draft", "Create a ready PR.");
promoteCommand.AddOption(promoteTypeOption);
promoteCommand.AddOption(promoteTitleOption);
promoteCommand.AddOption(promotePushOption);
promoteCommand.AddOption(promoteStartOption);
promoteCommand.AddOption(promotePrOption);
promoteCommand.AddOption(promoteBaseOption);
promoteCommand.AddOption(promoteDraftOption);
promoteCommand.AddOption(promoteNoDraftOption);
promoteCommand.SetHandler((
    string? repo,
    string format,
    string type,
    string title,
    bool push,
    bool start,
    bool pr,
    string? baseBranch,
    bool draft,
    bool noDraft) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        if (config.Git.RequireCleanWorkingTree && !GitService.IsClean(repoRoot))
        {
            Console.WriteLine("Working tree is not clean.");
            return 2;
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

        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = true,
                data = new
                {
                    item = created,
                    branch,
                    commit = new { sha, message = commitMessage },
                    pushed = shouldPush,
                    pr = prUrl
                }
            });
        }
        else
        {
            Console.WriteLine($"{item.Id} promoted on {branch}.");
            if (prUrl is not null)
            {
                Console.WriteLine($"PR: {prUrl}");
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, promoteTypeOption, promoteTitleOption, promotePushOption, promoteStartOption, promotePrOption, promoteBaseOption, promoteDraftOption, promoteNoDraftOption);
root.AddCommand(promoteCommand);

var prCommand = new Command("pr", "Pull request commands.");
var prCreateCommand = new Command("create", "Create a GitHub PR via gh and backlink the PR URL.");
var prIdArg = new Argument<string>("id", "Work item ID.");
var prBaseOption = new Option<string?>("--base", "Base branch for PR.");
var prDraftOption = new Option<bool>("--draft", "Create as draft.");
var prFillOption = new Option<bool>("--fill", "Fill PR body from work item.");
prCreateCommand.AddArgument(prIdArg);
prCreateCommand.AddOption(prBaseOption);
prCreateCommand.AddOption(prDraftOption);
prCreateCommand.AddOption(prFillOption);
prCreateCommand.SetHandler((string? repo, string format, string id, string? baseBranch, bool draft, bool fill) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
        var prUrl = CreatePr(repoRoot, config, item, baseBranch, draft, fill);

        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { pr = prUrl, item = item.Id } });
        }
        else
        {
            Console.WriteLine(prUrl);
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, prIdArg, prBaseOption, prDraftOption, prFillOption);
prCommand.AddCommand(prCreateCommand);
root.AddCommand(prCommand);

var createCommand = new Command("create", "Create resources.");
var createPrCommand = new Command("pr", "Alias for workbench pr create.");
createPrCommand.AddArgument(prIdArg);
createPrCommand.AddOption(prBaseOption);
createPrCommand.AddOption(prDraftOption);
createPrCommand.AddOption(prFillOption);
createPrCommand.SetHandler((string? repo, string format, string id, string? baseBranch, bool draft, bool fill) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var path = WorkItemService.GetItemPathById(repoRoot, config, id);
        var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
        var prUrl = CreatePr(repoRoot, config, item, baseBranch, draft, fill);

        if (resolvedFormat == "json")
        {
            WriteJson(new { ok = true, data = new { pr = prUrl, item = item.Id } });
        }
        else
        {
            Console.WriteLine(prUrl);
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, prIdArg, prBaseOption, prDraftOption, prFillOption);
createCommand.AddCommand(createPrCommand);
root.AddCommand(createCommand);

var validateCommand = new Command("validate", "Validate work items, links, and schemas.");
var strictOption = new Option<bool>("--strict", "Treat warnings as errors.");
var verboseOption = new Option<bool>("--verbose", "Show detailed validation output.");
validateCommand.AddOption(strictOption);
validateCommand.AddOption(verboseOption);
validateCommand.AddAlias("verify");
validateCommand.SetHandler((string? repo, string format, bool strict, bool verbose) =>
{
    try
    {
        var repoRoot = ResolveRepo(repo);
        var resolvedFormat = ResolveFormat(format);
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        if (configError is not null)
        {
            Console.WriteLine($"Config error: {configError}");
            return 2;
        }
        var result = ValidationService.ValidateRepo(repoRoot, config);
        var exit = result.Errors.Count > 0 ? 2 : result.Warnings.Count > 0 ? (strict ? 2 : 1) : 0;

        if (resolvedFormat == "json")
        {
            WriteJson(new
            {
                ok = result.Errors.Count == 0,
                data = new
                {
                    errors = result.Errors,
                    warnings = result.Warnings,
                    counts = new
                    {
                        errors = result.Errors.Count,
                        warnings = result.Warnings.Count,
                        workItems = result.WorkItemCount,
                        markdownFiles = result.MarkdownFileCount
                    }
                }
            });
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
        return exit;
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return 2;
    }
}, repoOption, formatOption, strictOption, verboseOption);
root.AddCommand(validateCommand);

return await root.InvokeAsync(args);
