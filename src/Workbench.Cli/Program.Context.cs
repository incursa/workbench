using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildContextCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var contextCommand = new Command(
            "context",
            "Show concise repository policy, active work, applicable artifacts, and one recommended next action.");
        var workItemOption = new Option<string?>("--work-item")
        {
            Description = "Select a work item explicitly instead of inferring it from the current branch."
        };
        contextCommand.Options.Add(workItemOption);
        contextCommand.SetAction(parseResult =>
        {
            try
            {
                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    throw new InvalidOperationException($"Config error: {configError}");
                }

                var data = ContextService.GetContext(
                    repoRoot,
                    config,
                    parseResult.GetValue(workItemOption));

                if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(new ContextOutput(true, data), WorkbenchJsonContext.Default.ContextOutput);
                }
                else
                {
                    Console.WriteLine($"Policy: {data.Policy.Level} (validation: {data.Policy.ValidationProfile})");
                    Console.WriteLine($"Branch: {data.Repository.Branch ?? "unknown"}");
                    Console.WriteLine(data.SelectedWorkItem is null
                        ? $"Active work: {data.ActiveWork.Count}"
                        : $"Work item: {data.SelectedWorkItem.Id} [{data.SelectedWorkItem.Status}] {data.SelectedWorkItem.Title}");
                    if (data.ApplicableArtifacts.Count > 0)
                    {
                        Console.WriteLine($"Applicable: {string.Join(", ", data.ApplicableArtifacts.Select(entry => entry.Reference))}");
                    }
                    Console.WriteLine($"Next: {data.RecommendedNextAction.Command}");
                    Console.WriteLine($"Reason: {data.RecommendedNextAction.Reason}");
                }

                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        return contextCommand;
    }
}
