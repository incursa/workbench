using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildFinishCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var finishCommand = new Command(
            "finish",
            "Idempotently record completion evidence, refresh local derived views, and validate a work item.");
        var idArgument = new Argument<string>("id")
        {
            Description = "Work item ID."
        };
        var pullRequestOption = new Option<string?>("--pr")
        {
            Description = "Pull request URL to record as completion evidence."
        };
        var evidenceOption = new Option<string[]>("--evidence")
        {
            Description = "Completion evidence entry. May be specified more than once.",
            AllowMultipleArgumentsPerToken = true
        };
        var noteOption = new Option<string?>("--note")
        {
            Description = "Completion note to record idempotently."
        };
        var profileOption = new Option<string?>("--profile")
        {
            Description = "Validation profile override (core|traceable|auditable)."
        };
        profileOption.CompletionSources.Add("core", "traceable", "auditable");
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report the local changes without writing them."
        };

        finishCommand.Arguments.Add(idArgument);
        finishCommand.Options.Add(pullRequestOption);
        finishCommand.Options.Add(evidenceOption);
        finishCommand.Options.Add(noteOption);
        finishCommand.Options.Add(profileOption);
        finishCommand.Options.Add(dryRunOption);
        finishCommand.SetAction(async parseResult =>
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

                var requestedProfile = parseResult.GetValue(profileOption);
                string? profileError = null;
                var profile = string.IsNullOrWhiteSpace(requestedProfile)
                    ? WorkflowPolicyResolver.Resolve(config.Workflow, config.Validation).ValidationProfile
                    : ValidationProfiles.Resolve(requestedProfile, null, out profileError);
                if (profileError is not null)
                {
                    throw new InvalidOperationException(profileError);
                }
                var data = await FinishService.FinishAsync(
                    repoRoot,
                    config,
                    new FinishOptions(
                        parseResult.GetValue(idArgument) ?? string.Empty,
                        parseResult.GetValue(pullRequestOption),
                        parseResult.GetValue(evidenceOption) ?? Array.Empty<string>(),
                        parseResult.GetValue(noteOption),
                        profile,
                        parseResult.GetValue(dryRunOption))).ConfigureAwait(false);

                if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(
                        new FinishOutput(data.Navigation.Ok && data.Validation.Ok, data),
                        WorkbenchJsonContext.Default.FinishOutput);
                }
                else
                {
                    var verb = data.DryRun ? "Would finish" : "Finished";
                    Console.WriteLine($"{verb} {data.WorkItemId}: {data.PreviousStatus} -> {data.FinalStatus}");
                    Console.WriteLine($"Evidence: {(data.EvidenceChanged ? "updated" : "unchanged")}");
                    Console.WriteLine(data.Navigation.Ok
                        ? $"Navigation: {data.Navigation.IndexFilesUpdated} index file(s) updated"
                        : $"Navigation: failed ({data.Navigation.Error})");
                    Console.WriteLine(data.Validation.Ok
                        ? $"Validation: passed ({data.Validation.Profile})"
                        : $"Validation: failed ({data.Validation.Errors.Count} error(s))");
                    if (!data.Validation.Ok)
                    {
                        foreach (var error in data.Validation.Errors)
                        {
                            Console.WriteLine($"- {error}");
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(data.RecommendedNextAction))
                    {
                        Console.WriteLine($"Next: {data.RecommendedNextAction}");
                    }
                }

                var hasWarnings = data.Validation.Warnings.Count > 0
                    || data.Navigation.Warnings.Count > 0
                    || data.Navigation.MissingDocs.Count > 0
                    || data.Navigation.MissingItems.Count > 0;
                var exitCode = 2;
                if (data.Navigation.Ok && data.Validation.Ok)
                {
                    exitCode = hasWarnings ? 1 : 0;
                }
                SetExitCode(exitCode);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        return finishCommand;
    }
}
