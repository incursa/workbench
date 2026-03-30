using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#pragma warning disable MA0006
#pragma warning disable MA0009
#pragma warning disable MA0011
#pragma warning disable MA0023
#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record QualitySyncOptions(
    string? ContractPath,
    string? ResultsPath,
    string? CoveragePath,
    string? OutDir,
    bool DryRun);

public sealed record QualityShowOptions(
    string Kind,
    string? Path);

public sealed record QualitySyncResult(
    QualitySyncData Data,
    TestInventory Inventory,
    TestRunSummary Results,
    CoverageSummary Coverage,
    QualityReport Report,
    string Markdown);

public sealed record QualityShowResult(QualityShowData Data);

public sealed record QualityAuthoredIntent(
    string ContractPath,
    int? Version,
    string Domain,
    string? SolutionPath,
    IList<string> Includes,
    IList<string> Excludes,
    IList<string> ExpectedEvidence,
    string? ConfidenceTarget,
    double LineMin,
    double BranchMin,
    IList<string> CriticalFiles,
    IList<string> RequiredTests,
    IList<QualityIntentionalGap> IntentionalGaps,
    QualityLinks Links);

public static class QualityService
{
    public const string DefaultContractPath = "quality/testing-intent.yaml";
    public const string DefaultOutputDirectory = "artifacts/quality/testing";
    public const string DefaultInventoryArtifact = "test-inventory.json";
    public const string DefaultResultsArtifact = "test-run-summary.json";
    public const string DefaultCoverageArtifact = "coverage-summary.json";
    public const string DefaultReportArtifact = "quality-report.json";
    public const string DefaultSummaryArtifact = "quality-summary.md";

    private static readonly Regex testAttributeRegex = new(
        @"^\s*\[(?<attribute>Fact|Theory|Test|TestMethod)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex traitAttributeRegex = new(
        @"^\s*\[\s*Trait\s*\(\s*""(?<key>[^""]+)""\s*,\s*""(?<value>[^""]+)""\s*\)\s*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex namespaceRegex = new(
        @"^\s*namespace\s+(?<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex typeRegex = new(
        @"^\s*(?:public|internal|private|protected|sealed|abstract|partial|static|\s)*(?:class|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex methodRegex = new(
        @"^\s*(?:public|internal|private|protected|static|async|virtual|override|sealed|partial|\s)+[\w<>\[\],?.]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex frameworkRegex = new(
        @"\b(net\d+(?:\.\d+)?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static QualitySyncResult Sync(string repoRoot, QualitySyncOptions options)
    {
        var contractPath = ResolvePath(repoRoot, options.ContractPath ?? DefaultContractPath);
        var outputDirectory = ResolvePath(repoRoot, options.OutDir ?? DefaultOutputDirectory);
        var authored = LoadAuthoredIntent(repoRoot, contractPath);
        var inventory = DiscoverTestInventory(repoRoot, authored, "workbench quality sync");
        var requirementTestRefs = RequirementTraceSyncService.BuildRequirementTestRefs(inventory);
        var results = IngestTestRunSummary(
            repoRoot,
            options.ResultsPath,
            inventory.Projects,
            inventory.Tests,
            "workbench quality sync");
        var coverage = IngestCoverageSummary(
            repoRoot,
            options.CoveragePath,
            authored,
            inventory.Projects,
            "workbench quality sync");

        var report = GenerateQualityReport(
            repoRoot,
            authored,
            inventory,
            results,
            coverage,
            outputDirectory);
        var markdown = BuildQualitySummaryMarkdown(report, inventory, results, coverage);

        var inventoryPath = Path.Combine(outputDirectory, DefaultInventoryArtifact);
        var resultsPath = Path.Combine(outputDirectory, DefaultResultsArtifact);
        var coveragePath = Path.Combine(outputDirectory, DefaultCoverageArtifact);
        var reportPath = Path.Combine(outputDirectory, DefaultReportArtifact);
        var markdownPath = Path.Combine(outputDirectory, DefaultSummaryArtifact);
        var warnings = new List<string>();
        warnings.AddRange(inventory.Warnings);
        warnings.AddRange(results.Warnings);
        warnings.AddRange(coverage.Warnings);
        warnings.AddRange(report.Assessment.Findings
            .Where(finding => string.Equals(finding.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.Message));

        if (!options.DryRun)
        {
            Directory.CreateDirectory(outputDirectory);
            WriteArtifact(repoRoot, inventoryPath, inventory, WorkbenchJsonContext.Default.TestInventory, "schemas/test-inventory.schema.json");
            WriteArtifact(repoRoot, resultsPath, results, WorkbenchJsonContext.Default.TestRunSummary, "schemas/test-run-summary.schema.json");
            WriteArtifact(repoRoot, coveragePath, coverage, WorkbenchJsonContext.Default.CoverageSummary, "schemas/coverage-summary.schema.json");
            WriteArtifact(repoRoot, reportPath, report, WorkbenchJsonContext.Default.QualityReport, "schemas/quality-report.schema.json");
            File.WriteAllText(markdownPath, markdown);
            var requirementBackfill = RequirementTraceSyncService.SyncRequirementTestRefs(repoRoot, requirementTestRefs, dryRun: false);
            warnings.AddRange(requirementBackfill.Warnings);
        }

        var data = new QualitySyncData(
            new QualitySyncInventoryData(
                NormalizeRepoPath(repoRoot, inventoryPath),
                inventory.Projects.Count,
                inventory.Tests.Count),
            new QualitySyncResultsData(
                NormalizeRepoPath(repoRoot, resultsPath),
                results.RunId,
                results.Summary.Status,
                results.Summary.Passed,
                results.Summary.Failed,
                results.Summary.Skipped),
            new QualitySyncCoverageData(
                NormalizeRepoPath(repoRoot, coveragePath),
                coverage.Files.Count > 0,
                coverage.Summary.LineRate,
                coverage.Summary.BranchRate),
            new QualitySyncReportData(
                NormalizeRepoPath(repoRoot, reportPath),
                NormalizeRepoPath(repoRoot, markdownPath),
                report.Assessment.Status,
                report.Assessment.Findings.Count),
            warnings,
            options.DryRun);

        return new QualitySyncResult(data, inventory, results, coverage, report with
        {
            MarkdownPath = NormalizeRepoPath(repoRoot, markdownPath)
        }, markdown);
    }

    public static QualityShowResult Show(string repoRoot, QualityShowOptions options)
    {
        var kind = NormalizeKind(options.Kind);
        var defaultPath = kind switch
        {
            "inventory" => Path.Combine(repoRoot, DefaultOutputDirectory, DefaultInventoryArtifact),
            "results" => Path.Combine(repoRoot, DefaultOutputDirectory, DefaultResultsArtifact),
            "coverage" => Path.Combine(repoRoot, DefaultOutputDirectory, DefaultCoverageArtifact),
            _ => Path.Combine(repoRoot, DefaultOutputDirectory, DefaultReportArtifact)
        };

        var artifactPath = ResolvePath(repoRoot, options.Path ?? defaultPath);
        if (!File.Exists(artifactPath))
        {
            throw new FileNotFoundException($"Quality artifact not found: {artifactPath}");
        }

        var normalizedPath = NormalizeRepoPath(repoRoot, artifactPath);
        var data = kind switch
        {
            "inventory" => new QualityShowData(
                kind,
                normalizedPath,
                ReadArtifact(artifactPath, WorkbenchJsonContext.Default.TestInventory),
                null,
                null,
                null,
                null),
            "results" => new QualityShowData(
                kind,
                normalizedPath,
                null,
                ReadArtifact(artifactPath, WorkbenchJsonContext.Default.TestRunSummary),
                null,
                null,
                null),
            "coverage" => new QualityShowData(
                kind,
                normalizedPath,
                null,
                null,
                ReadArtifact(artifactPath, WorkbenchJsonContext.Default.CoverageSummary),
                null,
                null),
            _ => CreateReportShowData(normalizedPath, artifactPath)
        };

        return new QualityShowResult(data);
    }

    public static QualityAuthoredIntent LoadAuthoredIntent(string repoRoot, string contractPath)
    {
        if (!File.Exists(contractPath))
        {
            throw new FileNotFoundException($"Testing intent contract not found: {contractPath}");
        }

        var includes = new List<string>();
        var excludes = new List<string>();
        var expectedEvidence = new List<string>();
        var criticalFiles = new List<string>();
        var requiredTests = new List<string>();
        var docs = new List<string>();
        var workItems = new List<string>();
        var files = new List<string>();
        var codeRefs = new List<string>();
        var intentionalGaps = new List<QualityIntentionalGap>();
        int? version = null;
        string domain = "testing";
        string? solutionPath = null;
        string? confidenceTarget = null;
        double? lineMin = null;
        double? branchMin = null;

        var pathStack = new Stack<(int Indent, string Key)>();
        GapBuilder? activeGap = null;
        int? activeGapIndent = null;

        foreach (var rawLine in File.ReadAllLines(contractPath))
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = CountIndent(rawLine);

            if (activeGap is not null && activeGapIndent.HasValue && indent <= activeGapIndent.Value && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                intentionalGaps.Add(activeGap.Build());
                activeGap = null;
                activeGapIndent = null;
            }

            while (pathStack.Count > 0 && indent <= pathStack.Peek().Indent)
            {
                pathStack.Pop();
            }

            if (activeGap is not null && activeGapIndent.HasValue && indent > activeGapIndent.Value && !trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                var gapScalar = ParseKeyValue(trimmed);
                if (gapScalar is not null)
                {
                    AssignGapProperty(activeGap, gapScalar.Value.Key, gapScalar.Value.Value);
                    continue;
                }
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                var item = Unquote(trimmed[2..].Trim());
                var section = GetCurrentSection(pathStack);
                switch (section)
                {
                    case "scope.includes":
                        includes.Add(NormalizeContractPath(item));
                        break;
                    case "scope.excludes":
                        excludes.Add(NormalizeContractPath(item));
                        break;
                    case "coverage.criticalFiles":
                    case "expectations.criticalFiles":
                        criticalFiles.Add(NormalizeContractPath(item));
                        break;
                    case "scenarios.requiredTests":
                    case "expectations.requiredTests":
                        requiredTests.Add(item);
                        break;
                    case "expectations.evidence":
                        expectedEvidence.Add(item.ToLowerInvariant());
                        break;
                    case "related.docs":
                        docs.Add(item);
                        break;
                    case "related.workItems":
                        workItems.Add(item);
                        break;
                    case "related.files":
                        files.Add(NormalizeContractPath(item));
                        break;
                    case "related.codeRefs":
                        codeRefs.Add(item);
                        break;
                    case "intentionalGaps":
                        if (activeGap is not null)
                        {
                            intentionalGaps.Add(activeGap.Build());
                        }

                        activeGap = new GapBuilder();
                        activeGapIndent = indent;
                        var inline = ParseKeyValue(item);
                        if (inline is not null)
                        {
                            AssignGapProperty(activeGap, inline.Value.Key, inline.Value.Value);
                        }
                        break;
                }

                continue;
            }

            var scalar = ParseKeyValue(trimmed);
            if (scalar is null)
            {
                continue;
            }

            var currentSection = GetCurrentSection(pathStack);
            var fullKey = string.IsNullOrEmpty(currentSection)
                ? scalar.Value.Key
                : $"{currentSection}.{scalar.Value.Key}";

            if (scalar.Value.Value is null)
            {
                pathStack.Push((indent, scalar.Value.Key));
                continue;
            }

            var value = Unquote(scalar.Value.Value);
            switch (fullKey)
            {
                case "version":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedVersion))
                    {
                        version = parsedVersion;
                    }
                    break;
                case "domain":
                    domain = string.IsNullOrWhiteSpace(value) ? "testing" : value;
                    break;
                case "scope.solutionPath":
                    solutionPath = NormalizeContractPath(value);
                    break;
                case "coverage.lineMin":
                    lineMin = ParseDouble(value, lineMin);
                    break;
                case "coverage.branchMin":
                    branchMin = ParseDouble(value, branchMin);
                    break;
                case "expectations.confidenceTarget":
                    confidenceTarget = value;
                    break;
            }
        }

        if (activeGap is not null)
        {
            intentionalGaps.Add(activeGap.Build());
        }

        if (expectedEvidence.Count == 0)
        {
            expectedEvidence.Add("inventory");
            expectedEvidence.Add("results");
            expectedEvidence.Add("coverage");
        }

        return new QualityAuthoredIntent(
            NormalizeRepoPath(repoRoot, contractPath),
            version,
            domain,
            solutionPath,
            includes,
            excludes,
            expectedEvidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            confidenceTarget,
            lineMin ?? 0,
            branchMin ?? 0,
            criticalFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            requiredTests.Distinct(StringComparer.Ordinal).ToList(),
            intentionalGaps,
            new QualityLinks(docs, workItems, files, codeRefs));
    }

    public static TestInventory DiscoverTestInventory(string repoRoot, QualityAuthoredIntent intent, string commandName)
    {
        var projects = new List<TestInventoryProject>();
        var tests = new List<TestInventoryTest>();
        var warnings = new List<string>();
        var solutionPath = ResolveSolutionPath(repoRoot, intent, warnings);
        var projectFiles = EnumerateProjectFiles(repoRoot);

        foreach (var projectPath in projectFiles)
        {
            var normalizedProjectPath = NormalizeRepoPath(repoRoot, projectPath);
            if (!IsIncluded(normalizedProjectPath, intent.Includes, intent.Excludes))
            {
                continue;
            }

            var projectDocument = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            if (!IsTestProject(projectDocument, normalizedProjectPath))
            {
                continue;
            }

            var targetFrameworks = ReadProjectTargetFrameworks(projectDocument);
            var assemblyName = ReadProjectAssemblyName(projectDocument, Path.GetFileNameWithoutExtension(projectPath));
            var projectWarnings = new List<string>
            {
                "Per-test discovery is based on source scanning heuristics in v1 and may be incomplete."
            };

            if (targetFrameworks.Count > 1)
            {
                projectWarnings.Add("Multi-targeted projects are represented with a single per-test target framework in v1 source-scan discovery.");
            }

            var sourceFiles = Directory
                .EnumerateFiles(Path.GetDirectoryName(projectPath) ?? repoRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsGeneratedOrBuildPath(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var projectTests = new List<TestInventoryTest>();
            foreach (var sourcePath in sourceFiles)
            {
                projectTests.AddRange(ScanTestsFromSourceFile(
                    repoRoot,
                    normalizedProjectPath,
                    assemblyName,
                    targetFrameworks,
                    sourcePath));
            }

            if (projectTests.Count == 0)
            {
                projectWarnings.Add("No test methods were detected by the v1 source scan.");
            }

            projects.Add(new TestInventoryProject(
                normalizedProjectPath,
                assemblyName,
                targetFrameworks,
                projectTests.Count,
                "source-scan",
                new Dictionary<string, string[]>(StringComparer.Ordinal),
                projectWarnings));
            tests.AddRange(projectTests);
        }

        if (projects.Count == 0)
        {
            warnings.Add("No test projects were discovered within the authored scope.");
        }

        var frameworks = projects
            .SelectMany(project => project.TargetFrameworks)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(framework => framework, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TestInventory(
            1,
            "testing",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            new QualityArtifactSource(
                commandName,
                typeof(QualityService).Assembly.GetName().Version?.ToString(),
                projects.Select(project => project.ProjectPath).ToList()),
            new TestInventoryScope(
                solutionPath,
                intent.Includes.ToList(),
                intent.Excludes.ToList()),
            new TestInventorySummary(
                projects.Count,
                tests.Count,
                frameworks,
                projects.Sum(project => project.Warnings.Count) + warnings.Count),
            projects,
            tests,
            warnings);
    }

    public static TestRunSummary IngestTestRunSummary(
        string repoRoot,
        string? resultsPath,
        IList<TestInventoryProject> inventoryProjects,
        IList<TestInventoryTest> inventoryTests,
        string commandName)
    {
        var lookupByFqn = inventoryTests
            .GroupBy(test => test.FullyQualifiedName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var lookupByAssembly = inventoryProjects
            .GroupBy(project => project.AssemblyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var requestedPath = ResolvePath(repoRoot, resultsPath ?? repoRoot);
        var trxFiles = EnumerateArtifacts(requestedPath, filePath =>
            string.Equals(Path.GetExtension(filePath), ".trx", StringComparison.OrdinalIgnoreCase));

        var warnings = new List<string>();
        var parsedTests = new List<TestRunTestResult>();
        var observedTimes = new List<DateTimeOffset>();
        var projectArtifactPaths = new Dictionary<(string ProjectPath, string TargetFramework), HashSet<string>>();

        foreach (var trxFile in trxFiles)
        {
            try
            {
                var document = XDocument.Load(trxFile, LoadOptions.PreserveWhitespace);
                var definitionsById = LoadTrxDefinitions(document);
                var artifactPath = NormalizeRepoPath(repoRoot, trxFile);

                var timesElement = DescendantsLocal(document, "Times").FirstOrDefault();
                if (timesElement is not null && TryParseDateTimeOffset(timesElement.Attribute("finish")?.Value, out var finishTime))
                {
                    observedTimes.Add(finishTime);
                }

                foreach (var resultElement in DescendantsLocal(document, "UnitTestResult"))
                {
                    var definition = ResolveTrxDefinition(resultElement, definitionsById);
                    var fullyQualifiedName = ResolveTrxName(resultElement, definition);
                    var mappedTest = lookupByFqn.TryGetValue(fullyQualifiedName, out var inventoryTest)
                        ? inventoryTest
                        : null;

                    var assemblyName = ResolveAssemblyName(definition);
                    var mappedProject = mappedTest is not null
                        ? inventoryProjects.FirstOrDefault(project => string.Equals(project.ProjectPath, mappedTest.ProjectPath, StringComparison.Ordinal))
                        : ResolveProjectFromAssembly(assemblyName, lookupByAssembly);

                    var projectPath = mappedTest?.ProjectPath
                        ?? mappedProject?.ProjectPath
                        ?? "unknown";
                    var targetFramework = mappedTest?.TargetFramework
                        ?? ResolveTargetFramework(definition, mappedProject)
                        ?? "unknown";
                    var outcome = NormalizeOutcome(resultElement.Attribute("outcome")?.Value);
                    var durationMs = ParseDurationMilliseconds(
                        resultElement.Attribute("duration")?.Value,
                        resultElement.Attribute("startTime")?.Value,
                        resultElement.Attribute("endTime")?.Value);
                    var errorMessage = DescendantsLocal(resultElement, "Message").FirstOrDefault()?.Value?.Trim();

                    parsedTests.Add(new TestRunTestResult(
                        mappedTest?.TestId,
                        fullyQualifiedName,
                        resultElement.Attribute("testName")?.Value,
                        projectPath,
                        targetFramework,
                        outcome,
                        durationMs,
                        string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage,
                        artifactPath));

                    var projectKey = (ProjectPath: projectPath, TargetFramework: targetFramework);
                    if (!projectArtifactPaths.TryGetValue(projectKey, out var artifactSet))
                    {
                        artifactSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        projectArtifactPaths[projectKey] = artifactSet;
                    }

                    artifactSet.Add(artifactPath);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse TRX artifact {NormalizeRepoPath(repoRoot, trxFile)}: {ex}");
            }
        }

        if (trxFiles.Count == 0)
        {
            warnings.Add($"No TRX files were found under {NormalizeRepoPath(repoRoot, requestedPath)}.");
        }

        var groupedProjects = parsedTests
            .GroupBy(test => (test.ProjectPath, test.TargetFramework))
            .Select(group =>
            {
                var total = group.Count();
                var passed = group.Count(entry => entry.Outcome == "passed");
                var failed = group.Count(entry => entry.Outcome == "failed");
                var skipped = group.Count(entry => entry.Outcome == "skipped");
                var notExecuted = group.Count(entry => entry.Outcome == "notExecuted");
                var durationMs = group.Sum(entry => entry.DurationMs ?? 0);
                var status = DetermineResultStatus(total, failed, notExecuted, group.Any(entry => entry.Outcome is "timeout" or "aborted" or "unknown"));
                return new TestRunProjectSummary(
                    group.Key.ProjectPath,
                    group.Key.TargetFramework,
                    status,
                    total,
                    passed,
                    failed,
                    skipped,
                    notExecuted,
                    durationMs,
                    projectArtifactPaths.TryGetValue(group.Key, out var artifactSet)
                        ? artifactSet.OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase).ToList()
                        : new List<string>());
            })
            .OrderBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.TargetFramework, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = parsedTests.Count;
        var passedCount = parsedTests.Count(test => test.Outcome == "passed");
        var failedCount = parsedTests.Count(test => test.Outcome == "failed");
        var skippedCount = parsedTests.Count(test => test.Outcome == "skipped");
        var notExecutedCount = parsedTests.Count(test => test.Outcome == "notExecuted");
        var hasPartialOutcomes = parsedTests.Any(test => test.Outcome is "timeout" or "aborted" or "unknown");
        var observedAt = observedTimes.Count == 0
            ? DateTimeOffset.UtcNow
            : observedTimes.Max();

        return new TestRunSummary(
            1,
            "testing",
            BuildRunId(trxFiles.Count == 0 ? null : observedAt),
            observedAt.ToString("O", CultureInfo.InvariantCulture),
            new QualityArtifactSource(
                commandName,
                typeof(QualityService).Assembly.GetName().Version?.ToString(),
                trxFiles.Count == 0
                    ? new List<string> { NormalizeRepoPath(repoRoot, requestedPath) }
                    : trxFiles.Select(file => NormalizeRepoPath(repoRoot, file)).ToList()),
            new TestRunSelection(
                ResolveSolutionPath(repoRoot, null, null),
                inventoryProjects.Select(project => project.ProjectPath).ToList(),
                null),
            new TestRunSummaryCounts(
                DetermineResultStatus(totalCount, failedCount, notExecutedCount, hasPartialOutcomes),
                totalCount,
                passedCount,
                failedCount,
                skippedCount,
                notExecutedCount,
                parsedTests.Sum(test => test.DurationMs ?? 0)),
            groupedProjects,
            parsedTests.OrderBy(test => test.FullyQualifiedName, StringComparer.Ordinal).ToList(),
            warnings);
    }

    public static CoverageSummary IngestCoverageSummary(
        string repoRoot,
        string? coveragePath,
        QualityAuthoredIntent authored,
        IList<TestInventoryProject> inventoryProjects,
        string commandName)
    {
        _ = inventoryProjects;
        var requestedPath = ResolvePath(repoRoot, coveragePath ?? repoRoot);
        var coverageFiles = EnumerateArtifacts(requestedPath, filePath =>
        {
            var name = Path.GetFileName(filePath);
            return name.EndsWith(".cobertura.xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "cobertura.xml", StringComparison.OrdinalIgnoreCase);
        });

        var warnings = new List<string>();
        var fileCounters = new Dictionary<string, CoverageCounter>(StringComparer.OrdinalIgnoreCase);
        var projectCounters = new Dictionary<string, CoverageCounter>(StringComparer.OrdinalIgnoreCase);
        var projectLookup = BuildProjectDirectoryLookup(repoRoot);
        var observedTimes = new List<DateTimeOffset>();

        foreach (var coverageFile in coverageFiles)
        {
            try
            {
                var document = XDocument.Load(coverageFile, LoadOptions.PreserveWhitespace);
                observedTimes.Add(new DateTimeOffset(File.GetLastWriteTimeUtc(coverageFile), TimeSpan.Zero));
                foreach (var classElement in DescendantsLocal(document, "class"))
                {
                    var filename = classElement.Attribute("filename")?.Value;
                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        continue;
                    }

                    var repoPath = NormalizeCoveragePath(repoRoot, filename);
                    if (!ShouldIncludeCoveragePath(repoRoot, repoPath, authored))
                    {
                        continue;
                    }

                    var lines = DescendantsLocal(classElement, "line").ToList();
                    var lineValid = lines.Count;
                    var lineCovered = lines.Count(line => ParseInt(line.Attribute("hits")?.Value) > 0);
                    var branchCovered = 0;
                    var branchValid = 0;

                    foreach (var line in lines.Where(line => string.Equals(line.Attribute("branch")?.Value, "true", StringComparison.OrdinalIgnoreCase)))
                    {
                        var (covered, total) = ParseConditionCoverage(line.Attribute("condition-coverage")?.Value);
                        branchCovered += covered;
                        branchValid += total;
                    }

                    var fileCounter = GetOrAddCounter(fileCounters, repoPath);
                    fileCounter.LinesCovered += lineCovered;
                    fileCounter.LinesValid += lineValid;
                    fileCounter.BranchesCovered += branchCovered;
                    fileCounter.BranchesValid += branchValid;

                    var projectPath = ResolveOwningProject(repoPath, projectLookup);
                    var projectCounter = GetOrAddCounter(projectCounters, projectPath);
                    projectCounter.LinesCovered += lineCovered;
                    projectCounter.LinesValid += lineValid;
                    projectCounter.BranchesCovered += branchCovered;
                    projectCounter.BranchesValid += branchValid;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse coverage artifact {NormalizeRepoPath(repoRoot, coverageFile)}: {ex}");
            }
        }

        if (coverageFiles.Count == 0)
        {
            warnings.Add($"No Cobertura coverage files were found under {NormalizeRepoPath(repoRoot, requestedPath)}.");
        }

        var fileSummaries = fileCounters
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CoverageFileSummary(
                entry.Key,
                entry.Value.LinesCovered,
                entry.Value.LinesValid,
                CalculateRate(entry.Value.LinesCovered, entry.Value.LinesValid, false),
                entry.Value.BranchesCovered,
                entry.Value.BranchesValid,
                CalculateRate(entry.Value.BranchesCovered, entry.Value.BranchesValid, true)))
            .ToList();

        var projectSummaries = projectCounters
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CoverageProjectSummary(
                entry.Key,
                entry.Value.LinesCovered,
                entry.Value.LinesValid,
                CalculateRate(entry.Value.LinesCovered, entry.Value.LinesValid, false),
                entry.Value.BranchesCovered,
                entry.Value.BranchesValid,
                CalculateRate(entry.Value.BranchesCovered, entry.Value.BranchesValid, true)))
            .ToList();

        var totals = new CoverageCounter();
        foreach (var counter in fileCounters.Values)
        {
            totals.LinesCovered += counter.LinesCovered;
            totals.LinesValid += counter.LinesValid;
            totals.BranchesCovered += counter.BranchesCovered;
            totals.BranchesValid += counter.BranchesValid;
        }

        var criticalFiles = authored.CriticalFiles
            .Select(expected =>
            {
                var matched = fileSummaries.FirstOrDefault(file =>
                    string.Equals(file.RepoPath, expected, StringComparison.OrdinalIgnoreCase)
                    || file.RepoPath.EndsWith(expected, StringComparison.OrdinalIgnoreCase));
                if (matched is null)
                {
                    return new CoverageCriticalFileSummary(expected, authored.LineMin, authored.BranchMin, null, null, "missing");
                }

                var status = matched.LineRate >= authored.LineMin && matched.BranchRate >= authored.BranchMin
                    ? "pass"
                    : "fail";
                return new CoverageCriticalFileSummary(expected, authored.LineMin, authored.BranchMin, matched.LineRate, matched.BranchRate, status);
            })
            .ToList();

        var observedAt = observedTimes.Count == 0
            ? DateTimeOffset.UtcNow
            : observedTimes.Max();

        return new CoverageSummary(
            1,
            "testing",
            BuildRunId(coverageFiles.Count == 0 ? null : observedAt),
            observedAt.ToString("O", CultureInfo.InvariantCulture),
            new QualityArtifactSource(
                commandName,
                typeof(QualityService).Assembly.GetName().Version?.ToString(),
                coverageFiles.Count == 0
                    ? new List<string> { NormalizeRepoPath(repoRoot, requestedPath) }
                    : coverageFiles.Select(file => NormalizeRepoPath(repoRoot, file)).ToList()),
            new CoverageSummaryTotals(
                totals.LinesCovered,
                totals.LinesValid,
                CalculateRate(totals.LinesCovered, totals.LinesValid, false),
                totals.BranchesCovered,
                totals.BranchesValid,
                CalculateRate(totals.BranchesCovered, totals.BranchesValid, true)),
            projectSummaries,
            fileSummaries,
            criticalFiles,
            warnings);
    }

    public static QualityReport GenerateQualityReport(
        string repoRoot,
        QualityAuthoredIntent authored,
        TestInventory inventory,
        TestRunSummary results,
        CoverageSummary coverage,
        string outputDirectory)
    {
        var findings = new List<QualityReportFinding>();
        var expectedEvidence = authored.ExpectedEvidence.Select(entry => entry.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (expectedEvidence.Contains("inventory") && inventory.Projects.Count == 0)
        {
            findings.Add(new QualityReportFinding("missing-inventory", "warn", "No test inventory was discovered within the authored scope.", "contract", authored.ContractPath));
        }

        if (expectedEvidence.Contains("results") && results.Summary.Status == "no-data")
        {
            findings.Add(new QualityReportFinding("missing-results", "warn", "No normalized test run evidence is available from TRX inputs.", "report", DefaultResultsArtifact));
        }

        if (expectedEvidence.Contains("coverage") && coverage.Files.Count == 0)
        {
            findings.Add(new QualityReportFinding("missing-coverage", "warn", "No normalized coverage evidence is available from Cobertura inputs.", "report", DefaultCoverageArtifact));
        }

        var inventoryByContractRef = inventory.Tests.ToDictionary(BuildContractTestReference, test => test, StringComparer.Ordinal);
        foreach (var requiredTest in authored.RequiredTests)
        {
            if (!inventoryByContractRef.TryGetValue(requiredTest, out var inventoryTest))
            {
                findings.Add(new QualityReportFinding("required-test-missing-inventory", "error", $"Required test is not present in the discovered inventory: {requiredTest}", "test", requiredTest));
                continue;
            }

            var seenInRun = results.Tests.Any(test =>
                string.Equals(test.TestId, inventoryTest.TestId, StringComparison.Ordinal)
                || string.Equals(test.FullyQualifiedName, inventoryTest.FullyQualifiedName, StringComparison.Ordinal));
            if (!seenInRun)
            {
                findings.Add(new QualityReportFinding("required-test-missing-results", "warn", $"Required test is not present in the latest observed run: {requiredTest}", "test", requiredTest));
            }
        }

        var runProjectPaths = results.Projects.Select(project => project.ProjectPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var project in inventory.Projects.Where(project => !runProjectPaths.Contains(project.ProjectPath)))
        {
            findings.Add(new QualityReportFinding("project-missing-run", "warn", $"Discovered test project has no current run evidence: {project.ProjectPath}", "project", project.ProjectPath));
        }

        foreach (var criticalFile in coverage.CriticalFiles.Where(file => file.Status != "pass"))
        {
            var message = criticalFile.Status == "missing"
                ? $"Critical file is missing from coverage evidence: {criticalFile.RepoPath}"
                : $"Critical file coverage is below the authored threshold: {criticalFile.RepoPath}";
            findings.Add(new QualityReportFinding(
                criticalFile.Status == "missing" ? "critical-file-missing" : "critical-file-under-threshold",
                criticalFile.Status == "missing" ? "warn" : "error",
                message,
                "file",
                criticalFile.RepoPath));
        }

        if (coverage.Files.Count > 0 && coverage.Summary.LineRate < authored.LineMin)
        {
            findings.Add(new QualityReportFinding("coverage-line-under-threshold", "error", $"Observed line coverage {coverage.Summary.LineRate:P1} is below the authored minimum {authored.LineMin:P1}.", "report", DefaultCoverageArtifact));
        }

        if (coverage.Files.Count > 0 && coverage.Summary.BranchRate < authored.BranchMin)
        {
            findings.Add(new QualityReportFinding("coverage-branch-under-threshold", "error", $"Observed branch coverage {coverage.Summary.BranchRate:P1} is below the authored minimum {authored.BranchMin:P1}.", "report", DefaultCoverageArtifact));
        }

        if (results.Summary.Failed > 0)
        {
            findings.Add(new QualityReportFinding("test-failures-present", "error", $"The latest observed run contains {results.Summary.Failed} failed tests.", "report", DefaultResultsArtifact));
        }

        var status = DetermineAssessmentStatus(findings, expectedEvidence, results, coverage);
        var confidenceVerdict = "not-assessed";
        if (authored.ConfidenceTarget is not null)
        {
            confidenceVerdict = findings.Any(finding => finding.Severity is "warn" or "error")
                ? "under-target"
                : "aligned";
        }

        return new QualityReport(
            1,
            "testing",
            BuildReportId(),
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            new QualityReportAuthored(
                authored.ContractPath,
                authored.Version,
                authored.ConfidenceTarget,
                authored.ExpectedEvidence.ToList(),
                authored.RequiredTests.ToList(),
                authored.CriticalFiles.ToList(),
                authored.IntentionalGaps.ToList(),
                authored.Links),
            new QualityReportObserved(
                NormalizeRepoPath(repoRoot, Path.Combine(outputDirectory, DefaultInventoryArtifact)),
                NormalizeRepoPath(repoRoot, Path.Combine(outputDirectory, DefaultResultsArtifact)),
                NormalizeRepoPath(repoRoot, Path.Combine(outputDirectory, DefaultCoverageArtifact)),
                inventory.GeneratedAt,
                results.ObservedAt,
                coverage.ObservedAt,
                new QualityReportObservedSummary(
                    inventory.Tests.Count,
                    results.Summary.Passed,
                    results.Summary.Failed,
                    results.Summary.Skipped,
                    coverage.Files.Count == 0 ? null : coverage.Summary.LineRate,
                    coverage.Files.Count == 0 ? null : coverage.Summary.BranchRate)),
            new QualityReportAssessment(status, confidenceVerdict, findings),
            NormalizeRepoPath(repoRoot, Path.Combine(outputDirectory, DefaultSummaryArtifact)));
    }

    public static string BuildQualitySummaryMarkdown(
        QualityReport report,
        TestInventory inventory,
        TestRunSummary results,
        CoverageSummary coverage)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Quality Summary");
        builder.AppendLine();
        builder.AppendLine($"Generated: {report.GeneratedAt}");
        builder.AppendLine($"Status: `{report.Assessment.Status}`");
        builder.AppendLine($"Confidence: `{report.Assessment.ConfidenceVerdict}`");
        builder.AppendLine();
        builder.AppendLine("## Authored Truth");
        builder.AppendLine($"- Contract: `{report.Authored.ContractPath}`");
        builder.AppendLine($"- Expected evidence: {FormatInlineList(report.Authored.ExpectedEvidence)}");
        builder.AppendLine($"- Confidence target: `{report.Authored.ConfidenceTarget ?? "not-specified"}`");
        builder.AppendLine($"- Required tests: {report.Authored.RequiredTests.Count}");
        builder.AppendLine($"- Critical files: {report.Authored.CriticalFiles.Count}");
        if (report.Authored.IntentionalGaps.Count > 0)
        {
            builder.AppendLine("- Intentional gaps:");
            foreach (var gap in report.Authored.IntentionalGaps)
            {
                builder.AppendLine($"  - `{gap.Subject}`: {gap.Rationale}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Observed Truth");
        builder.AppendLine($"- Inventory: {inventory.Projects.Count} test projects, {inventory.Tests.Count} discovered tests");
        builder.AppendLine($"- Results: {results.Summary.Passed} passed, {results.Summary.Failed} failed, {results.Summary.Skipped} skipped");
        builder.AppendLine($"- Coverage: line {coverage.Summary.LineRate:P1}, branch {coverage.Summary.BranchRate:P1}");
        if (inventory.Warnings.Count > 0 || results.Warnings.Count > 0 || coverage.Warnings.Count > 0)
        {
            builder.AppendLine("- Warnings:");
            foreach (var warning in inventory.Warnings.Concat(results.Warnings).Concat(coverage.Warnings))
            {
                builder.AppendLine($"  - {warning}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Findings");
        if (report.Assessment.Findings.Count == 0)
        {
            builder.AppendLine("- No advisory findings.");
        }
        else
        {
            foreach (var finding in report.Assessment.Findings)
            {
                builder.AppendLine($"- [{finding.Severity}] {finding.Message}");
            }
        }

        return builder.ToString();
    }

    private static QualityShowData CreateReportShowData(string normalizedPath, string artifactPath)
    {
        var report = ReadArtifact(artifactPath, WorkbenchJsonContext.Default.QualityReport);
        return new QualityShowData("report", normalizedPath, null, null, null, report, report.MarkdownPath);
    }

    private static T ReadArtifact<T>(string path, JsonTypeInfo<T> typeInfo) where T : class
    {
        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize(json, typeInfo);
        return value ?? throw new InvalidOperationException($"Failed to deserialize artifact at {path}.");
    }

    private static void WriteArtifact<T>(string repoRoot, string path, T artifact, JsonTypeInfo<T> typeInfo, string schemaRelativePath)
    {
        var json = JsonSerializer.Serialize(artifact, typeInfo);
        var errors = SchemaValidationService.ValidateJsonContent(repoRoot, schemaRelativePath, NormalizeRepoPath(repoRoot, path), json);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        File.WriteAllText(path, json);
    }

    private static string NormalizeKind(string kind)
    {
        var normalized = string.IsNullOrWhiteSpace(kind) ? "report" : kind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "report" or "inventory" or "results" or "coverage" => normalized,
            _ => throw new InvalidOperationException($"Unsupported quality artifact kind '{kind}'. Use report, inventory, results, or coverage.")
        };
    }

    private static string BuildContractTestReference(TestInventoryTest test)
    {
        return string.IsNullOrWhiteSpace(test.SourcePath)
            ? test.DisplayName
            : $"{test.SourcePath}::{test.DisplayName}";
    }

    private static string DetermineAssessmentStatus(
        IList<QualityReportFinding> findings,
        ISet<string> expectedEvidence,
        TestRunSummary results,
        CoverageSummary coverage)
    {
        var missingEvidence = expectedEvidence.Contains("results") && results.Summary.Status == "no-data"
            || expectedEvidence.Contains("coverage") && coverage.Files.Count == 0;
        if (missingEvidence)
        {
            return "incomplete";
        }

        if (findings.Any(finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)))
        {
            return "fail";
        }

        if (findings.Any(finding => string.Equals(finding.Severity, "warn", StringComparison.OrdinalIgnoreCase)))
        {
            return "warn";
        }

        return "ok";
    }

    private static string DetermineResultStatus(int total, int failed, int notExecuted, bool hasPartialOutcomes)
    {
        if (total == 0)
        {
            return "no-data";
        }

        if (failed > 0)
        {
            return "failed";
        }

        if (hasPartialOutcomes || notExecuted > 0)
        {
            return "partial";
        }

        return "passed";
    }

    private static TestInventoryProject? ResolveProjectFromAssembly(string? assemblyName, IReadOnlyDictionary<string, TestInventoryProject> lookupByAssembly)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        return lookupByAssembly.TryGetValue(assemblyName, out var project) ? project : null;
    }

    private static string? ResolveTargetFramework(TrxDefinition? definition, TestInventoryProject? mappedProject)
    {
        if (mappedProject is not null && mappedProject.TargetFrameworks.Count == 1)
        {
            return mappedProject.TargetFrameworks[0];
        }

        var frameworkSource = definition?.CodeBase ?? definition?.Storage;
        if (string.IsNullOrWhiteSpace(frameworkSource))
        {
            return mappedProject?.TargetFrameworks.FirstOrDefault();
        }

        var match = frameworkRegex.Match(frameworkSource);
        return match.Success ? match.Groups[1].Value : mappedProject?.TargetFrameworks.FirstOrDefault();
    }

    private static string ResolveTrxName(XElement resultElement, TrxDefinition? definition)
    {
        var testName = resultElement.Attribute("testName")?.Value;
        if (!string.IsNullOrWhiteSpace(testName) && testName.Contains('.', StringComparison.Ordinal))
        {
            return testName;
        }

        if (!string.IsNullOrWhiteSpace(definition?.ClassName) && !string.IsNullOrWhiteSpace(definition.MethodName))
        {
            return $"{definition.ClassName}.{definition.MethodName}";
        }

        return testName ?? definition?.Name ?? "unknown";
    }

    private static string? ResolveAssemblyName(TrxDefinition? definition)
    {
        var candidate = definition?.Storage ?? definition?.CodeBase;
        return string.IsNullOrWhiteSpace(candidate) ? null : Path.GetFileNameWithoutExtension(candidate);
    }

    private static TrxDefinition? ResolveTrxDefinition(XElement resultElement, IReadOnlyDictionary<string, TrxDefinition> definitionsById)
    {
        var testId = resultElement.Attribute("testId")?.Value;
        if (!string.IsNullOrWhiteSpace(testId) && definitionsById.TryGetValue(testId, out var byTestId))
        {
            return byTestId;
        }

        var executionId = resultElement.Attribute("executionId")?.Value;
        return string.IsNullOrWhiteSpace(executionId)
            ? null
            : definitionsById.Values.FirstOrDefault(definition => string.Equals(definition.ExecutionId, executionId, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, TrxDefinition> LoadTrxDefinitions(XDocument document)
    {
        var definitions = new Dictionary<string, TrxDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var unitTest in DescendantsLocal(document, "UnitTest"))
        {
            var id = unitTest.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var executionId = DescendantsLocal(unitTest, "Execution").FirstOrDefault()?.Attribute("id")?.Value;
            var method = DescendantsLocal(unitTest, "TestMethod").FirstOrDefault();
            definitions[id] = new TrxDefinition(
                id,
                executionId,
                unitTest.Attribute("name")?.Value,
                method?.Attribute("className")?.Value,
                method?.Attribute("name")?.Value,
                method?.Attribute("codeBase")?.Value,
                unitTest.Attribute("storage")?.Value);
        }

        return definitions;
    }

    private static IEnumerable<XElement> DescendantsLocal(XContainer container, string localName)
    {
        return container.Descendants().Where(element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal));
    }

    private static double? ParseDurationMilliseconds(string? durationText, string? startTimeText, string? endTimeText)
    {
        if (!string.IsNullOrWhiteSpace(durationText) && TimeSpan.TryParse(durationText, CultureInfo.InvariantCulture, out var duration))
        {
            return duration.TotalMilliseconds;
        }

        if (TryParseDateTimeOffset(startTimeText, out var start) && TryParseDateTimeOffset(endTimeText, out var end))
        {
            return (end - start).TotalMilliseconds;
        }

        return null;
    }

    private static bool TryParseDateTimeOffset(string? text, out DateTimeOffset value)
    {
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }

    private static string NormalizeOutcome(string? outcome)
    {
        return outcome?.Trim().ToLowerInvariant() switch
        {
            "passed" => "passed",
            "failed" => "failed",
            "notexecuted" or "notrunnable" => "notExecuted",
            "timeout" => "timeout",
            "aborted" => "aborted",
            "inconclusive" or "skipped" => "skipped",
            null or "" => "unknown",
            _ => "unknown"
        };
    }

    private static List<TestInventoryTest> ScanTestsFromSourceFile(
        string repoRoot,
        string projectPath,
        string assemblyName,
        IList<string> targetFrameworks,
        string sourcePath)
    {
        var normalizedSourcePath = NormalizeRepoPath(repoRoot, sourcePath);
        var results = new List<TestInventoryTest>();
        string? currentNamespace = null;
        string? currentType = null;
        var pendingTraits = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var targetFramework = targetFrameworks.FirstOrDefault() ?? "unknown";

        var lines = File.ReadAllLines(sourcePath);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var namespaceMatch = namespaceRegex.Match(line);
            if (namespaceMatch.Success)
            {
                currentNamespace = namespaceMatch.Groups["namespace"].Value;
            }

            var typeMatch = typeRegex.Match(line);
            if (typeMatch.Success)
            {
                currentType = typeMatch.Groups["name"].Value;
            }

            var attributeMatch = testAttributeRegex.Match(line);
            if (attributeMatch.Success)
            {
                AddPendingTrait(pendingTraits, "framework", attributeMatch.Groups["attribute"].Value);
                continue;
            }

            var traitMatch = traitAttributeRegex.Match(line);
            if (traitMatch.Success)
            {
                AddPendingTrait(pendingTraits, traitMatch.Groups["key"].Value, traitMatch.Groups["value"].Value);
                continue;
            }

            if (pendingTraits.Count == 0)
            {
                continue;
            }

            var methodMatch = methodRegex.Match(line);
            if (!methodMatch.Success)
            {
                continue;
            }

            var methodName = methodMatch.Groups["name"].Value;
            var fullyQualifiedName = string.Join(".", new[] { currentNamespace, currentType, methodName }.Where(segment => !string.IsNullOrWhiteSpace(segment)));
            results.Add(new TestInventoryTest(
                $"{projectPath}::{fullyQualifiedName}",
                fullyQualifiedName,
                methodName,
                projectPath,
                assemblyName,
                targetFramework,
                normalizedSourcePath,
                index + 1,
                BuildTraitsDictionary(pendingTraits),
                null));

            pendingTraits.Clear();
        }

        return results;
    }

    private static void AddPendingTrait(IDictionary<string, List<string>> pendingTraits, string key, string value)
    {
        var normalizedKey = key.Trim();
        var normalizedValue = value.Trim();
        if (normalizedKey.Length == 0 || normalizedValue.Length == 0)
        {
            return;
        }

        if (!pendingTraits.TryGetValue(normalizedKey, out var values))
        {
            values = new List<string>();
            pendingTraits[normalizedKey] = values;
        }

        if (!values.Any(entry => string.Equals(entry, normalizedValue, StringComparison.OrdinalIgnoreCase)))
        {
            values.Add(normalizedValue);
        }
    }

    private static IReadOnlyDictionary<string, string[]> BuildTraitsDictionary(IDictionary<string, List<string>> pendingTraits)
    {
        return pendingTraits
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildProjectDirectoryLookup(string repoRoot)
    {
        return EnumerateProjectFiles(repoRoot)
            .Select(path => new
            {
                ProjectPath = NormalizeRepoPath(repoRoot, path),
                DirectoryPath = NormalizeRepoPath(repoRoot, Path.GetDirectoryName(path) ?? repoRoot)
            })
            .ToDictionary(entry => entry.DirectoryPath, entry => entry.ProjectPath, StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveOwningProject(string repoPath, IReadOnlyDictionary<string, string> projectLookup)
    {
        return projectLookup
            .OrderByDescending(entry => entry.Key.Length)
            .FirstOrDefault(entry => repoPath.StartsWith(entry.Key + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(repoPath, entry.Key, StringComparison.OrdinalIgnoreCase))
            .Value ?? "unknown";
    }

    private static CoverageCounter GetOrAddCounter(IDictionary<string, CoverageCounter> counters, string key)
    {
        if (!counters.TryGetValue(key, out var counter))
        {
            counter = new CoverageCounter();
            counters[key] = counter;
        }

        return counter;
    }

    private static (int Covered, int Total) ParseConditionCoverage(string? conditionCoverage)
    {
        if (string.IsNullOrWhiteSpace(conditionCoverage))
        {
            return (0, 0);
        }

        var openParen = conditionCoverage.IndexOf('(');
        var slash = conditionCoverage.IndexOf('/');
        var closeParen = conditionCoverage.IndexOf(')');
        if (openParen < 0 || slash < 0 || closeParen < 0 || slash <= openParen)
        {
            return (0, 0);
        }

        var coveredText = conditionCoverage[(openParen + 1)..slash];
        var totalText = conditionCoverage[(slash + 1)..closeParen];
        return (ParseInt(coveredText), ParseInt(totalText));
    }

    private static double CalculateRate(int covered, int valid, bool treatZeroValidAsFull)
    {
        if (valid <= 0)
        {
            return treatZeroValidAsFull ? 1 : 0;
        }

        return (double)covered / valid;
    }

    private static string NormalizeCoveragePath(string repoRoot, string filename)
    {
        if (Path.IsPathRooted(filename))
        {
            return NormalizeRepoPath(repoRoot, filename);
        }

        var candidate = ResolvePath(repoRoot, filename);
        if (File.Exists(candidate))
        {
            return NormalizeRepoPath(repoRoot, candidate);
        }

        return NormalizeContractPath(filename);
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string GetCurrentSection(Stack<(int Indent, string Key)> pathStack)
    {
        return string.Join(".", pathStack.Reverse().Select(entry => entry.Key));
    }

    private static KeyValuePair<string, string?>? ParseKeyValue(string text)
    {
        var colonIndex = text.IndexOf(':');
        if (colonIndex <= 0)
        {
            return null;
        }

        var key = text[..colonIndex].Trim();
        var valueText = text[(colonIndex + 1)..].Trim();
        return new KeyValuePair<string, string?>(key, valueText.Length == 0 ? null : valueText);
    }

    private static void AssignGapProperty(GapBuilder gap, string key, string? value)
    {
        var normalized = Unquote(value ?? string.Empty);
        switch (key)
        {
            case "subject":
                gap.Subject = normalized;
                break;
            case "rationale":
                gap.Rationale = normalized;
                break;
            case "relatedWorkItem":
                gap.RelatedWorkItem = normalized;
                break;
        }
    }

    private static double? ParseDouble(string value, double? fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static bool IsIncluded(string path, IList<string> includes, IList<string> excludes)
    {
        var included = includes.Count == 0 || includes.Any(include => MatchesScopePattern(path, include));
        if (!included)
        {
            return false;
        }

        return !excludes.Any(exclude => MatchesScopePattern(path, exclude));
    }

    private static string? ResolveSolutionPath(string repoRoot, QualityAuthoredIntent? intent, IList<string>? warnings)
    {
        if (!string.IsNullOrWhiteSpace(intent?.SolutionPath))
        {
            var authoredPath = intent.SolutionPath!;
            var resolvedPath = ResolvePath(repoRoot, authoredPath);
            if (File.Exists(resolvedPath))
            {
                return NormalizeRepoPath(repoRoot, resolvedPath);
            }

            warnings?.Add($"Authored solutionPath was not found: {authoredPath}");
            return authoredPath;
        }

        return FindSolutionPath(repoRoot) is { } discovered
            ? NormalizeRepoPath(repoRoot, discovered)
            : null;
    }

    private static string? FindSolutionPath(string repoRoot)
    {
        return Directory
            .EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories)
            .Where(path =>
                !IsGeneratedOrBuildPath(path)
                && (string.Equals(Path.GetExtension(path), ".sln", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetExtension(path), ".slnx", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static bool MatchesScopePattern(string path, string pattern)
    {
        var normalizedPath = NormalizeContractPath(path);
        var normalizedPattern = NormalizeContractPath(pattern);
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return false;
        }

        if (!ContainsWildcard(normalizedPattern))
        {
            return string.Equals(normalizedPath, normalizedPattern, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedPattern + "/", StringComparison.OrdinalIgnoreCase);
        }

        var expression = "^" + Regex.Escape(normalizedPattern)
            .Replace(@"\*\*/", "(?:.*/)?")
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/]*")
            .Replace(@"\?", @"[^/]") + "$";

        return Regex.IsMatch(
            normalizedPath,
            expression,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ShouldIncludeCoveragePath(string repoRoot, string repoPath, QualityAuthoredIntent authored)
    {
        if (IsGeneratedOrBuildPath(Path.Combine(repoRoot, repoPath)))
        {
            return false;
        }

        if (authored.Includes.Count > 0 && !authored.Includes.Any(include => MatchesScopePattern(repoPath, include)))
        {
            return false;
        }

        if (authored.Excludes.Any(exclude => MatchesScopePattern(repoPath, exclude)))
        {
            return false;
        }

        if (authored.IntentionalGaps.Any(gap => MatchesScopePattern(repoPath, gap.Subject)))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsWildcard(string value)
    {
        return value.Contains('*', StringComparison.Ordinal) || value.Contains('?', StringComparison.Ordinal);
    }

    private static List<string> EnumerateProjectFiles(string repoRoot)
    {
        return Directory
            .EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> EnumerateArtifacts(string path, Func<string, bool> predicate)
    {
        if (File.Exists(path))
        {
            return predicate(path) ? new List<string> { path } : new List<string>();
        }

        if (!Directory.Exists(path))
        {
            return new List<string>();
        }

        return Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedOrBuildPath(file))
            .Where(predicate)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsGeneratedOrBuildPath(string path)
    {
        var segments = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.None);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTestProject(XDocument projectDocument, string projectPath)
    {
        var root = projectDocument.Root;
        if (root is not null)
        {
            var sdk = root.Attribute("Sdk")?.Value;
            if (!string.IsNullOrWhiteSpace(sdk) && sdk.Contains("MSTest.Sdk", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var descendants = projectDocument.Descendants().ToList();
        if (descendants.Any(element =>
                string.Equals(element.Name.LocalName, "IsTestProject", StringComparison.Ordinal)
                && string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (descendants.Any(element =>
                string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal)
                && (element.Attribute("Include")?.Value?.Contains("xunit", StringComparison.OrdinalIgnoreCase) == true
                    || element.Attribute("Include")?.Value?.Contains("NUnit", StringComparison.OrdinalIgnoreCase) == true
                    || element.Attribute("Include")?.Value?.Contains("MSTest", StringComparison.OrdinalIgnoreCase) == true
                    || element.Attribute("Include")?.Value?.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) == true)))
        {
            return true;
        }

        return projectPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ReadProjectTargetFrameworks(XDocument projectDocument)
    {
        var frameworks = projectDocument.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "TargetFramework", StringComparison.Ordinal)
                || string.Equals(element.Name.LocalName, "TargetFrameworks", StringComparison.Ordinal))
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (frameworks.Count == 0)
        {
            frameworks.Add("unknown");
        }

        return frameworks;
    }

    private static string ReadProjectAssemblyName(XDocument projectDocument, string fallback)
    {
        return projectDocument.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "AssemblyName", StringComparison.Ordinal))
            ?.Value
            ?.Trim()
            ?? fallback;
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string NormalizeRepoPath(string repoRoot, string path)
    {
        var full = Path.GetFullPath(path);
        var repoFull = Path.GetFullPath(repoRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
        {
            return full.Replace('\\', '/');
        }

        return Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
    }

    private static string NormalizeContractPath(string path)
    {
        return path.Replace('\\', '/').Trim().TrimStart('.', '/');
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string BuildRunId(DateTimeOffset? time)
    {
        var value = time ?? DateTimeOffset.UtcNow;
        return value.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    }

    private static string BuildReportId()
    {
        return $"quality-{DateTimeOffset.UtcNow.ToUniversalTime():yyyyMMddTHHmmssZ}";
    }

    private static string FormatInlineList(IList<string> values)
    {
        return values.Count == 0 ? "none" : string.Join(", ", values.Select(value => $"`{value}`"));
    }

    private sealed class GapBuilder
    {
        public string Subject { get; set; } = string.Empty;

        public string Rationale { get; set; } = string.Empty;

        public string? RelatedWorkItem { get; set; }

        public QualityIntentionalGap Build()
        {
            return new QualityIntentionalGap(
                string.IsNullOrWhiteSpace(Subject) ? "unspecified" : Subject,
                string.IsNullOrWhiteSpace(Rationale) ? "No rationale provided." : Rationale,
                string.IsNullOrWhiteSpace(RelatedWorkItem) ? null : RelatedWorkItem);
        }
    }

    private sealed record TrxDefinition(
        string Id,
        string? ExecutionId,
        string? Name,
        string? ClassName,
        string? MethodName,
        string? CodeBase,
        string? Storage);

    private sealed class CoverageCounter
    {
        public int LinesCovered { get; set; }

        public int LinesValid { get; set; }

        public int BranchesCovered { get; set; }

        public int BranchesValid { get; set; }
    }
}
