using System.CommandLine;

static int RunStub(string commandName)
{
    Console.WriteLine($"workbench {commandName}: not implemented");
    return 0;
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
versionCommand.SetHandler(() => RunStub("version"));
root.AddCommand(versionCommand);

var doctorCommand = new Command("doctor", "Check git, config, and expected paths.");
doctorCommand.SetHandler(() => RunStub("doctor"));
root.AddCommand(doctorCommand);

var scaffoldForceOption = new Option<bool>("--force", "Overwrite existing files.");
var scaffoldCommand = new Command("scaffold", "Create the default folder structure, templates, and config.");
scaffoldCommand.AddOption(scaffoldForceOption);
scaffoldCommand.AddAlias("init");
scaffoldCommand.SetHandler(() => RunStub("scaffold"));
root.AddCommand(scaffoldCommand);

var configCommand = new Command("config", "Configuration commands.");
var configShowCommand = new Command("show", "Print effective config (defaults + repo config + CLI overrides).");
configShowCommand.SetHandler(() => RunStub("config show"));
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

static Option<string> CreateStatusOption()
{
    var option = new Option<string>(
        name: "--status",
        description: "Work item status");
    option.AddCompletions("draft", "ready", "in-progress", "blocked", "done", "dropped");
    return option;
}

static Option<string> CreatePriorityOption()
{
    var option = new Option<string>(
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
itemNewCommand.SetHandler(() => RunStub("item new"));
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
itemListCommand.SetHandler(() => RunStub("item list"));
itemCommand.AddCommand(itemListCommand);

var itemShowCommand = new Command("show", "Show metadata and resolved path for an item.");
var itemIdArg = new Argument<string>("id", "Work item ID (e.g., TASK-0042).");
itemShowCommand.AddArgument(itemIdArg);
itemShowCommand.SetHandler(() => RunStub("item show"));
itemCommand.AddCommand(itemShowCommand);

var itemStatusCommand = new Command("status", "Update status and updated date.");
var statusIdArg = new Argument<string>("id", "Work item ID.");
var statusValueArg = new Argument<string>("status", "New status.");
var noteOption = new Option<string?>("--note", "Append a note.");
itemStatusCommand.AddArgument(statusIdArg);
itemStatusCommand.AddArgument(statusValueArg);
itemStatusCommand.AddOption(noteOption);
itemStatusCommand.SetHandler(() => RunStub("item status"));
itemCommand.AddCommand(itemStatusCommand);

var itemCloseCommand = new Command("close", "Set status to done; optionally move to work/done.");
var closeIdArg = new Argument<string>("id", "Work item ID.");
var moveOption = new Option<bool>("--move", "Move to work/done.");
itemCloseCommand.AddArgument(closeIdArg);
itemCloseCommand.AddOption(moveOption);
itemCloseCommand.SetHandler(() => RunStub("item close"));
itemCommand.AddCommand(itemCloseCommand);

var itemMoveCommand = new Command("move", "Move a work item file and update inbound links where possible.");
var moveIdArg = new Argument<string>("id", "Work item ID.");
var moveToOption = new Option<string>("--to", "Destination path.") { IsRequired = true };
itemMoveCommand.AddArgument(moveIdArg);
itemMoveCommand.AddOption(moveToOption);
itemMoveCommand.SetHandler(() => RunStub("item move"));
itemCommand.AddCommand(itemMoveCommand);

var itemRenameCommand = new Command("rename", "Regenerate slug from title, rename the file, and update inbound links.");
var renameIdArg = new Argument<string>("id", "Work item ID.");
var renameTitleOption = new Option<string>("--title", "New title.") { IsRequired = true };
itemRenameCommand.AddArgument(renameIdArg);
itemRenameCommand.AddOption(renameTitleOption);
itemRenameCommand.SetHandler(() => RunStub("item rename"));
itemCommand.AddCommand(itemRenameCommand);

root.AddCommand(itemCommand);

var addCommand = new Command("add", "Shorthand for item creation.");
Command BuildAddCommand(string typeName)
{
    var cmd = new Command(typeName, $"Alias for workbench item new --type {typeName}.");
    cmd.AddOption(CreateTitleOption());
    cmd.AddOption(CreateStatusOption());
    cmd.AddOption(CreatePriorityOption());
    cmd.AddOption(CreateOwnerOption());
    cmd.SetHandler(() => RunStub($"add {typeName}"));
    return cmd;
}
addCommand.AddCommand(BuildAddCommand("task"));
addCommand.AddCommand(BuildAddCommand("bug"));
addCommand.AddCommand(BuildAddCommand("spike"));
root.AddCommand(addCommand);

var boardCommand = new Command("board", "Workboard commands.");
var boardRegenCommand = new Command("regen", "Regenerate work/WORKBOARD.md.");
boardRegenCommand.SetHandler(() => RunStub("board regen"));
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
promoteCommand.SetHandler(() => RunStub("promote"));
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
prCreateCommand.SetHandler(() => RunStub("pr create"));
prCommand.AddCommand(prCreateCommand);
root.AddCommand(prCommand);

var createCommand = new Command("create", "Create resources.");
var createPrCommand = new Command("pr", "Alias for workbench pr create.");
createPrCommand.AddArgument(prIdArg);
createPrCommand.AddOption(prBaseOption);
createPrCommand.AddOption(prDraftOption);
createPrCommand.AddOption(prFillOption);
createPrCommand.SetHandler(() => RunStub("create pr"));
createCommand.AddCommand(createPrCommand);
root.AddCommand(createCommand);

var validateCommand = new Command("validate", "Validate work items, links, and schemas.");
var strictOption = new Option<bool>("--strict", "Treat warnings as errors.");
validateCommand.AddOption(strictOption);
validateCommand.AddAlias("verify");
validateCommand.SetHandler(() => RunStub("validate"));
root.AddCommand(validateCommand);

return await root.InvokeAsync(args);
