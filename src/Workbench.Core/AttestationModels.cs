using System.Text.Json.Serialization;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record AttestationRepositoryMetadata(
    [property: JsonPropertyName("root")] string Root,
    [property: JsonPropertyName("commit")] string? Commit,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("configPath")] string? ConfigPath,
    [property: JsonPropertyName("workbenchConfigPath")] string? WorkbenchConfigPath);

public sealed record AttestationSelection(
    [property: JsonPropertyName("scope")] IList<string> Scope,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("emit")] string Emit,
    [property: JsonPropertyName("outDir")] string OutDir,
    [property: JsonPropertyName("exec")] bool Exec,
    [property: JsonPropertyName("noExec")] bool NoExec);

public sealed record AttestationValidationProfileSummary(
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("errors")] int Errors,
    [property: JsonPropertyName("warnings")] int Warnings,
    [property: JsonPropertyName("findings")] IList<ValidationFinding> Findings);

public sealed record AttestationValidationSummary(
    [property: JsonPropertyName("selectedProfile")] string SelectedProfile,
    [property: JsonPropertyName("profiles")] IList<AttestationValidationProfileSummary> Profiles);

public sealed record AttestationTraceCoverageSummary(
    [property: JsonPropertyName("requirements")] int Requirements,
    [property: JsonPropertyName("withSatisfiedBy")] int WithSatisfiedBy,
    [property: JsonPropertyName("satisfiedByPercent")] double SatisfiedByPercent,
    [property: JsonPropertyName("withImplementedBy")] int WithImplementedBy,
    [property: JsonPropertyName("implementedByPercent")] double ImplementedByPercent,
    [property: JsonPropertyName("withVerifiedBy")] int WithVerifiedBy,
    [property: JsonPropertyName("verifiedByPercent")] double VerifiedByPercent,
    [property: JsonPropertyName("withTestRefs")] int WithTestRefs,
    [property: JsonPropertyName("testRefsPercent")] double TestRefsPercent,
    [property: JsonPropertyName("withCodeRefs")] int WithCodeRefs,
    [property: JsonPropertyName("codeRefsPercent")] double CodeRefsPercent,
    [property: JsonPropertyName("withDownstreamTrace")] int WithDownstreamTrace,
    [property: JsonPropertyName("downstreamTracePercent")] double DownstreamTracePercent);

public sealed record AttestationWorkItemStatusSummary(
    [property: JsonPropertyName("totalArtifacts")] int TotalArtifacts,
    [property: JsonPropertyName("linkedRequirementCount")] int LinkedRequirementCount,
    [property: JsonPropertyName("done")] int Done,
    [property: JsonPropertyName("inProgress")] int InProgress,
    [property: JsonPropertyName("open")] int Open,
    [property: JsonPropertyName("blocked")] int Blocked,
    [property: JsonPropertyName("unknown")] int Unknown);

public sealed record AttestationVerificationStatusSummary(
    [property: JsonPropertyName("totalArtifacts")] int TotalArtifacts,
    [property: JsonPropertyName("linkedRequirementCount")] int LinkedRequirementCount,
    [property: JsonPropertyName("passing")] int Passing,
    [property: JsonPropertyName("failing")] int Failing,
    [property: JsonPropertyName("pending")] int Pending,
    [property: JsonPropertyName("stale")] int Stale,
    [property: JsonPropertyName("unknown")] int Unknown);

public sealed record AttestationAggregateSummary(
    [property: JsonPropertyName("requirements")] int Requirements,
    [property: JsonPropertyName("specifications")] int Specifications,
    [property: JsonPropertyName("architectures")] int Architectures,
    [property: JsonPropertyName("workItems")] int WorkItems,
    [property: JsonPropertyName("verifications")] int Verifications,
    [property: JsonPropertyName("traceCoverage")] AttestationTraceCoverageSummary TraceCoverage,
    [property: JsonPropertyName("workItemStatuses")] AttestationWorkItemStatusSummary WorkItemStatuses,
    [property: JsonPropertyName("verificationStatuses")] AttestationVerificationStatusSummary VerificationStatuses);

public sealed record AttestationQualityReportEvidenceSummary(
    [property: JsonPropertyName("present")] bool Present,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("confidenceVerdict")] string? ConfidenceVerdict,
    [property: JsonPropertyName("generatedAt")] string? GeneratedAt,
    [property: JsonPropertyName("passed")] int? Passed,
    [property: JsonPropertyName("failed")] int? Failed,
    [property: JsonPropertyName("skipped")] int? Skipped,
    [property: JsonPropertyName("lineRate")] double? LineRate,
    [property: JsonPropertyName("branchRate")] double? BranchRate,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationTestEvidenceSummary(
    [property: JsonPropertyName("present")] bool Present,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("observedAt")] string? ObservedAt,
    [property: JsonPropertyName("passed")] int? Passed,
    [property: JsonPropertyName("failed")] int? Failed,
    [property: JsonPropertyName("skipped")] int? Skipped,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationCoverageEvidenceSummary(
    [property: JsonPropertyName("present")] bool Present,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("observedAt")] string? ObservedAt,
    [property: JsonPropertyName("lineRate")] double? LineRate,
    [property: JsonPropertyName("branchRate")] double? BranchRate,
    [property: JsonPropertyName("meetsLineThreshold")] bool? MeetsLineThreshold,
    [property: JsonPropertyName("meetsBranchThreshold")] bool? MeetsBranchThreshold,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationSimpleEvidenceSummary(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("present")] bool Present,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("paths")] IList<string> Paths,
    [property: JsonPropertyName("observedAt")] string? ObservedAt,
    [property: JsonPropertyName("notes")] IList<string> Notes,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationExecutionCommandResult(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("arguments")] IList<string> Arguments,
    [property: JsonPropertyName("workingDirectory")] string? WorkingDirectory,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("status")] string Status);

public sealed record AttestationExecutionSummary(
    [property: JsonPropertyName("requested")] bool Requested,
    [property: JsonPropertyName("performed")] bool Performed,
    [property: JsonPropertyName("commands")] IList<AttestationExecutionCommandResult> Commands,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationEvidenceSnapshot(
    [property: JsonPropertyName("qualityReport")] AttestationQualityReportEvidenceSummary QualityReport,
    [property: JsonPropertyName("testResults")] AttestationTestEvidenceSummary TestResults,
    [property: JsonPropertyName("coverage")] AttestationCoverageEvidenceSummary Coverage,
    [property: JsonPropertyName("benchmarks")] AttestationSimpleEvidenceSummary Benchmarks,
    [property: JsonPropertyName("manualQa")] AttestationSimpleEvidenceSummary ManualQa,
    [property: JsonPropertyName("execution")] AttestationExecutionSummary Execution);

public sealed record AttestationArtifactSummary(
    [property: JsonPropertyName("artifactId")] string ArtifactId,
    [property: JsonPropertyName("artifactType")] string ArtifactType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("repoRelativePath")] string RepoRelativePath,
    [property: JsonPropertyName("requirementIds")] IList<string> RequirementIds,
    [property: JsonPropertyName("validationErrors")] IList<string> ValidationErrors,
    [property: JsonPropertyName("validationWarnings")] IList<string> ValidationWarnings);

public sealed record AttestationArtifactCollections(
    [property: JsonPropertyName("architectures")] IList<AttestationArtifactSummary> Architectures,
    [property: JsonPropertyName("workItems")] IList<AttestationArtifactSummary> WorkItems,
    [property: JsonPropertyName("verifications")] IList<AttestationArtifactSummary> Verifications);

public sealed record AttestationRequirementTraceSummary(
    [property: JsonPropertyName("satisfiedBy")] IList<string> SatisfiedBy,
    [property: JsonPropertyName("implementedBy")] IList<string> ImplementedBy,
    [property: JsonPropertyName("verifiedBy")] IList<string> VerifiedBy);

public sealed record AttestationRequirementLineageSummary(
    [property: JsonPropertyName("derivedFrom")] IList<string> DerivedFrom,
    [property: JsonPropertyName("supersedes")] IList<string> Supersedes,
    [property: JsonPropertyName("sourceRefs")] IList<string> SourceRefs,
    [property: JsonPropertyName("relatedArtifacts")] IList<string> RelatedArtifacts);

public sealed record AttestationRequirementDirectRefs(
    [property: JsonPropertyName("testRefs")] IList<string> TestRefs,
    [property: JsonPropertyName("codeRefs")] IList<string> CodeRefs);

public sealed record AttestationRequirementRollupSummary(
    [property: JsonPropertyName("implemented")] bool? Implemented,
    [property: JsonPropertyName("verified")] bool? Verified,
    [property: JsonPropertyName("releaseReady")] bool? ReleaseReady,
    [property: JsonPropertyName("rule")] string? Rule);

public sealed record AttestationRequirementRecord(
    [property: JsonPropertyName("requirementId")] string RequirementId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("clause")] string Clause,
    [property: JsonPropertyName("specificationId")] string SpecificationId,
    [property: JsonPropertyName("specificationTitle")] string SpecificationTitle,
    [property: JsonPropertyName("specificationPath")] string SpecificationPath,
    [property: JsonPropertyName("specificationRepoRelativePath")] string SpecificationRepoRelativePath,
    [property: JsonPropertyName("specificationStatus")] string SpecificationStatus,
    [property: JsonPropertyName("hasSatisfiedBy")] bool HasSatisfiedBy,
    [property: JsonPropertyName("hasImplementedBy")] bool HasImplementedBy,
    [property: JsonPropertyName("hasVerifiedBy")] bool HasVerifiedBy,
    [property: JsonPropertyName("hasTestRefs")] bool HasTestRefs,
    [property: JsonPropertyName("hasCodeRefs")] bool HasCodeRefs,
    [property: JsonPropertyName("validationErrors")] IList<string> ValidationErrors,
    [property: JsonPropertyName("validationWarnings")] IList<string> ValidationWarnings,
    [property: JsonPropertyName("trace")] AttestationRequirementTraceSummary Trace,
    [property: JsonPropertyName("lineage")] AttestationRequirementLineageSummary Lineage,
    [property: JsonPropertyName("directRefs")] AttestationRequirementDirectRefs DirectRefs,
    [property: JsonPropertyName("linkedArchitectures")] IList<AttestationArtifactSummary> LinkedArchitectures,
    [property: JsonPropertyName("linkedWorkItems")] IList<AttestationArtifactSummary> LinkedWorkItems,
    [property: JsonPropertyName("linkedVerifications")] IList<AttestationArtifactSummary> LinkedVerifications,
    [property: JsonPropertyName("linkedWorkItemStatuses")] IList<string> LinkedWorkItemStatuses,
    [property: JsonPropertyName("linkedVerificationStatuses")] IList<string> LinkedVerificationStatuses,
    [property: JsonPropertyName("testEvidenceStatus")] string TestEvidenceStatus,
    [property: JsonPropertyName("coverageEvidenceStatus")] string CoverageEvidenceStatus,
    [property: JsonPropertyName("benchmarkEvidenceStatus")] string BenchmarkEvidenceStatus,
    [property: JsonPropertyName("manualQaStatus")] string ManualQaStatus,
    [property: JsonPropertyName("gaps")] IList<string> Gaps,
    [property: JsonPropertyName("derivedRollups")] AttestationRequirementRollupSummary? DerivedRollups);

public sealed record AttestationGapSummary(
    [property: JsonPropertyName("requirementsWithoutDownstreamTrace")] IList<string> RequirementsWithoutDownstreamTrace,
    [property: JsonPropertyName("requirementsWithoutImplementationEvidence")] IList<string> RequirementsWithoutImplementationEvidence,
    [property: JsonPropertyName("requirementsWithoutVerificationCoverage")] IList<string> RequirementsWithoutVerificationCoverage,
    [property: JsonPropertyName("requirementsWithFailingOrStaleEvidence")] IList<string> RequirementsWithFailingOrStaleEvidence,
    [property: JsonPropertyName("orphanArtifacts")] IList<string> OrphanArtifacts,
    [property: JsonPropertyName("unresolvedReferences")] IList<string> UnresolvedReferences);

public sealed record AttestationDerivedRollupSummary(
    [property: JsonPropertyName("implementedEnabled")] bool ImplementedEnabled,
    [property: JsonPropertyName("verifiedEnabled")] bool VerifiedEnabled,
    [property: JsonPropertyName("releaseReadyEnabled")] bool ReleaseReadyEnabled,
    [property: JsonPropertyName("implementedRule")] string? ImplementedRule,
    [property: JsonPropertyName("verifiedRule")] string? VerifiedRule,
    [property: JsonPropertyName("releaseReadyRule")] string? ReleaseReadyRule,
    [property: JsonPropertyName("implementedRequirements")] int ImplementedRequirements,
    [property: JsonPropertyName("verifiedRequirements")] int VerifiedRequirements,
    [property: JsonPropertyName("releaseReadyRequirements")] int ReleaseReadyRequirements);

public sealed record AttestationSnapshot(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("repository")] AttestationRepositoryMetadata Repository,
    [property: JsonPropertyName("selection")] AttestationSelection Selection,
    [property: JsonPropertyName("validation")] AttestationValidationSummary Validation,
    [property: JsonPropertyName("aggregates")] AttestationAggregateSummary Aggregates,
    [property: JsonPropertyName("evidence")] AttestationEvidenceSnapshot Evidence,
    [property: JsonPropertyName("artifacts")] AttestationArtifactCollections Artifacts,
    [property: JsonPropertyName("requirements")] IList<AttestationRequirementRecord> Requirements,
    [property: JsonPropertyName("gaps")] AttestationGapSummary Gaps,
    [property: JsonPropertyName("derivedRollups")] AttestationDerivedRollupSummary? DerivedRollups,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationRunData(
    [property: JsonPropertyName("snapshot")] AttestationSnapshot Snapshot,
    [property: JsonPropertyName("summaryHtmlPath")] string? SummaryHtmlPath,
    [property: JsonPropertyName("detailsHtmlPath")] string? DetailsHtmlPath,
    [property: JsonPropertyName("jsonPath")] string? JsonPath,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);

public sealed record AttestationOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] AttestationRunData Data);

#pragma warning restore MA0048
