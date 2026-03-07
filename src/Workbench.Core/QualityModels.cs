using System.Text.Json.Serialization;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record QualityArtifactSource(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("toolVersion")] string? ToolVersion,
    [property: JsonPropertyName("inputPaths")] IList<string> InputPaths);

public sealed record TestInventoryScope(
    [property: JsonPropertyName("solutionPath")] string? SolutionPath,
    [property: JsonPropertyName("includes")] IList<string> Includes,
    [property: JsonPropertyName("excludes")] IList<string> Excludes);

public sealed record TestInventorySummary(
    [property: JsonPropertyName("projects")] int Projects,
    [property: JsonPropertyName("tests")] int Tests,
    [property: JsonPropertyName("frameworks")] IList<string> Frameworks,
    [property: JsonPropertyName("discoveryWarnings")] int DiscoveryWarnings);

public sealed record TestInventoryProject(
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("assemblyName")] string AssemblyName,
    [property: JsonPropertyName("targetFrameworks")] IList<string> TargetFrameworks,
    [property: JsonPropertyName("testCount")] int TestCount,
    [property: JsonPropertyName("discoveryMethod")] string DiscoveryMethod,
    [property: JsonPropertyName("traits")] IReadOnlyDictionary<string, string[]> Traits,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record TestInventoryClassification(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("source")] string Source);

public sealed record TestInventoryTest(
    [property: JsonPropertyName("testId")] string TestId,
    [property: JsonPropertyName("fullyQualifiedName")] string FullyQualifiedName,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("assemblyName")] string AssemblyName,
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("sourcePath")] string? SourcePath,
    [property: JsonPropertyName("line")] int? Line,
    [property: JsonPropertyName("traits")] IReadOnlyDictionary<string, string[]> Traits,
    [property: JsonPropertyName("classification")] TestInventoryClassification? Classification);

public sealed record TestInventory(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("source")] QualityArtifactSource Source,
    [property: JsonPropertyName("scope")] TestInventoryScope Scope,
    [property: JsonPropertyName("summary")] TestInventorySummary Summary,
    [property: JsonPropertyName("projects")] IList<TestInventoryProject> Projects,
    [property: JsonPropertyName("tests")] IList<TestInventoryTest> Tests,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record TestRunSelection(
    [property: JsonPropertyName("solutionPath")] string? SolutionPath,
    [property: JsonPropertyName("projectPaths")] IList<string> ProjectPaths,
    [property: JsonPropertyName("filter")] string? Filter);

public sealed record TestRunSummaryCounts(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("notExecuted")] int NotExecuted,
    [property: JsonPropertyName("durationMs")] double DurationMs);

public sealed record TestRunProjectSummary(
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("notExecuted")] int NotExecuted,
    [property: JsonPropertyName("durationMs")] double DurationMs,
    [property: JsonPropertyName("artifactPaths")] IList<string> ArtifactPaths);

public sealed record TestRunTestResult(
    [property: JsonPropertyName("testId")] string? TestId,
    [property: JsonPropertyName("fullyQualifiedName")] string FullyQualifiedName,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("durationMs")] double? DurationMs,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("artifactPath")] string? ArtifactPath);

public sealed record TestRunSummary(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("observedAt")] string ObservedAt,
    [property: JsonPropertyName("source")] QualityArtifactSource Source,
    [property: JsonPropertyName("selection")] TestRunSelection Selection,
    [property: JsonPropertyName("summary")] TestRunSummaryCounts Summary,
    [property: JsonPropertyName("projects")] IList<TestRunProjectSummary> Projects,
    [property: JsonPropertyName("tests")] IList<TestRunTestResult> Tests,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record CoverageSummaryTotals(
    [property: JsonPropertyName("linesCovered")] int LinesCovered,
    [property: JsonPropertyName("linesValid")] int LinesValid,
    [property: JsonPropertyName("lineRate")] double LineRate,
    [property: JsonPropertyName("branchesCovered")] int BranchesCovered,
    [property: JsonPropertyName("branchesValid")] int BranchesValid,
    [property: JsonPropertyName("branchRate")] double BranchRate);

public sealed record CoverageProjectSummary(
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("linesCovered")] int LinesCovered,
    [property: JsonPropertyName("linesValid")] int LinesValid,
    [property: JsonPropertyName("lineRate")] double LineRate,
    [property: JsonPropertyName("branchesCovered")] int BranchesCovered,
    [property: JsonPropertyName("branchesValid")] int BranchesValid,
    [property: JsonPropertyName("branchRate")] double BranchRate);

public sealed record CoverageFileSummary(
    [property: JsonPropertyName("repoPath")] string RepoPath,
    [property: JsonPropertyName("linesCovered")] int LinesCovered,
    [property: JsonPropertyName("linesValid")] int LinesValid,
    [property: JsonPropertyName("lineRate")] double LineRate,
    [property: JsonPropertyName("branchesCovered")] int BranchesCovered,
    [property: JsonPropertyName("branchesValid")] int BranchesValid,
    [property: JsonPropertyName("branchRate")] double BranchRate);

public sealed record CoverageCriticalFileSummary(
    [property: JsonPropertyName("repoPath")] string RepoPath,
    [property: JsonPropertyName("lineMin")] double LineMin,
    [property: JsonPropertyName("branchMin")] double BranchMin,
    [property: JsonPropertyName("lineRate")] double? LineRate,
    [property: JsonPropertyName("branchRate")] double? BranchRate,
    [property: JsonPropertyName("status")] string Status);

public sealed record CoverageSummary(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("observedAt")] string ObservedAt,
    [property: JsonPropertyName("source")] QualityArtifactSource Source,
    [property: JsonPropertyName("summary")] CoverageSummaryTotals Summary,
    [property: JsonPropertyName("projects")] IList<CoverageProjectSummary> Projects,
    [property: JsonPropertyName("files")] IList<CoverageFileSummary> Files,
    [property: JsonPropertyName("criticalFiles")] IList<CoverageCriticalFileSummary> CriticalFiles,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record QualityLinks(
    [property: JsonPropertyName("docs")] IList<string> Docs,
    [property: JsonPropertyName("workItems")] IList<string> WorkItems,
    [property: JsonPropertyName("files")] IList<string> Files,
    [property: JsonPropertyName("codeRefs")] IList<string> CodeRefs);

public sealed record QualityIntentionalGap(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("relatedWorkItem")] string? RelatedWorkItem);

public sealed record QualityReportAuthored(
    [property: JsonPropertyName("contractPath")] string ContractPath,
    [property: JsonPropertyName("version")] int? Version,
    [property: JsonPropertyName("confidenceTarget")] string? ConfidenceTarget,
    [property: JsonPropertyName("expectedEvidence")] IList<string> ExpectedEvidence,
    [property: JsonPropertyName("requiredTests")] IList<string> RequiredTests,
    [property: JsonPropertyName("criticalFiles")] IList<string> CriticalFiles,
    [property: JsonPropertyName("intentionalGaps")] IList<QualityIntentionalGap> IntentionalGaps,
    [property: JsonPropertyName("links")] QualityLinks Links);

public sealed record QualityReportObservedSummary(
    [property: JsonPropertyName("discoveredTests")] int DiscoveredTests,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("lineRate")] double? LineRate,
    [property: JsonPropertyName("branchRate")] double? BranchRate);

public sealed record QualityReportObserved(
    [property: JsonPropertyName("inventoryPath")] string? InventoryPath,
    [property: JsonPropertyName("runSummaryPath")] string? RunSummaryPath,
    [property: JsonPropertyName("coverageSummaryPath")] string? CoverageSummaryPath,
    [property: JsonPropertyName("inventoryGeneratedAt")] string? InventoryGeneratedAt,
    [property: JsonPropertyName("runObservedAt")] string? RunObservedAt,
    [property: JsonPropertyName("coverageObservedAt")] string? CoverageObservedAt,
    [property: JsonPropertyName("summary")] QualityReportObservedSummary Summary);

public sealed record QualityReportFinding(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("subjectType")] string? SubjectType,
    [property: JsonPropertyName("subjectRef")] string? SubjectRef);

public sealed record QualityReportAssessment(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("confidenceVerdict")] string ConfidenceVerdict,
    [property: JsonPropertyName("findings")] IList<QualityReportFinding> Findings);

public sealed record QualityReport(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("reportId")] string ReportId,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("authored")] QualityReportAuthored Authored,
    [property: JsonPropertyName("observed")] QualityReportObserved Observed,
    [property: JsonPropertyName("assessment")] QualityReportAssessment Assessment,
    [property: JsonPropertyName("markdownPath")] string? MarkdownPath);

public sealed record QualitySyncInventoryData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("projects")] int Projects,
    [property: JsonPropertyName("tests")] int Tests);

public sealed record QualitySyncResultsData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped);

public sealed record QualitySyncCoverageData(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("lineRate")] double LineRate,
    [property: JsonPropertyName("branchRate")] double BranchRate);

public sealed record QualitySyncReportData(
    [property: JsonPropertyName("jsonPath")] string JsonPath,
    [property: JsonPropertyName("markdownPath")] string MarkdownPath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("findings")] int Findings);

public sealed record QualitySyncData(
    [property: JsonPropertyName("inventory")] QualitySyncInventoryData Inventory,
    [property: JsonPropertyName("results")] QualitySyncResultsData Results,
    [property: JsonPropertyName("coverage")] QualitySyncCoverageData Coverage,
    [property: JsonPropertyName("report")] QualitySyncReportData Report,
    [property: JsonPropertyName("warnings")] IList<string> Warnings,
    [property: JsonPropertyName("dryRun")] bool DryRun);

public sealed record QualitySyncOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] QualitySyncData Data);

public sealed record QualityShowData(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("inventory")] TestInventory? Inventory,
    [property: JsonPropertyName("results")] TestRunSummary? Results,
    [property: JsonPropertyName("coverage")] CoverageSummary? Coverage,
    [property: JsonPropertyName("report")] QualityReport? Report,
    [property: JsonPropertyName("markdownPath")] string? MarkdownPath);

public sealed record QualityShowOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] QualityShowData Data);
