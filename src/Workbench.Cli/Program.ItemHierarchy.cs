using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildItemHierarchyCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var hierarchyCommand = new Command(
            "hierarchy",
            "Show or update optional parent and initiative work-item references without propagating status.");

        var showCommand = new Command("show", "Show authored hierarchy references and derived local children.");
        var showIdArgument = new Argument<string>("id") { Description = "Work item ID." };
        showCommand.Arguments.Add(showIdArgument);
        showCommand.SetAction(parseResult =>
        {
            try
            {
                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var config = LoadHierarchyConfig(repoRoot);
                var data = WorkItemHierarchyService.Show(repoRoot, config, parseResult.GetValue(showIdArgument) ?? string.Empty);
                WriteHierarchy(data, format);
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        hierarchyCommand.Subcommands.Add(showCommand);

        var setCommand = new Command("set", "Atomically replace optional parent or initiative references.");
        var setIdArgument = new Argument<string>("id") { Description = "Work item ID." };
        var parentOption = new Option<string?>("--parent")
        {
            Description = "Local WI ID or workbench://host/owner/repository/WI-ID parent reference."
        };
        var clearParentOption = new Option<bool>("--clear-parent")
        {
            Description = "Remove the parent reference."
        };
        var initiativeOption = new Option<string[]>("--initiative")
        {
            Description = "Ordered local WI ID or cross-repository initiative reference. Repeat as needed."
        };
        var clearInitiativesOption = new Option<bool>("--clear-initiatives")
        {
            Description = "Remove all initiative references."
        };
        setCommand.Arguments.Add(setIdArgument);
        setCommand.Options.Add(parentOption);
        setCommand.Options.Add(clearParentOption);
        setCommand.Options.Add(initiativeOption);
        setCommand.Options.Add(clearInitiativesOption);
        setCommand.SetAction(parseResult =>
        {
            try
            {
                var parent = parseResult.GetValue(parentOption);
                var clearParent = parseResult.GetValue(clearParentOption);
                var initiatives = parseResult.GetValue(initiativeOption) ?? [];
                var clearInitiatives = parseResult.GetValue(clearInitiativesOption);
                if (!string.IsNullOrWhiteSpace(parent) && clearParent)
                {
                    throw new InvalidOperationException("Choose either --parent or --clear-parent, not both.");
                }
                if (initiatives.Length > 0 && clearInitiatives)
                {
                    throw new InvalidOperationException("Choose either --initiative or --clear-initiatives, not both.");
                }

                var updateParent = !string.IsNullOrWhiteSpace(parent) || clearParent;
                var updateInitiatives = initiatives.Length > 0 || clearInitiatives;
                if (!updateParent && !updateInitiatives)
                {
                    throw new InvalidOperationException(
                        "Specify at least one change: --parent, --clear-parent, --initiative, or --clear-initiatives.");
                }

                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var config = LoadHierarchyConfig(repoRoot);
                var data = WorkItemHierarchyService.Set(
                    repoRoot,
                    config,
                    parseResult.GetValue(setIdArgument) ?? string.Empty,
                    updateParent,
                    clearParent ? null : parent,
                    updateInitiatives,
                    clearInitiatives ? [] : initiatives);
                WriteHierarchy(data, format);
                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        hierarchyCommand.Subcommands.Add(setCommand);

        return hierarchyCommand;
    }

    private static WorkbenchConfig LoadHierarchyConfig(string repoRoot)
    {
        var config = WorkbenchConfig.Load(repoRoot, out var configError);
        return configError is null
            ? config
            : throw new InvalidOperationException($"Config error: {configError}");
    }

    private static void WriteHierarchy(WorkItemHierarchyData data, string format)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(
                new WorkItemHierarchyOutput(true, data),
                WorkbenchJsonContext.Default.WorkItemHierarchyOutput);
            return;
        }

        Console.WriteLine($"Work item: {data.WorkItemId}");
        Console.WriteLine($"Parent: {data.ParentRef ?? "-"}");
        Console.WriteLine($"Initiatives: {(data.InitiativeRefs.Count == 0 ? "-" : string.Join(", ", data.InitiativeRefs))}");
        Console.WriteLine($"Local children: {(data.ChildRefs.Count == 0 ? "-" : string.Join(", ", data.ChildRefs))}");
    }
}
