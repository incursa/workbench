using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildQualityCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var qualityCommand = new Command("quality", "Group: repo-native quality evidence commands.");

        var syncCommand = new Command("sync", "Discover testing evidence, ingest normalized artifacts, and generate the current quality report.");
        var contractOption = new Option<string?>("--contract")
        {
            Description = "Authored testing intent contract path.",
            DefaultValueFactory = _ => QualityService.DefaultContractPath
        };
        var resultsOption = new Option<string?>("--results")
        {
            Description = "TRX file or directory root to ingest."
        };
        var coverageOption = new Option<string?>("--coverage")
        {
            Description = "Cobertura file or directory root to ingest."
        };
        var outDirOption = new Option<string?>("--out-dir")
        {
            Description = "Directory for normalized quality artifacts.",
            DefaultValueFactory = _ => QualityService.DefaultOutputDirectory
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Compute the quality artifacts without writing files."
        };
        syncCommand.Options.Add(contractOption);
        syncCommand.Options.Add(resultsOption);
        syncCommand.Options.Add(coverageOption);
        syncCommand.Options.Add(outDirOption);
        syncCommand.Options.Add(dryRunOption);
        syncCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                _ = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var result = QualityService.Sync(
                    repoRoot,
                    new QualitySyncOptions(
                        parseResult.GetValue(contractOption),
                        parseResult.GetValue(resultsOption),
                        parseResult.GetValue(coverageOption),
                        parseResult.GetValue(outDirOption),
                        parseResult.GetValue(dryRunOption)));

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(new QualitySyncOutput(true, result.Data), WorkbenchJsonContext.Default.QualitySyncOutput);
                }
                else
                {
                    Console.WriteLine($"Inventory: {result.Data.Inventory.Projects} projects, {result.Data.Inventory.Tests} tests");
                    Console.WriteLine($"Results: {result.Data.Results.Status} ({result.Data.Results.Passed} passed, {result.Data.Results.Failed} failed, {result.Data.Results.Skipped} skipped)");
                    Console.WriteLine($"Coverage: {(result.Data.Coverage.Available ? $"line {result.Data.Coverage.LineRate:P1}, branch {result.Data.Coverage.BranchRate:P1}" : "no data")}");
                    Console.WriteLine($"Report: {result.Data.Report.Status} with {result.Data.Report.Findings} findings");
                    Console.WriteLine($"Artifacts: {result.Data.Report.JsonPath}");
                    Console.WriteLine($"Summary: {result.Data.Report.MarkdownPath}");
                    if (result.Data.Warnings.Count > 0)
                    {
                        Console.WriteLine("Warnings:");
                        foreach (var warning in result.Data.Warnings)
                        {
                            Console.WriteLine($"- {warning}");
                        }
                    }
                    if (result.Data.DryRun)
                    {
                        Console.WriteLine("Dry run: no files were written.");
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

        var showCommand = new Command("show", "Read the latest normalized quality artifact or a selected evidence kind.");
        var kindOption = new Option<string>("--kind")
        {
            Description = "Artifact kind to show (report|inventory|results|coverage).",
            DefaultValueFactory = _ => "report"
        };
        kindOption.CompletionSources.Add("report", "inventory", "results", "coverage");
        var pathOption = new Option<string?>("--path")
        {
            Description = "Optional explicit artifact path to read."
        };
        showCommand.Options.Add(kindOption);
        showCommand.Options.Add(pathOption);
        showCommand.SetAction(parseResult =>
        {
            try
            {
                var repo = parseResult.GetValue(repoOption);
                var format = parseResult.GetValue(formatOption) ?? "table";
                var repoRoot = ResolveRepo(repo);
                var resolvedFormat = ResolveFormat(format);
                _ = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    Console.WriteLine($"Config error: {configError}");
                    SetExitCode(2);
                    return;
                }

                var result = QualityService.Show(
                    repoRoot,
                    new QualityShowOptions(
                        parseResult.GetValue(kindOption) ?? "report",
                        parseResult.GetValue(pathOption)));

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(new QualityShowOutput(true, result.Data), WorkbenchJsonContext.Default.QualityShowOutput);
                }
                else
                {
                    WriteQualityTable(result.Data);
                }

                SetExitCode(0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });

        qualityCommand.Subcommands.Add(syncCommand);
        qualityCommand.Subcommands.Add(showCommand);
        qualityCommand.SetAction(parseResult =>
        {
            Console.WriteLine("Use `workbench quality sync` to generate testing evidence or `workbench quality show` to inspect it.");
            SetExitCode(0);
        });

        return qualityCommand;
    }

    static void WriteQualityTable(QualityShowData data)
    {
        Console.WriteLine($"Kind: {data.Kind}");
        Console.WriteLine($"Path: {data.Path}");

        if (data.Report is not null)
        {
            Console.WriteLine($"Status: {data.Report.Assessment.Status}");
            Console.WriteLine($"Confidence: {data.Report.Assessment.ConfidenceVerdict}");
            Console.WriteLine($"Observed tests: {data.Report.Observed.Summary.DiscoveredTests}");
            Console.WriteLine($"Results: {data.Report.Observed.Summary.Passed} passed, {data.Report.Observed.Summary.Failed} failed, {data.Report.Observed.Summary.Skipped} skipped");
            if (data.Report.Observed.Summary.LineRate.HasValue && data.Report.Observed.Summary.BranchRate.HasValue)
            {
                Console.WriteLine($"Coverage: line {data.Report.Observed.Summary.LineRate.Value:P1}, branch {data.Report.Observed.Summary.BranchRate.Value:P1}");
            }

            if (data.Report.Assessment.Findings.Count > 0)
            {
                Console.WriteLine("Findings:");
                foreach (var finding in data.Report.Assessment.Findings)
                {
                    Console.WriteLine($"- [{finding.Severity}] {finding.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(data.MarkdownPath))
            {
                Console.WriteLine($"Markdown summary: {data.MarkdownPath}");
            }

            return;
        }

        if (data.Inventory is not null)
        {
            Console.WriteLine($"Projects: {data.Inventory.Projects.Count}");
            Console.WriteLine($"Tests: {data.Inventory.Tests.Count}");
            if (data.Inventory.Summary.Frameworks.Count > 0)
            {
                Console.WriteLine($"Frameworks: {string.Join(", ", data.Inventory.Summary.Frameworks)}");
            }

            WriteQualityEntries("Warnings", data.Inventory.Warnings);
            return;
        }

        if (data.Results is not null)
        {
            Console.WriteLine($"Run: {data.Results.RunId}");
            Console.WriteLine($"Status: {data.Results.Summary.Status}");
            Console.WriteLine($"Passed: {data.Results.Summary.Passed}");
            Console.WriteLine($"Failed: {data.Results.Summary.Failed}");
            Console.WriteLine($"Skipped: {data.Results.Summary.Skipped}");
            WriteQualityEntries("Warnings", data.Results.Warnings);
            return;
        }

        if (data.Coverage is not null)
        {
            Console.WriteLine($"Line coverage: {data.Coverage.Summary.LineRate:P1}");
            Console.WriteLine($"Branch coverage: {data.Coverage.Summary.BranchRate:P1}");
            if (data.Coverage.CriticalFiles.Count > 0)
            {
                Console.WriteLine("Critical files:");
                foreach (var file in data.Coverage.CriticalFiles)
                {
                    Console.WriteLine($"- {file.RepoPath}: {file.Status}");
                }
            }
            WriteQualityEntries("Warnings", data.Coverage.Warnings);
        }
    }

    static void WriteQualityEntries(string label, IEnumerable<string> entries)
    {
        var list = entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
        if (list.Count == 0)
        {
            return;
        }

        Console.WriteLine($"{label}:");
        foreach (var entry in list)
        {
            Console.WriteLine($"- {entry}");
        }
    }
}
