using System.Globalization;
using System.Text;

namespace Workbench.Core;

public static partial class AttestationService
{
    private static AttestationSnapshot BuildSnapshot(
        string repoRoot,
        AttestationRunOptions options,
        AttestationConfig attestationConfig,
        string selectedProfile,
        IList<string> effectiveScope,
        IReadOnlyList<AttestationValidationProfileSummary> validationSnapshots,
        ValidationResult selectedValidation,
        ValidationGraph graph,
        AttestationEvidenceSnapshot evidence,
        IList<string> warnings)
    {
        var repoMetadata = BuildRepositoryMetadata(repoRoot, attestationConfig.ConfigPath);
        var selection = new AttestationSelection(
            effectiveScope.ToList(),
            selectedProfile,
            NormalizeEmit(options.Emit),
            NormalizeRepoPath(repoRoot, ResolveOutputDirectory(repoRoot, options.OutDir)),
            options.Exec,
            options.NoExec);

        var requirements = graph.Requirements
            .Where(requirement => IsRequirementInScope(requirement, effectiveScope))
            .OrderBy(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var scopedValidationFindings = FilterValidationFindings(selectedValidation.Findings, repoRoot, effectiveScope, attestationConfig.ScopeExcludes).ToList();
        var validationIndex = BuildValidationFindingIndex(repoRoot, requirements, scopedValidationFindings);
        var artifactLookup = BuildArtifactLookup(graph);
        var verificationLookup = BuildVerificationLookup(graph);
        var artifacts = BuildArtifactCollections(graph, requirements, artifactLookup);
        var requirementsRecords = BuildRequirementRecords(
            graph,
            requirements,
            validationIndex,
            evidence,
            attestationConfig,
            artifactLookup,
            verificationLookup);

        var aggregates = BuildAggregateSummary(attestationConfig, requirementsRecords, graph, artifacts, artifactLookup);
        var gaps = BuildGapSummary(requirementsRecords, scopedValidationFindings);
        var derivedRollups = BuildDerivedRollups(attestationConfig, requirementsRecords);

        return new AttestationSnapshot(
            2,
            "attestation",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            repoMetadata,
            selection,
            new AttestationValidationSummary(selectedProfile, validationSnapshots.ToList(), validationIndex.Findings.ToList()),
            aggregates,
            evidence,
            artifacts,
            requirementsRecords,
            gaps,
            derivedRollups,
            warnings.ToList());
    }

    private static AttestationRepositoryMetadata BuildRepositoryMetadata(string repoRoot, string? attestationConfigPath)
    {
        var configPath = ResolveConfigPath(repoRoot, attestationConfigPath);
        return new AttestationRepositoryMetadata(
            repoRoot,
            GetRepositoryDisplayName(repoRoot),
            TryReadGitValue(repoRoot, "rev-parse", "HEAD"),
            TryReadGitValue(repoRoot, "rev-parse", "--abbrev-ref", "HEAD"),
            NormalizeRepoPath(repoRoot, configPath),
            NormalizeRepoPath(repoRoot, WorkbenchConfig.GetConfigPath(repoRoot)));
    }

    private static string GetRepositoryDisplayName(string repoRoot)
    {
        var trimmed = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var displayName = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(displayName) ? trimmed : displayName;
    }

    private static IReadOnlyList<AttestationValidationProfileSummary> BuildValidationSnapshots(
        string repoRoot,
        WorkbenchConfig config,
        AttestationConfig attestationConfig,
        IList<string> scope)
    {
        var summaries = new List<AttestationValidationProfileSummary>();
        foreach (var profile in new[] { ValidationProfiles.Core, ValidationProfiles.Traceable, ValidationProfiles.Auditable })
        {
            var result = ValidationService.ValidateRepo(
                repoRoot,
                config,
                new ValidationOptions(Array.Empty<string>(), Array.Empty<string>(), false, profile, scope));

            var findings = FilterValidationFindings(result.Findings, repoRoot, scope, attestationConfig.ScopeExcludes).ToList();
            summaries.Add(new AttestationValidationProfileSummary(
                profile,
                CountSeverity(findings, "error"),
                CountSeverity(findings, "warning")));
        }

        return summaries;
    }

    private static AttestationArtifactCollections BuildArtifactCollections(
        ValidationGraph graph,
        IReadOnlyList<RequirementNode> requirements,
        IReadOnlyDictionary<string, AttestationArtifactSummary> lookup)
    {
        var architectureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verificationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in requirements)
        {
            foreach (var id in GetTraceValues(requirement.Trace, "Satisfied By"))
            {
                AddLinkedArtifactIds(graph, id, "architecture", architectureIds, lookup, requirement.RequirementId);
            }

            foreach (var id in GetTraceValues(requirement.Trace, "Implemented By"))
            {
                AddLinkedArtifactIds(graph, id, "work_item", workItemIds, lookup, requirement.RequirementId);
            }

            foreach (var id in GetTraceValues(requirement.Trace, "Verified By"))
            {
                AddLinkedArtifactIds(graph, id, "verification", verificationIds, lookup, requirement.RequirementId);
            }
        }

        return new AttestationArtifactCollections(
            architectureIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList(),
            workItemIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList(),
            verificationIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IList<AttestationRequirementRecord> BuildRequirementRecords(
        ValidationGraph graph,
        IReadOnlyList<RequirementNode> requirements,
        ValidationFindingIndex validationIndex,
        AttestationEvidenceSnapshot evidence,
        AttestationConfig attestationConfig,
        IReadOnlyDictionary<string, AttestationArtifactSummary> artifactLookup,
        IReadOnlyDictionary<string, VerificationNode> verificationLookup)
    {
        var records = new List<AttestationRequirementRecord>();
        foreach (var requirement in requirements)
        {
            var specification = graph.Specifications.FirstOrDefault(spec => string.Equals(spec.Artifact.ArtifactId, requirement.SpecArtifactId, StringComparison.OrdinalIgnoreCase));
            var satisfiedBy = GetTraceValues(requirement.Trace, "Satisfied By");
            var implementedBy = GetTraceValues(requirement.Trace, "Implemented By");
            var verifiedBy = GetTraceValues(requirement.Trace, "Verified By");
            var derivedFrom = GetTraceValues(requirement.Trace, "Derived From");
            var supersedes = GetTraceValues(requirement.Trace, "Supersedes");
            var sourceRefs = GetTraceValues(requirement.Trace, "Source Refs");
            var testRefs = GetTraceValues(requirement.Trace, "Test Refs");
            var codeRefs = GetTraceValues(requirement.Trace, "Code Refs");
            var derivedEvidence = CollectVerificationEvidence(verifiedBy, verificationLookup);
            var effectiveTestRefs = CombineRefs(testRefs, derivedEvidence.TestRefs);
            var effectiveCodeRefs = CombineRefs(codeRefs, derivedEvidence.CodeRefs);
            validationIndex.RequirementFindingIds.TryGetValue(requirement.RequirementId, out var validationFindingIds);
            validationFindingIds ??= new List<string>();

            var testEvidenceStatus = DetermineRequirementTestEvidenceStatus(evidence.TestResults, effectiveTestRefs);
            var coverageEvidenceStatus = DetermineRequirementCoverageEvidenceStatus(evidence.Coverage, effectiveCodeRefs);
            var benchmarkEvidenceStatus = ResolveRequirementBenchmarkEvidenceStatus(evidence.Benchmarks, derivedEvidence.BenchmarkNotApplicable);
            var manualQaStatus = evidence.ManualQa.Status ?? "unknown";
            var gaps = BuildRequirementGaps(
                satisfiedBy,
                implementedBy,
                verifiedBy,
                effectiveTestRefs,
                effectiveCodeRefs,
                testEvidenceStatus,
                coverageEvidenceStatus,
                benchmarkEvidenceStatus,
                manualQaStatus,
                validationFindingIds,
                validationIndex.FindingLookup);

            records.Add(new AttestationRequirementRecord(
                requirement.RequirementId,
                requirement.Title,
                requirement.Clause,
                specification?.Artifact.ArtifactId ?? requirement.SpecArtifactId,
                specification?.Artifact.Title ?? string.Empty,
                specification?.Artifact.RepoRelativePath ?? requirement.SpecRepoRelativePath,
                specification?.Artifact.Status ?? string.Empty,
                new AttestationRequirementTraceSummary(satisfiedBy, implementedBy, verifiedBy),
                new AttestationRequirementLineageSummary(derivedFrom, supersedes, sourceRefs, requirement.RelatedArtifacts.ToList()),
                new AttestationRequirementDirectRefs(effectiveTestRefs, effectiveCodeRefs),
                validationFindingIds.Count == 0 ? null : validationFindingIds.ToList(),
                testEvidenceStatus,
                coverageEvidenceStatus,
                benchmarkEvidenceStatus,
                manualQaStatus,
                gaps,
                BuildRequirementRollups(attestationConfig, implementedBy, verifiedBy, validationFindingIds, artifactLookup, evidence)));
        }

        return records;
    }

    private static AttestationAggregateSummary BuildAggregateSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements,
        ValidationGraph graph,
        AttestationArtifactCollections artifacts,
        IReadOnlyDictionary<string, AttestationArtifactSummary> artifactLookup)
    {
        var total = requirements.Count;
        var satisfiedBy = requirements.Count(requirement => requirement.Trace.SatisfiedBy.Count > 0);
        var implementedBy = requirements.Count(requirement => requirement.Trace.ImplementedBy.Count > 0);
        var verifiedBy = requirements.Count(requirement => requirement.Trace.VerifiedBy.Count > 0);
        var testRefs = requirements.Count(requirement => requirement.DirectRefs.TestRefs.Count > 0);
        var codeRefs = requirements.Count(requirement => requirement.DirectRefs.CodeRefs.Count > 0);
        var downstream = requirements.Count(requirement => requirement.Trace.SatisfiedBy.Count > 0 || requirement.Trace.ImplementedBy.Count > 0 || requirement.Trace.VerifiedBy.Count > 0);

        return new AttestationAggregateSummary(
            total,
            graph.Specifications.Count(specification => requirements.Any(requirement => string.Equals(requirement.SpecificationId, specification.Artifact.ArtifactId, StringComparison.OrdinalIgnoreCase))),
            artifacts.Architectures.Count,
            artifacts.WorkItems.Count,
            artifacts.Verifications.Count,
            new AttestationTraceCoverageSummary(
                total,
                satisfiedBy,
                Percent(satisfiedBy, total),
                implementedBy,
                Percent(implementedBy, total),
                verifiedBy,
                Percent(verifiedBy, total),
                testRefs,
                Percent(testRefs, total),
                codeRefs,
                Percent(codeRefs, total),
                downstream,
                Percent(downstream, total)),
            BuildWorkItemStatusSummary(attestationConfig, requirements, artifactLookup),
            BuildVerificationStatusSummary(attestationConfig, requirements, artifactLookup));
    }

    private static AttestationWorkItemStatusSummary BuildWorkItemStatusSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements,
        IReadOnlyDictionary<string, AttestationArtifactSummary> artifactLookup)
    {
        var artifacts = requirements
            .SelectMany(requirement => requirement.Trace.ImplementedBy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => artifactLookup.TryGetValue(id, out var summary) ? summary : null)
            .Where(summary => summary is not null)
            .Select(summary => summary!)
            .ToList();
        return new AttestationWorkItemStatusSummary(
            artifacts.Count,
            requirements.Count(requirement => requirement.Trace.ImplementedBy.Count > 0),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemDone),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemInProgress),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemOpen),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemBlocked),
            CountStatuses(artifacts, new[] { "unknown" }));
    }

    private static AttestationVerificationStatusSummary BuildVerificationStatusSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements,
        IReadOnlyDictionary<string, AttestationArtifactSummary> artifactLookup)
    {
        var artifacts = requirements
            .SelectMany(requirement => requirement.Trace.VerifiedBy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => artifactLookup.TryGetValue(id, out var summary) ? summary : null)
            .Where(summary => summary is not null)
            .Select(summary => summary!)
            .ToList();
        return new AttestationVerificationStatusSummary(
            artifacts.Count,
            requirements.Count(requirement => requirement.Trace.VerifiedBy.Count > 0),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationPassing),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationFailing),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationPending),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationStale),
            CountStatuses(artifacts, new[] { "unknown" }));
    }

    private static AttestationGapSummary BuildGapSummary(
        IList<AttestationRequirementRecord> requirements,
        IReadOnlyList<ValidationFinding> selectedValidationFindings)
    {
        var withoutDownstream = requirements.Where(requirement => requirement.Trace.SatisfiedBy.Count == 0 && requirement.Trace.ImplementedBy.Count == 0 && requirement.Trace.VerifiedBy.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var withoutImplementation = requirements.Where(requirement => requirement.Trace.ImplementedBy.Count == 0 && requirement.DirectRefs.TestRefs.Count == 0 && requirement.DirectRefs.CodeRefs.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var withoutVerification = requirements.Where(requirement => requirement.Trace.VerifiedBy.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var failingOrStale = requirements.Where(requirement => IsProblemEvidenceStatus(requirement.TestEvidenceStatus) || IsProblemEvidenceStatus(requirement.CoverageEvidenceStatus) || IsProblemEvidenceStatus(requirement.BenchmarkEvidenceStatus) || IsProblemEvidenceStatus(requirement.ManualQaStatus)).Select(requirement => requirement.RequirementId).ToList();

        var orphanArtifacts = selectedValidationFindings
            .Where(finding => string.Equals(finding.Category, ValidationCategories.OrphanArtifact, StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.ArtifactId ?? finding.File ?? finding.Message)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unresolved = selectedValidationFindings
            .Where(finding => string.Equals(finding.Category, ValidationCategories.UnresolvedReference, StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.TargetId ?? finding.ArtifactId ?? finding.Message)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AttestationGapSummary(withoutDownstream, withoutImplementation, withoutVerification, failingOrStale, orphanArtifacts, unresolved);
    }

    private static AttestationDerivedRollupSummary? BuildDerivedRollups(AttestationConfig attestationConfig, IList<AttestationRequirementRecord> requirements)
    {
        if (attestationConfig.Rollups is null)
        {
            return null;
        }

        var implementedCount = requirements.Count(requirement => requirement.DerivedRollups?.Implemented == true);
        var verifiedCount = requirements.Count(requirement => requirement.DerivedRollups?.Verified == true);
        var releaseReadyCount = requirements.Count(requirement => requirement.DerivedRollups?.ReleaseReady == true);

        string? releaseReadyRule = null;
        if (attestationConfig.Rollups.ReleaseReady)
        {
            releaseReadyRule = attestationConfig.Rollups.RequireNoOpenWorkItems
                ? "implemented && verified && no open work items"
                : "implemented && verified && open work items allowed";
        }

        return new AttestationDerivedRollupSummary(
            attestationConfig.Rollups.Implemented,
            attestationConfig.Rollups.Verified,
            attestationConfig.Rollups.ReleaseReady,
            attestationConfig.Rollups.Implemented ? $"any linked work item status in [{string.Join(", ", attestationConfig.StatusPolicy.WorkItemDone)}]" : null,
            attestationConfig.Rollups.Verified ? $"any linked verification status in [{string.Join(", ", attestationConfig.StatusPolicy.VerificationPassing)}]" : null,
            releaseReadyRule,
            implementedCount,
            verifiedCount,
            releaseReadyCount);
    }

    private static AttestationRequirementRollupSummary? BuildRequirementRollups(
        AttestationConfig attestationConfig,
        IReadOnlyList<string> implementedBy,
        IReadOnlyList<string> verifiedBy,
        IReadOnlyList<string> validationFindingIds,
        IReadOnlyDictionary<string, AttestationArtifactSummary> artifactLookup,
        AttestationEvidenceSnapshot evidence)
    {
        if (attestationConfig.Rollups is null)
        {
            return null;
        }

        var implemented = attestationConfig.Rollups.Implemented && implementedBy.Any(id => artifactLookup.TryGetValue(id, out var item) && HasStatus(attestationConfig.StatusPolicy.WorkItemDone, item.Status));
        var verified = attestationConfig.Rollups.Verified && verifiedBy.Any(id => artifactLookup.TryGetValue(id, out var item) && HasStatus(attestationConfig.StatusPolicy.VerificationPassing, item.Status));
        var releaseReady = attestationConfig.Rollups.ReleaseReady &&
            (!attestationConfig.Rollups.RequireNoOpenWorkItems || !implementedBy.Any(id => artifactLookup.TryGetValue(id, out var item) && (HasStatus(attestationConfig.StatusPolicy.WorkItemOpen, item.Status) || HasStatus(attestationConfig.StatusPolicy.WorkItemBlocked, item.Status)))) &&
            (!attestationConfig.Rollups.RequireNoValidationErrors || validationFindingIds.Count == 0) &&
            (!attestationConfig.Rollups.RequireNoFailingVerifications || !verifiedBy.Any(id => artifactLookup.TryGetValue(id, out var item) && HasStatus(attestationConfig.StatusPolicy.VerificationFailing, item.Status))) &&
            (!attestationConfig.Rollups.RequireNoStaleEvidence || (!string.Equals(evidence.TestResults.Status, "stale", StringComparison.OrdinalIgnoreCase) && !string.Equals(evidence.Coverage.Status, "stale", StringComparison.OrdinalIgnoreCase) && !string.Equals(evidence.Benchmarks.Status, "stale", StringComparison.OrdinalIgnoreCase) && !string.Equals(evidence.ManualQa.Status, "stale", StringComparison.OrdinalIgnoreCase)));

        return new AttestationRequirementRollupSummary(implemented, verified, releaseReady, "configured local rule");
    }

    private static string DetermineRequirementTestEvidenceStatus(AttestationTestEvidenceSummary evidence, IReadOnlyList<string> testRefs)
    {
        if (testRefs.Count == 0)
        {
            return "unknown";
        }

        return NormalizeEvidenceStatus(evidence.Status, evidence.Present);
    }

    private static string DetermineRequirementCoverageEvidenceStatus(AttestationCoverageEvidenceSummary evidence, IReadOnlyList<string> codeRefs)
    {
        if (codeRefs.Count == 0)
        {
            return "unknown";
        }

        if (!evidence.Present)
        {
            return "unknown";
        }

        if (string.Equals(evidence.Status, "passing", StringComparison.OrdinalIgnoreCase))
        {
            if (evidence.MeetsLineThreshold == false || evidence.MeetsBranchThreshold == false)
            {
                return "failing";
            }

            return "passing";
        }

        return NormalizeEvidenceStatus(evidence.Status, evidence.Present);
    }

    private static IList<string> BuildRequirementGaps(
        IReadOnlyList<string> satisfiedBy,
        IReadOnlyList<string> implementedBy,
        IReadOnlyList<string> verifiedBy,
        IReadOnlyList<string> testRefs,
        IReadOnlyList<string> codeRefs,
        string testEvidenceStatus,
        string coverageEvidenceStatus,
        string benchmarkEvidenceStatus,
        string manualQaStatus,
        IReadOnlyList<string> validationFindingIds,
        IReadOnlyDictionary<string, AttestationValidationFindingSummary> findingLookup)
    {
        var gaps = new List<string>();

        if (satisfiedBy.Count == 0 && implementedBy.Count == 0 && verifiedBy.Count == 0)
        {
            gaps.Add("no downstream trace links");
        }

        if (implementedBy.Count == 0 && testRefs.Count == 0 && codeRefs.Count == 0)
        {
            gaps.Add("no implementation evidence or direct refs");
        }

        if (verifiedBy.Count == 0)
        {
            gaps.Add("no verification coverage");
        }

        if (testRefs.Count > 0)
        {
            if (string.Equals(testEvidenceStatus, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                gaps.Add("test evidence unknown");
            }
            else if (IsProblemEvidenceStatus(testEvidenceStatus))
            {
                gaps.Add($"test evidence {testEvidenceStatus}");
            }
        }

        if (codeRefs.Count > 0)
        {
            if (string.Equals(coverageEvidenceStatus, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                gaps.Add("coverage evidence unknown");
            }
            else if (IsProblemEvidenceStatus(coverageEvidenceStatus))
            {
                gaps.Add($"coverage evidence {coverageEvidenceStatus}");
            }
        }

        if (string.Equals(benchmarkEvidenceStatus, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add("benchmark evidence unknown");
        }
        else if (IsProblemEvidenceStatus(benchmarkEvidenceStatus))
        {
            gaps.Add($"benchmark evidence {benchmarkEvidenceStatus}");
        }

        if (string.Equals(manualQaStatus, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add("manual QA evidence unknown");
        }
        else if (IsProblemEvidenceStatus(manualQaStatus))
        {
            gaps.Add($"manual QA evidence {manualQaStatus}");
        }

        var validationErrors = validationFindingIds.Where(id => findingLookup.TryGetValue(id, out var finding) && string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)).ToList();
        var validationWarnings = validationFindingIds.Where(id => findingLookup.TryGetValue(id, out var finding) && string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)).ToList();

        if (validationErrors.Count > 0)
        {
            gaps.Add($"validation errors ({validationErrors.Count})");
        }
        else if (validationWarnings.Count > 0)
        {
            gaps.Add($"validation warnings ({validationWarnings.Count})");
        }

        return gaps
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, AttestationArtifactSummary> BuildArtifactLookup(
        ValidationGraph graph)
    {
        var lookup = new Dictionary<string, AttestationArtifactSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in graph.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.ArtifactId))
            {
                continue;
            }

            lookup[artifact.ArtifactId] = BuildArtifactSummary(artifact);
        }

        return lookup;
    }

    private static AttestationArtifactSummary BuildArtifactSummary(CanonicalArtifactNode artifact)
    {
        return new AttestationArtifactSummary(
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Title,
            artifact.Status,
            artifact.RepoRelativePath,
            new List<string>());
    }

    private static Dictionary<string, VerificationNode> BuildVerificationLookup(ValidationGraph graph)
    {
        var lookup = new Dictionary<string, VerificationNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var verification in graph.Verifications)
        {
            if (string.IsNullOrWhiteSpace(verification.Artifact.ArtifactId))
            {
                continue;
            }

            lookup[verification.Artifact.ArtifactId] = verification;
        }

        return lookup;
    }

    private static VerificationDerivedEvidence CollectVerificationEvidence(
        IReadOnlyList<string> verifiedBy,
        IReadOnlyDictionary<string, VerificationNode> verificationLookup)
    {
        if (verifiedBy.Count == 0 || verificationLookup.Count == 0)
        {
            return VerificationDerivedEvidence.Empty;
        }

        var testRefs = new List<string>();
        var codeRefs = new List<string>();
        var benchmarkNotApplicable = false;

        foreach (var verificationId in verifiedBy.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!verificationLookup.TryGetValue(verificationId, out var verification))
            {
                continue;
            }

            benchmarkNotApplicable |= verification.BenchmarkNotApplicable;

            foreach (var evidenceRef in verification.EvidenceRefs)
            {
                if (TryClassifyVerificationEvidenceReference(evidenceRef, out var refKind))
                {
                    if (string.Equals(refKind, "test", StringComparison.Ordinal))
                    {
                        AddValue(testRefs, evidenceRef);
                    }
                    else if (string.Equals(refKind, "code", StringComparison.Ordinal))
                    {
                        AddValue(codeRefs, evidenceRef);
                    }
                }
            }
        }

        return new VerificationDerivedEvidence(
            testRefs,
            codeRefs,
            benchmarkNotApplicable);
    }

    private static bool TryClassifyVerificationEvidenceReference(string reference, out string kind)
    {
        var normalized = NormalizeEvidenceReference(reference);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            kind = string.Empty;
            return false;
        }

        if (IsTestEvidenceReference(normalized))
        {
            kind = "test";
            return true;
        }

        if (IsCodeEvidenceReference(normalized))
        {
            kind = "code";
            return true;
        }

        kind = string.Empty;
        return false;
    }

    private static string NormalizeEvidenceReference(string reference)
    {
        var candidate = reference.Trim();
        var fragmentIndex = candidate.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            candidate = candidate[..fragmentIndex];
        }

        var queryIndex = candidate.IndexOf('?');
        if (queryIndex >= 0)
        {
            candidate = candidate[..queryIndex];
        }

        return candidate.Trim();
    }

    private static bool IsTestEvidenceReference(string reference)
    {
        var normalized = reference.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("tests.cs", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodeEvidenceReference(string reference)
    {
        var normalized = reference.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("src/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/src/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".fs", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> CombineRefs(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        if (first.Count == 0 && second.Count == 0)
        {
            return new List<string>();
        }

        var combined = new List<string>(first.Count + second.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRefs(first);
        AddRefs(second);
        return combined;

        void AddRefs(IReadOnlyList<string> refs)
        {
            foreach (var reference in refs)
            {
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                if (seen.Add(reference))
                {
                    combined.Add(reference);
                }
            }
        }
    }

    private static string ResolveRequirementBenchmarkEvidenceStatus(
        AttestationSimpleEvidenceSummary benchmarks,
        bool benchmarkNotApplicable)
    {
        if (benchmarkNotApplicable)
        {
            return "not-applicable";
        }

        return benchmarks.Status ?? "unknown";
    }

    private static ValidationFindingIndex BuildValidationFindingIndex(
        string repoRoot,
        IReadOnlyList<RequirementNode> requirements,
        IReadOnlyList<ValidationFinding> findings)
    {
        var groups = new Dictionary<string, ValidationFindingGroup>(StringComparer.Ordinal);
        foreach (var finding in findings)
        {
            var key = BuildFindingGroupKey(repoRoot, finding);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new ValidationFindingGroup(
                    finding.Profile,
                    finding.Severity,
                    finding.Category,
                    finding.Message,
                    NormalizeRepoPathOrNull(repoRoot, finding.File),
                    finding.ArtifactId,
                    finding.Field,
                    finding.TargetId,
                    finding.TargetType,
                    NormalizeRepoPathOrNull(repoRoot, finding.TargetFile),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                groups[key] = group;
            }

            foreach (var requirement in requirements)
            {
                if (IsFindingForRequirement(finding, requirement.RequirementId, requirement.SpecPath))
                {
                    group.RequirementIds.Add(requirement.RequirementId);
                }
            }
        }

        var orderedGroups = groups.Values
            .OrderBy(group => group.Severity.Equals("error", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(group => group.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.RepoRelativePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.TargetId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var findingsById = new Dictionary<string, AttestationValidationFindingSummary>(StringComparer.OrdinalIgnoreCase);
        var requirementFindingIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<AttestationValidationFindingSummary>();

        for (var index = 0; index < orderedGroups.Count; index++)
        {
            var group = orderedGroups[index];
            var findingId = $"F{index + 1:000}";
            var requirementIds = group.RequirementIds
                .OrderBy(requirementId => requirementId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var groupedByRequirement = requirementIds.Count > 1;
            var summary = new AttestationValidationFindingSummary(
                findingId,
                group.Profile,
                group.Severity,
                group.Category,
                group.Message,
                group.RepoRelativePath,
                requirementIds.Count == 0 ? group.ArtifactId : null,
                group.Field,
                groupedByRequirement ? null : group.TargetId,
                group.TargetType,
                groupedByRequirement ? null : group.TargetFile,
                requirementIds.Count == 0 ? null : requirementIds);
            summaries.Add(summary);
            findingsById[findingId] = summary;

            foreach (var requirementId in requirementIds)
            {
                if (!requirementFindingIds.TryGetValue(requirementId, out var list))
                {
                    list = new List<string>();
                    requirementFindingIds[requirementId] = list;
                }

                list.Add(findingId);
            }
        }

        return new ValidationFindingIndex(
            summaries,
            requirementFindingIds.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase),
            findingsById);
    }

    private static IEnumerable<ValidationFinding> FilterValidationFindings(
        IEnumerable<ValidationFinding> findings,
        string repoRoot,
        IList<string> scope,
        IList<string> excludes)
    {
        foreach (var finding in findings)
        {
            if (string.Equals(finding.Profile, ValidationProfiles.RepoState, StringComparison.OrdinalIgnoreCase))
            {
                yield return finding;
                continue;
            }

            if (finding.File is not null)
            {
                var repoRelative = NormalizeRepoPath(repoRoot, finding.File);
                if (excludes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (scope.Count > 0 && !scope.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            yield return finding;
        }
    }

    private static bool IsRequirementInScope(RequirementNode requirement, IList<string> scope)
    {
        return scope.Count == 0 || scope.Any(prefix => requirement.SpecRepoRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetTraceValues(IReadOnlyDictionary<string, IReadOnlyList<string>> trace, string key)
    {
        return trace.TryGetValue(key, out var values)
            ? values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
    }

    private static bool IsFindingForRequirement(ValidationFinding finding, string requirementId, string specPath)
    {
        if (string.Equals(finding.ArtifactId, requirementId, StringComparison.OrdinalIgnoreCase) || string.Equals(finding.TargetId, requirementId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (finding.File is not null && string.Equals(NormalizePathForComparison(finding.File), NormalizePathForComparison(specPath), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string BuildFindingGroupKey(string repoRoot, ValidationFinding finding)
    {
        return string.Join(
            '\u001f',
            NormalizeKeyToken(finding.Profile),
            NormalizeKeyToken(finding.Severity),
            NormalizeKeyToken(finding.Category),
            NormalizeRepoPathOrNull(repoRoot, finding.File) ?? string.Empty,
            NormalizeKeyToken(finding.Field),
            NormalizeKeyToken(finding.TargetType),
            NormalizeRepoPathOrNull(repoRoot, finding.TargetFile) ?? string.Empty,
            NormalizeFindingMessageForGrouping(finding.Message));
    }

    private static string NormalizePathForComparison(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static string? NormalizeRepoPathOrNull(string repoRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return NormalizeRepoPath(repoRoot, path);
    }

    private static string NormalizeFindingMessageForGrouping(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(message.Length);
        var index = 0;
        while (index < message.Length)
        {
            if (char.IsUpper(message[index]))
            {
                var start = index;
                var sawDash = false;
                var allAllowed = true;

                while (index < message.Length)
                {
                    var current = message[index];
                    if (char.IsUpper(current) || char.IsDigit(current) || current == '-' || current == '_')
                    {
                        sawDash |= current == '-';
                        index++;
                        continue;
                    }

                    allAllowed = false;
                    break;
                }

                if (allAllowed && sawDash)
                {
                    builder.Append("<id>");
                }
                else
                {
                    builder.Append(message, start, index - start);
                }

                continue;
            }

            builder.Append(message[index]);
            index++;
        }

        return builder.ToString();
    }

    private static string NormalizeKeyToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static int CountSeverity(IEnumerable<ValidationFinding> findings, string severity)
    {
        return findings.Count(finding => string.Equals(finding.Severity, severity, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> NormalizePrefixes(IList<string>? prefixes)
    {
        if (prefixes is null || prefixes.Count == 0)
        {
            return new List<string>();
        }

        return prefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim().TrimStart('/').Replace('\\', '/').TrimEnd('/'))
            .Where(prefix => prefix.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildEffectiveScope(
        IList<string>? scope,
        IList<string> includes,
        IList<string> excludes)
    {
        var effective = NormalizePrefixes(scope);
        if (effective.Count == 0)
        {
            effective = NormalizePrefixes(includes);
        }

        if (effective.Count == 0)
        {
            return effective;
        }

        var normalizedExcludes = NormalizePrefixes(excludes);
        if (normalizedExcludes.Count == 0)
        {
            return effective;
        }

        return effective
            .Where(prefix => !normalizedExcludes.Any(exclude => prefix.StartsWith(exclude, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static int CountStatuses(IList<AttestationArtifactSummary> artifacts, IEnumerable<string> statuses)
    {
        var materialized = statuses
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Select(status => status.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unknown = materialized.Any(status => HasStatus(new[] { "unknown" }, status));
        var count = 0;
        foreach (var artifact in artifacts)
        {
            if (unknown && string.IsNullOrWhiteSpace(artifact.Status))
            {
                count++;
                continue;
            }

            if (HasStatus(materialized, artifact.Status))
            {
                count++;
            }
        }

        return count;
    }

    private sealed record ValidationFindingGroup(
        string Profile,
        string Severity,
        string Category,
        string Message,
        string? RepoRelativePath,
        string? ArtifactId,
        string? Field,
        string? TargetId,
        string? TargetType,
        string? TargetFile,
        HashSet<string> RequirementIds);

    private sealed record ValidationFindingIndex(
        IReadOnlyList<AttestationValidationFindingSummary> Findings,
        IReadOnlyDictionary<string, IReadOnlyList<string>> RequirementFindingIds,
        IReadOnlyDictionary<string, AttestationValidationFindingSummary> FindingLookup);

    private sealed record VerificationDerivedEvidence(
        IReadOnlyList<string> TestRefs,
        IReadOnlyList<string> CodeRefs,
        bool BenchmarkNotApplicable)
    {
        public static VerificationDerivedEvidence Empty => new(new List<string>(), new List<string>(), false);
    }

    private static string NormalizeEvidenceStatus(string? status, bool present)
    {
        if (!present || string.IsNullOrWhiteSpace(status))
        {
            return "unknown";
        }

        var normalized = NormalizeStatusToken(status);
        return normalized switch
        {
            "pass" or "passed" or "success" or "succeeded" or "ok" => "passing",
            "fail" or "failed" or "failure" or "error" => "failing",
            "pending" or "queued" or "running" or "in_progress" => "pending",
            "stale" or "obsolete" or "outdated" => "stale",
            "no-data" or "no_data" or "missing" => "unknown",
            _ => normalized
        };
    }

    private static bool HasStatus(IEnumerable<string> statuses, string? candidate)
    {
        var normalizedCandidate = NormalizeStatusToken(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        return statuses.Any(status => string.Equals(NormalizeStatusToken(status), normalizedCandidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStatusToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
    }

    private static bool IsProblemEvidenceStatus(string status)
    {
        var normalized = NormalizeStatusToken(status);
        return normalized is "failing" or "stale" or "pending" or "blocked" or "planned" or "unknown";
    }

    private static double Percent(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0;
        }

        return (double)numerator / denominator;
    }

    private static void AddLinkedArtifactIds(
        ValidationGraph graph,
        string id,
        string expectedType,
        ISet<string> ids,
        IReadOnlyDictionary<string, AttestationArtifactSummary>? lookup = null,
        string? requirementId = null)
    {
        foreach (var match in graph.ResolveArtifact(id))
        {
            if (string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(match.ArtifactId);
                if (lookup is not null && requirementId is not null && lookup.TryGetValue(match.ArtifactId, out var summary) && summary.RequirementIds is not null)
                {
                    AddValue(summary.RequirementIds, requirementId);
                }
            }
        }
    }

    private static void AddValue(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }
}
