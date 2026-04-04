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
        var syncRequirementCommentsOption = new Option<bool>("--sync-requirement-comments")
        {
            Description = "Synchronize generated XML-style requirement comment blocks into test source files."
        };
        syncCommand.Options.Add(contractOption);
        syncCommand.Options.Add(resultsOption);
        syncCommand.Options.Add(coverageOption);
        syncCommand.Options.Add(outDirOption);
        syncCommand.Options.Add(dryRunOption);
        syncCommand.Options.Add(syncRequirementCommentsOption);
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
                        parseResult.GetValue(dryRunOption),
                        parseResult.GetValue(syncRequirementCommentsOption)));

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
                    if (result.Data.TraceSync is not null)
                    {
                        Console.WriteLine($"Trace sync: specs {result.Data.TraceSync.Specifications.FilesUpdated} files, {result.Data.TraceSync.Specifications.RequirementsUpdated} requirements");
                        if (result.Data.TraceSync.TestRequirementComments is null)
                        {
                            Console.WriteLine("Requirement comments: not requested");
                        }
                        else
                        {
                            Console.WriteLine($"Requirement comments: {result.Data.TraceSync.TestRequirementComments.FilesUpdated} files, {result.Data.TraceSync.TestRequirementComments.RequirementsUpdated} requirements");
                        }
                    }
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

        var attestCommand = new Command("attest", "Generate a derived repository evidence snapshot as HTML and JSON.");
        var attestScopeOption = new Option<string[]>("--scope")
        {
            Description = "Repo-relative path prefixes or files to include in the snapshot scope.",
            AllowMultipleArgumentsPerToken = true
        };
        var attestProfileOption = new Option<string>("--profile")
        {
            Description = "Validation profile to use for the snapshot (core|traceable|auditable)."
        };
        attestProfileOption.CompletionSources.Add("core", "traceable", "auditable");
        var emitOption = new Option<string>("--emit")
        {
            Description = "Derived output format to write (html|json|both).",
            DefaultValueFactory = _ => "both"
        };
        emitOption.CompletionSources.Add("html", "json", "both");
        var attestOutDirOption = new Option<string?>("--out-dir")
        {
            Description = "Directory for derived attestation artifacts.",
            DefaultValueFactory = _ => AttestationService.DefaultOutputDirectory
        };
        var attestConfigOption = new Option<string?>("--config")
        {
            Description = "Optional attestation config path.",
            DefaultValueFactory = _ => AttestationConfig.DefaultConfigPath
        };
        var attestResultsOption = new Option<string?>("--results")
        {
            Description = "TRX file or directory root to ingest for evidence."
        };
        var attestCoverageOption = new Option<string?>("--coverage")
        {
            Description = "Cobertura file or directory root to ingest for evidence."
        };
        var benchmarksOption = new Option<string?>("--benchmarks")
        {
            Description = "Benchmark evidence file or directory root to inspect."
        };
        var manualQaOption = new Option<string?>("--manual-qa")
        {
            Description = "Manual QA evidence file or directory root to inspect."
        };
        var execOption = new Option<bool>("--exec")
        {
            Description = "Run configured evidence refresh commands before generating the snapshot."
        };
        var noExecOption = new Option<bool>("--no-exec")
        {
            Description = "Do not execute configured evidence refresh commands."
        };
        attestCommand.Options.Add(attestScopeOption);
        attestCommand.Options.Add(attestProfileOption);
        attestCommand.Options.Add(emitOption);
        attestCommand.Options.Add(attestOutDirOption);
        attestCommand.Options.Add(attestConfigOption);
        attestCommand.Options.Add(attestResultsOption);
        attestCommand.Options.Add(attestCoverageOption);
        attestCommand.Options.Add(benchmarksOption);
        attestCommand.Options.Add(manualQaOption);
        attestCommand.Options.Add(execOption);
        attestCommand.Options.Add(noExecOption);
        attestCommand.SetAction(parseResult =>
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

                var result = AttestationService.Generate(
                    repoRoot,
                    new AttestationRunOptions(
                        (parseResult.GetValue(attestScopeOption) ?? Array.Empty<string>()).ToList(),
                        parseResult.GetValue(attestProfileOption),
                        parseResult.GetValue(emitOption) ?? "both",
                        parseResult.GetValue(attestOutDirOption) ?? AttestationService.DefaultOutputDirectory,
                        parseResult.GetValue(attestConfigOption),
                        parseResult.GetValue(attestResultsOption),
                        parseResult.GetValue(attestCoverageOption),
                        parseResult.GetValue(benchmarksOption),
                        parseResult.GetValue(manualQaOption),
                        parseResult.GetValue(execOption),
                        parseResult.GetValue(noExecOption)));

                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(
                        new AttestationOutput(
                            true,
                            new AttestationRunData(
                                result.Snapshot,
                                result.SummaryHtmlPath,
                                result.DetailsHtmlPath,
                                result.JsonPath,
                                result.Warnings.ToList())),
                        AttestationJsonContext.Default.AttestationOutput);
                }
                else
                {
                    WriteAttestationTable(result);
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
        qualityCommand.Subcommands.Add(attestCommand);
        qualityCommand.Subcommands.Add(showCommand);
        qualityCommand.SetAction(parseResult =>
        {
            Console.WriteLine("Use `workbench quality sync` to generate testing evidence, `workbench quality attest` to generate a repository evidence snapshot, or `workbench quality show` to inspect normalized artifacts.");
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

    static void WriteAttestationTable(AttestationRunResult result)
    {
        var snapshot = result.Snapshot;
        var trace = snapshot.Aggregates.TraceCoverage;
        var workItems = snapshot.Aggregates.WorkItemStatuses;
        var verifications = snapshot.Aggregates.VerificationStatuses;

        Console.WriteLine($"Repository: {snapshot.Repository.Root}");
        Console.WriteLine($"Profile: {snapshot.Selection.Profile}");
        Console.WriteLine($"Scope: {(snapshot.Selection.Scope.Count == 0 ? "entire repository" : string.Join(", ", snapshot.Selection.Scope))}");
        Console.WriteLine($"Requirements: {snapshot.Aggregates.Requirements}");
        Console.WriteLine($"Trace coverage: {trace.WithSatisfiedBy}/{trace.Requirements} satisfied, {trace.WithImplementedBy}/{trace.Requirements} implemented, {trace.WithVerifiedBy}/{trace.Requirements} verified");
        Console.WriteLine($"Direct refs: {trace.WithTestRefs}/{trace.Requirements} test refs, {trace.WithCodeRefs}/{trace.Requirements} code refs");
        Console.WriteLine($"Work items: {workItems.Done} done, {workItems.InProgress} in progress, {workItems.Open} open, {workItems.Blocked} blocked, {workItems.Unknown} unknown");
        Console.WriteLine($"Verification artifacts: {verifications.Passing} passing, {verifications.Failing} failing, {verifications.Pending} pending, {verifications.Stale} stale, {verifications.Unknown} unknown");
        Console.WriteLine($"Evidence: tests {snapshot.Evidence.TestResults.Status ?? "unknown"}, coverage {snapshot.Evidence.Coverage.Status ?? "unknown"}, benchmarks {snapshot.Evidence.Benchmarks.Status ?? "unknown"}, manual QA {snapshot.Evidence.ManualQa.Status ?? "unknown"}");

        if (snapshot.Evidence.Execution.Requested)
        {
            Console.WriteLine($"Execution: {(snapshot.Evidence.Execution.Performed ? "performed" : "requested")}, {snapshot.Evidence.Execution.Commands.Count} command(s)");
        }

        if (!string.IsNullOrWhiteSpace(result.SummaryHtmlPath))
        {
            Console.WriteLine($"Summary HTML: {result.SummaryHtmlPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.DetailsHtmlPath))
        {
            Console.WriteLine($"Details HTML: {result.DetailsHtmlPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.JsonPath))
        {
            Console.WriteLine($"JSON snapshot: {result.JsonPath}");
        }

        WriteQualityEntries("Warnings", result.Warnings);
    }
}
