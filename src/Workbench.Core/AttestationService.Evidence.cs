using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

#pragma warning disable ERP022

namespace Workbench.Core;

public static partial class AttestationService
{
    private static AttestationEvidenceSnapshot BuildEvidenceSnapshot(
        string repoRoot,
        AttestationConfig attestationConfig,
        QualityAuthoredIntent authoredIntent,
        AttestationRunOptions options,
        IList<string> warnings)
    {
        if (options.Exec && options.NoExec)
        {
            throw new InvalidOperationException("Attestation execution options conflict: use either --exec or --no-exec, not both.");
        }

        var testResultsRoot = ResolveEvidencePath(repoRoot, options.ResultsPath, attestationConfig.TestResultsRoots, DefaultTestResultsRoots);
        var coverageRoot = ResolveEvidencePath(repoRoot, options.CoveragePath, attestationConfig.CoverageRoots, DefaultCoverageRoots);
        var benchmarkRoot = ResolveEvidencePath(repoRoot, options.BenchmarksPath, attestationConfig.BenchmarkRoots, DefaultBenchmarkRoots);
        var manualQaRoot = ResolveEvidencePath(repoRoot, options.ManualQaPath, attestationConfig.ManualQaRoots, DefaultManualQaRoots);
        var qualityReportPath = FindFirstExistingFile(repoRoot, attestationConfig.QualityTestingRoots, "quality-report.json", DefaultQualityReportRoots);

        var inventory = QualityService.DiscoverTestInventory(repoRoot, authoredIntent, "workbench quality attest");
        var testResults = QualityService.IngestTestRunSummary(repoRoot, testResultsRoot, inventory.Projects, inventory.Tests, "workbench quality attest");
        var coverage = QualityService.IngestCoverageSummary(repoRoot, coverageRoot, authoredIntent, inventory.Projects, "workbench quality attest");

        var qualityReport = BuildQualityReportEvidenceSummary(repoRoot, qualityReportPath);
        var testSummary = BuildTestEvidenceSummary(testResultsRoot, testResults);
        var coverageSummary = BuildCoverageEvidenceSummary(coverageRoot, coverage, authoredIntent, attestationConfig);
        var benchmarksSummary = BuildSimpleEvidenceSummary(repoRoot, "benchmarks", benchmarkRoot, attestationConfig.StaleAfterDays);
        var manualQaSummary = BuildSimpleEvidenceSummary(repoRoot, "manual-qa", manualQaRoot, attestationConfig.StaleAfterDays);
        var execution = options.Exec && !options.NoExec
            ? ExecuteConfiguredEvidenceCommands(repoRoot, attestationConfig, warnings)
            : new AttestationExecutionSummary(false, false, new List<AttestationExecutionCommandResult>(), new List<string>());

        return new AttestationEvidenceSnapshot(qualityReport, testSummary, coverageSummary, benchmarksSummary, manualQaSummary, execution);
    }

    private static QualityAuthoredIntent LoadQualityIntent(string repoRoot, AttestationConfig attestationConfig, IList<string> warnings)
    {
        var candidates = new List<string>();
        candidates.AddRange(attestationConfig.QualityTestingRoots);
        candidates.AddRange(DefaultQualityReportRoots);

        var contractPath = FindFirstExistingPath(repoRoot, candidates, QualityService.DefaultContractPath);
        try
        {
            return QualityService.LoadAuthoredIntent(repoRoot, contractPath);
        }
        catch (Exception ex)
        {
            warnings.Add(ex.ToString());
            return new QualityAuthoredIntent(
                NormalizeRepoPath(repoRoot, contractPath),
                null,
                "testing",
                null,
                new List<string>(),
                new List<string>(),
                new List<string>(),
                null,
                0,
                0,
                new List<string>(),
                new List<string>(),
                new List<QualityIntentionalGap>(),
                new QualityLinks(new List<string>(), new List<string>(), new List<string>(), new List<string>()));
        }
    }

    private static AttestationQualityReportEvidenceSummary BuildQualityReportEvidenceSummary(string repoRoot, string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            return new AttestationQualityReportEvidenceSummary(false, null, null, null, null, null, null, null, null, null, new List<string> { "No normalized quality report found." });
        }

        try
        {
            var show = QualityService.Show(repoRoot, new QualityShowOptions("report", reportPath)).Data;
            var report = show.Report;
            if (report is null)
            {
                return new AttestationQualityReportEvidenceSummary(false, NormalizeRepoPath(repoRoot, reportPath), null, null, null, null, null, null, null, null, new List<string> { "Normalized quality report file could not be read." });
            }

            return new AttestationQualityReportEvidenceSummary(
                true,
                show.Path,
                report.Assessment.Status,
                report.Assessment.ConfidenceVerdict,
                report.GeneratedAt,
                report.Observed.Summary.Passed,
                report.Observed.Summary.Failed,
                report.Observed.Summary.Skipped,
                report.Observed.Summary.LineRate,
                report.Observed.Summary.BranchRate,
                report.Assessment.Findings
                    .Where(finding => string.Equals(finding.Severity, "warn", StringComparison.OrdinalIgnoreCase))
                    .Select(finding => finding.Message)
                    .ToList());
        }
        catch (Exception ex)
        {
            return new AttestationQualityReportEvidenceSummary(false, NormalizeRepoPath(repoRoot, reportPath), null, null, null, null, null, null, null, null, new List<string> { ex.ToString() });
        }
    }

    private static AttestationTestEvidenceSummary BuildTestEvidenceSummary(string? resultsPath, TestRunSummary results)
    {
        var present = !string.Equals(results.Summary.Status, "no-data", StringComparison.OrdinalIgnoreCase) || results.Tests.Count > 0;
        return new AttestationTestEvidenceSummary(
            present,
            string.IsNullOrWhiteSpace(resultsPath) ? null : resultsPath,
            results.Summary.Status,
            results.ObservedAt,
            results.Summary.Passed,
            results.Summary.Failed,
            results.Summary.Skipped,
            results.Warnings.ToList());
    }

    private static AttestationCoverageEvidenceSummary BuildCoverageEvidenceSummary(
        string? coveragePath,
        CoverageSummary coverage,
        QualityAuthoredIntent authoredIntent,
        AttestationConfig attestationConfig)
    {
        var present = coverage.Files.Count > 0;
        var status = DetermineCoverageStatus(present, coverage.Summary.LineRate, coverage.Summary.BranchRate, authoredIntent, attestationConfig, coverage.ObservedAt);
        return new AttestationCoverageEvidenceSummary(
            present,
            string.IsNullOrWhiteSpace(coveragePath) ? null : coveragePath,
            status,
            coverage.ObservedAt,
            coverage.Summary.LineRate,
            coverage.Summary.BranchRate,
            present ? coverage.Summary.LineRate >= authoredIntent.LineMin : null,
            present ? coverage.Summary.BranchRate >= authoredIntent.BranchMin : null,
            coverage.Warnings.ToList());
    }

    private static AttestationSimpleEvidenceSummary BuildSimpleEvidenceSummary(
        string repoRoot,
        string kind,
        string rootPath,
        int? staleAfterDays)
    {
        var files = EnumerateEvidenceFiles(rootPath);
        if (files.Count == 0)
        {
            return new AttestationSimpleEvidenceSummary(
                kind,
                false,
                "unknown",
                new List<string>(),
                null,
                new List<string> { $"No {kind} evidence files found." },
                new List<string>());
        }

        var statuses = files.Select(ReadEvidenceStatus).Where(status => !string.IsNullOrWhiteSpace(status)).Select(status => status!.ToLowerInvariant()).ToList();
        var observedAt = files
            .Select(GetFileTimestamp)
            .Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault())
            .OrderByDescending(value => value)
            .FirstOrDefault();
        var status = DetermineSimpleEvidenceStatus(statuses, observedAt, staleAfterDays);

        return new AttestationSimpleEvidenceSummary(
            kind,
            true,
            status,
            files.Select(file => NormalizeRepoPath(repoRoot, file)).ToList(),
            observedAt == default ? null : observedAt.ToString("O", CultureInfo.InvariantCulture),
            new List<string> { $"{files.Count} file(s) discovered." },
            new List<string>());
    }

    private static string DetermineCoverageStatus(
        bool present,
        double lineRate,
        double branchRate,
        QualityAuthoredIntent authoredIntent,
        AttestationConfig attestationConfig,
        string observedAt)
    {
        if (!present)
        {
            return "unknown";
        }

        if (attestationConfig.StaleAfterDays.HasValue &&
            TryParseDateTimeOffset(observedAt, out var observed) &&
            observed < DateTimeOffset.UtcNow.AddDays(-attestationConfig.StaleAfterDays.Value))
        {
            return "stale";
        }

        if (lineRate < authoredIntent.LineMin || branchRate < authoredIntent.BranchMin)
        {
            return "failing";
        }

        return "passing";
    }

    private static string DetermineSimpleEvidenceStatus(IReadOnlyList<string> statuses, DateTimeOffset observedAt, int? staleAfterDays)
    {
        if (statuses.Any(status => FailingBenchmarkStatuses.Contains(status)))
        {
            return "failing";
        }

        if (statuses.Any(status => StaleBenchmarkStatuses.Contains(status)))
        {
            return "stale";
        }

        if (staleAfterDays.HasValue && observedAt != default && observedAt < DateTimeOffset.UtcNow.AddDays(-staleAfterDays.Value))
        {
            return "stale";
        }

        if (statuses.Any(status => PendingBenchmarkStatuses.Contains(status)))
        {
            return "pending";
        }

        if (statuses.Any(status => PassingBenchmarkStatuses.Contains(status)))
        {
            return "passing";
        }

        return "unknown";
    }

    private static AttestationExecutionSummary ExecuteConfiguredEvidenceCommands(
        string repoRoot,
        AttestationConfig config,
        IList<string> warnings)
    {
        var commandSpecs = new List<(string Kind, IList<AttestationExecutionCommandSpec> Commands)>
        {
            ("tests", config.TestCommands),
            ("coverage", config.CoverageCommands),
            ("benchmarks", config.BenchmarkCommands),
            ("manual-qa", config.ManualQaCommands)
        };

        var results = new List<AttestationExecutionCommandResult>();
        foreach (var (kind, commands) in commandSpecs)
        {
            foreach (var command in commands)
            {
                results.Add(RunCommand(repoRoot, kind, command, warnings));
            }
        }

        return new AttestationExecutionSummary(true, true, results, warnings.ToList());
    }

    private static AttestationExecutionCommandResult RunCommand(
        string repoRoot,
        string kind,
        AttestationExecutionCommandSpec commandSpec,
        IList<string> warnings)
    {
        var workingDirectory = string.IsNullOrWhiteSpace(commandSpec.WorkingDirectory)
            ? repoRoot
            : ResolvePath(repoRoot, commandSpec.WorkingDirectory);
        var commandFileName = ResolveCommandFileName(workingDirectory, commandSpec.Command);

        try
        {
            var startInfo = new ProcessStartInfo(commandFileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var arg in commandSpec.Args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new InvalidOperationException($"Failed to start {kind} command '{commandSpec.Command}'.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                warnings.Add($"{kind} command '{commandSpec.Command}' exited with code {process.ExitCode}.");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                warnings.Add($"{kind} command '{commandSpec.Command}' emitted diagnostics.");
            }

            _ = stdout;

            return new AttestationExecutionCommandResult(
                kind,
                commandSpec.Command,
                commandSpec.Args.ToList(),
                NormalizeRepoPath(repoRoot, workingDirectory),
                process.ExitCode,
                process.ExitCode == 0 ? "passed" : "failed");
        }
        catch (Exception ex)
        {
            warnings.Add($"{kind} command '{commandSpec.Command}' failed to start: {ex}");
            return new AttestationExecutionCommandResult(
                kind,
                commandSpec.Command,
                commandSpec.Args.ToList(),
                NormalizeRepoPath(repoRoot, workingDirectory),
                -1,
                "failed");
        }
    }

    private static string ResolveEvidencePath(string repoRoot, string? overridePath, IList<string> configuredRoots, string[] defaultRoots)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return ResolvePath(repoRoot, overridePath);
        }

        foreach (var root in configuredRoots.Concat(defaultRoots))
        {
            var candidate = ResolvePath(repoRoot, root);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return ResolvePath(repoRoot, defaultRoots[0]);
    }

    private static string? FindFirstExistingFile(string repoRoot, IList<string> configuredRoots, string fileName, string[] defaultRoots)
    {
        foreach (var root in configuredRoots.Concat(defaultRoots))
        {
            var candidate = ResolvePath(repoRoot, root);
            if (File.Exists(candidate) && string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            var nested = Path.Combine(candidate, fileName);
            if (File.Exists(nested))
            {
                return nested;
            }
        }

        return null;
    }

    private static string FindFirstExistingPath(string repoRoot, IList<string> roots, string relativePath)
    {
        foreach (var root in roots)
        {
            var candidate = ResolvePath(repoRoot, root);
            var path = File.Exists(candidate) ? candidate : Path.Combine(candidate, relativePath);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return ResolvePath(repoRoot, relativePath);
    }

    private static string ReadEvidenceStatus(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            if (FrontMatter.TryParse(content, out var frontMatter, out _))
            {
                return DetermineSimpleStatusFromContent(frontMatter!.Data.TryGetValue("status", out var statusValue) ? statusValue?.ToString() : null);
            }

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;
                foreach (var key in new[] { "status", "result", "outcome", "verdict" })
                {
                    if (root.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
                    {
                        var candidate = DetermineSimpleStatusFromContent(property.GetString());
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
        }
        catch
        {
            // Best-effort parsing only.
        }

        return string.Empty;
    }

    private static string DetermineSimpleStatusFromContent(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var normalized = status.Trim().ToLowerInvariant();
        if (PassingBenchmarkStatuses.Contains(normalized))
        {
            return "passing";
        }

        if (FailingBenchmarkStatuses.Contains(normalized))
        {
            return "failing";
        }

        if (PendingBenchmarkStatuses.Contains(normalized))
        {
            return "pending";
        }

        if (StaleBenchmarkStatuses.Contains(normalized))
        {
            return "stale";
        }

        return string.Empty;
    }

    private static DateTimeOffset? GetFileTimestamp(string path)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseDateTimeOffset(string value, out DateTimeOffset parsed)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed);
    }

    private static List<string> EnumerateEvidenceFiles(string rootPath)
    {
        var resolvedRoot = Path.GetFullPath(rootPath);
        if (File.Exists(resolvedRoot))
        {
            return new List<string> { resolvedRoot };
        }

        if (!Directory.Exists(resolvedRoot))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(resolvedRoot, "*", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveConfigPath(string repoRoot, string? configPath)
    {
        var target = string.IsNullOrWhiteSpace(configPath) ? AttestationConfig.DefaultConfigPath : configPath.Trim();
        return ResolvePath(repoRoot, target);
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string NormalizeRepoPath(string repoRoot, string path)
    {
        var full = Path.GetFullPath(path);
        var repoFull = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
        {
            return full.Replace('\\', '/');
        }

        return Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
    }

    private static string ResolveCommandFileName(string workingDirectory, string command)
    {
        if (Path.IsPathRooted(command))
        {
            return Path.GetFullPath(command);
        }

        if (command.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) || command.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(workingDirectory, command));
        }

        return command;
    }

    private static string? TryReadGitValue(string repoRoot, params string[] args)
    {
        try
        {
            var result = GitService.Run(repoRoot, args);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut)
                ? result.StdOut.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsGeneratedOrBuildPath(string path)
    {
        var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }
}

#pragma warning restore ERP022
