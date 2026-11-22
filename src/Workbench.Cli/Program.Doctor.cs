using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildDoctorCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var command = new Command(
            "doctor",
            "Check repository health and optionally repair deterministic local drift.");
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Output machine-readable JSON (legacy alias for --format json)."
        };
        var fixOption = new Option<bool>("--fix")
        {
            Description = "Repair deterministic local scaffolding and generated-navigation drift."
        };
        command.Options.Add(jsonOption);
        command.Options.Add(fixOption);
        command.SetAction(async parseResult =>
        {
            try
            {
                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var jsonOutput = parseResult.GetValue(jsonOption) ||
                    string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);
                var result = await DoctorService.RunAsync(
                    repoRoot,
                    parseResult.GetValue(fixOption)).ConfigureAwait(false);

                if (jsonOutput)
                {
                    WriteJson(
                        new DoctorOutput(!result.HasErrors, result.Data),
                        WorkbenchJsonContext.Default.DoctorOutput);
                }
                else
                {
                    Console.WriteLine($"Repo: {result.Data.RepoRoot}");
                    foreach (var check in result.Data.Checks)
                    {
                        Console.WriteLine(FormatDoctorCheck(check));
                    }
                    foreach (var repair in result.Data.Repairs)
                    {
                        Console.WriteLine($"Repaired: {repair}");
                    }
                    if (!string.IsNullOrWhiteSpace(result.Data.RecommendedNextAction))
                    {
                        Console.WriteLine($"Next: {result.Data.RecommendedNextAction}");
                    }
                }

                var exitCode = 0;
                if (result.HasErrors)
                {
                    exitCode = 2;
                }
                else if (result.HasWarnings)
                {
                    exitCode = 1;
                }
                SetExitCode(exitCode);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        return command;
    }

    private static string FormatDoctorCheck(DoctorCheck check)
    {
        var detail = check.Details?.Error ?? check.Details?.Reason;
        if (string.IsNullOrWhiteSpace(detail) && check.Details?.Missing is { Count: > 0 })
        {
            detail = string.Join(", ", check.Details.Missing);
        }
        return string.IsNullOrWhiteSpace(detail)
            ? $"{check.Name}: {check.Status}"
            : $"{check.Name}: {check.Status} ({detail})";
    }
}
