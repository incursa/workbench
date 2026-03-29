using System.Globalization;

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

        var artifacts = BuildArtifactCollections(graph, selectedValidation, effectiveScope);
        var requirements = BuildRequirementRecords(
            graph,
            selectedValidation,
            evidence,
            attestationConfig,
            effectiveScope);

        var aggregates = BuildAggregateSummary(attestationConfig, requirements, graph, artifacts);
        var gaps = BuildGapSummary(requirements, selectedValidation);
        var derivedRollups = BuildDerivedRollups(attestationConfig, requirements);

        return new AttestationSnapshot(
            1,
            "attestation",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            repoMetadata,
            selection,
            new AttestationValidationSummary(selectedProfile, validationSnapshots.ToList()),
            aggregates,
            evidence,
            artifacts,
            requirements,
            gaps,
            derivedRollups,
            warnings.ToList());
    }

    private static AttestationRepositoryMetadata BuildRepositoryMetadata(string repoRoot, string? attestationConfigPath)
    {
        var configPath = ResolveConfigPath(repoRoot, attestationConfigPath);
        return new AttestationRepositoryMetadata(
            repoRoot,
            TryReadGitValue(repoRoot, "rev-parse", "HEAD"),
            TryReadGitValue(repoRoot, "rev-parse", "--abbrev-ref", "HEAD"),
            NormalizeRepoPath(repoRoot, configPath),
            NormalizeRepoPath(repoRoot, WorkbenchConfig.GetConfigPath(repoRoot)));
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
                CountSeverity(findings, "warning"),
                findings));
        }

        return summaries;
    }

    private static AttestationArtifactCollections BuildArtifactCollections(
        ValidationGraph graph,
        ValidationResult selectedValidation,
        IList<string> scope)
    {
        var lookup = BuildArtifactLookup(graph, selectedValidation);
        var architectureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verificationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in graph.Requirements.Where(requirement => IsRequirementInScope(requirement, scope)))
        {
            foreach (var id in GetTraceValues(requirement.Trace, "Satisfied By"))
            {
                AddLinkedArtifactIds(graph, id, "architecture", architectureIds);
            }

            foreach (var id in GetTraceValues(requirement.Trace, "Implemented By"))
            {
                AddLinkedArtifactIds(graph, id, "work_item", workItemIds);
            }

            foreach (var id in GetTraceValues(requirement.Trace, "Verified By"))
            {
                AddLinkedArtifactIds(graph, id, "verification", verificationIds);
            }
        }

        return new AttestationArtifactCollections(
            architectureIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList(),
            workItemIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList(),
            verificationIds.Select(id => lookup.TryGetValue(id, out var summary) ? summary : null).Where(summary => summary is not null).Select(summary => summary!).OrderBy(summary => summary.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IList<AttestationRequirementRecord> BuildRequirementRecords(
        ValidationGraph graph,
        ValidationResult selectedValidation,
        AttestationEvidenceSnapshot evidence,
        AttestationConfig attestationConfig,
        IList<string> scope)
    {
        var artifactLookup = BuildArtifactLookup(graph, selectedValidation);
        var validationFindings = selectedValidation.Findings.ToList();
        var requirements = graph.Requirements
            .Where(requirement => IsRequirementInScope(requirement, scope))
            .OrderBy(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

            var linkedArchitectures = BuildLinkedArtifacts(graph, artifactLookup, satisfiedBy, "architecture");
            var linkedWorkItems = BuildLinkedArtifacts(graph, artifactLookup, implementedBy, "work_item");
            var linkedVerifications = BuildLinkedArtifacts(graph, artifactLookup, verifiedBy, "verification");

            var allRequirementFindings = validationFindings
                .Where(finding => IsFindingForRequirement(finding, requirement.RequirementId, requirement.SpecPath))
                .Select(FormatFinding)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var validationErrors = allRequirementFindings.Where(message => message.StartsWith("[error]", StringComparison.OrdinalIgnoreCase)).ToList();
            var validationWarnings = allRequirementFindings.Where(message => message.StartsWith("[warning]", StringComparison.OrdinalIgnoreCase)).ToList();

            var testEvidenceStatus = DetermineRequirementTestEvidenceStatus(evidence.TestResults, testRefs);
            var coverageEvidenceStatus = DetermineRequirementCoverageEvidenceStatus(evidence.Coverage, codeRefs);
            var benchmarkEvidenceStatus = evidence.Benchmarks.Status ?? "unknown";
            var manualQaStatus = evidence.ManualQa.Status ?? "unknown";
            var gaps = BuildRequirementGaps(
                satisfiedBy,
                implementedBy,
                verifiedBy,
                testRefs,
                codeRefs,
                linkedVerifications,
                testEvidenceStatus,
                coverageEvidenceStatus,
                benchmarkEvidenceStatus,
                manualQaStatus,
                validationErrors,
                validationWarnings);

            records.Add(new AttestationRequirementRecord(
                requirement.RequirementId,
                requirement.Title,
                requirement.Clause,
                specification?.Artifact.ArtifactId ?? requirement.SpecArtifactId,
                specification?.Artifact.Title ?? string.Empty,
                specification?.Artifact.Path ?? requirement.SpecPath,
                specification?.Artifact.RepoRelativePath ?? requirement.SpecRepoRelativePath,
                specification?.Artifact.Status ?? string.Empty,
                satisfiedBy.Count > 0,
                implementedBy.Count > 0,
                verifiedBy.Count > 0,
                testRefs.Count > 0,
                codeRefs.Count > 0,
                validationErrors,
                validationWarnings,
                new AttestationRequirementTraceSummary(satisfiedBy, implementedBy, verifiedBy),
                new AttestationRequirementLineageSummary(derivedFrom, supersedes, sourceRefs, requirement.RelatedArtifacts.ToList()),
                new AttestationRequirementDirectRefs(testRefs, codeRefs),
                linkedArchitectures,
                linkedWorkItems,
                linkedVerifications,
                linkedWorkItems.Select(item => item.Status).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(status => status, StringComparer.OrdinalIgnoreCase).ToList(),
                linkedVerifications.Select(item => item.Status).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(status => status, StringComparer.OrdinalIgnoreCase).ToList(),
                testEvidenceStatus,
                coverageEvidenceStatus,
                benchmarkEvidenceStatus,
                manualQaStatus,
                gaps,
                BuildRequirementRollups(attestationConfig, linkedWorkItems, linkedVerifications, validationErrors, evidence)));
        }

        return records;
    }

    private static AttestationAggregateSummary BuildAggregateSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements,
        ValidationGraph graph,
        AttestationArtifactCollections artifacts)
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
            BuildWorkItemStatusSummary(attestationConfig, requirements),
            BuildVerificationStatusSummary(attestationConfig, requirements));
    }

    private static AttestationWorkItemStatusSummary BuildWorkItemStatusSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements)
    {
        var artifacts = requirements.SelectMany(requirement => requirement.LinkedWorkItems).DistinctBy(item => item.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList();
        return new AttestationWorkItemStatusSummary(
            artifacts.Count,
            requirements.Count(requirement => requirement.LinkedWorkItems.Count > 0),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemDone),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemInProgress),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemOpen),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.WorkItemBlocked),
            CountStatuses(artifacts, new[] { "unknown" }));
    }

    private static AttestationVerificationStatusSummary BuildVerificationStatusSummary(
        AttestationConfig attestationConfig,
        IList<AttestationRequirementRecord> requirements)
    {
        var artifacts = requirements.SelectMany(requirement => requirement.LinkedVerifications).DistinctBy(item => item.ArtifactId, StringComparer.OrdinalIgnoreCase).ToList();
        return new AttestationVerificationStatusSummary(
            artifacts.Count,
            requirements.Count(requirement => requirement.LinkedVerifications.Count > 0),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationPassing),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationFailing),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationPending),
            CountStatuses(artifacts, attestationConfig.StatusPolicy.VerificationStale),
            CountStatuses(artifacts, new[] { "unknown" }));
    }

    private static AttestationGapSummary BuildGapSummary(
        IList<AttestationRequirementRecord> requirements,
        ValidationResult selectedValidation)
    {
        var withoutDownstream = requirements.Where(requirement => requirement.Trace.SatisfiedBy.Count == 0 && requirement.Trace.ImplementedBy.Count == 0 && requirement.Trace.VerifiedBy.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var withoutImplementation = requirements.Where(requirement => requirement.Trace.ImplementedBy.Count == 0 && requirement.DirectRefs.TestRefs.Count == 0 && requirement.DirectRefs.CodeRefs.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var withoutVerification = requirements.Where(requirement => requirement.Trace.VerifiedBy.Count == 0 && requirement.LinkedVerifications.Count == 0).Select(requirement => requirement.RequirementId).ToList();
        var failingOrStale = requirements.Where(requirement => IsProblemEvidenceStatus(requirement.TestEvidenceStatus) || IsProblemEvidenceStatus(requirement.CoverageEvidenceStatus) || IsProblemEvidenceStatus(requirement.BenchmarkEvidenceStatus) || IsProblemEvidenceStatus(requirement.ManualQaStatus)).Select(requirement => requirement.RequirementId).ToList();

        var orphanArtifacts = selectedValidation.Findings
            .Where(finding => string.Equals(finding.Category, ValidationCategories.OrphanArtifact, StringComparison.OrdinalIgnoreCase))
            .Select(finding => finding.ArtifactId ?? finding.File ?? finding.Message)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unresolved = selectedValidation.Findings
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
        IList<AttestationArtifactSummary> linkedWorkItems,
        IList<AttestationArtifactSummary> linkedVerifications,
        IList<string> validationErrors,
        AttestationEvidenceSnapshot evidence)
    {
        if (attestationConfig.Rollups is null)
        {
            return null;
        }

        var implemented = attestationConfig.Rollups.Implemented && linkedWorkItems.Any(item => HasStatus(attestationConfig.StatusPolicy.WorkItemDone, item.Status));
        var verified = attestationConfig.Rollups.Verified && linkedVerifications.Any(item => HasStatus(attestationConfig.StatusPolicy.VerificationPassing, item.Status));
        var releaseReady = attestationConfig.Rollups.ReleaseReady &&
            (!attestationConfig.Rollups.RequireNoOpenWorkItems || !linkedWorkItems.Any(item => HasStatus(attestationConfig.StatusPolicy.WorkItemOpen, item.Status) || HasStatus(attestationConfig.StatusPolicy.WorkItemBlocked, item.Status))) &&
            (!attestationConfig.Rollups.RequireNoValidationErrors || validationErrors.Count == 0) &&
            (!attestationConfig.Rollups.RequireNoFailingVerifications || !linkedVerifications.Any(item => HasStatus(attestationConfig.StatusPolicy.VerificationFailing, item.Status))) &&
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
        IList<AttestationArtifactSummary> linkedVerifications,
        string testEvidenceStatus,
        string coverageEvidenceStatus,
        string benchmarkEvidenceStatus,
        string manualQaStatus,
        IList<string> validationErrors,
        IList<string> validationWarnings)
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

        if (verifiedBy.Count == 0 && linkedVerifications.Count == 0)
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
        ValidationGraph graph,
        ValidationResult selectedValidation)
    {
        var lookup = new Dictionary<string, AttestationArtifactSummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in graph.Artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.ArtifactId))
            {
                continue;
            }

            lookup[artifact.ArtifactId] = BuildArtifactSummary(artifact, selectedValidation);
        }

        return lookup;
    }

    private static AttestationArtifactSummary BuildArtifactSummary(CanonicalArtifactNode artifact, ValidationResult selectedValidation)
    {
        var findings = selectedValidation.Findings
            .Where(finding => IsFindingForArtifact(finding, artifact.ArtifactId, artifact.Path))
            .Select(FormatFinding)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var validationWarnings = findings.Where(message => message.StartsWith("[warning]", StringComparison.OrdinalIgnoreCase)).ToList();
        var validationErrors = findings.Where(message => message.StartsWith("[error]", StringComparison.OrdinalIgnoreCase)).ToList();

        return new AttestationArtifactSummary(
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Title,
            artifact.Status,
            artifact.Path,
            artifact.RepoRelativePath,
            new List<string>(),
            validationErrors,
            validationWarnings);
    }

    private static IList<AttestationArtifactSummary> BuildLinkedArtifacts(
        ValidationGraph graph,
        IReadOnlyDictionary<string, AttestationArtifactSummary> lookup,
        IReadOnlyList<string> ids,
        string expectedType)
    {
        var list = new List<AttestationArtifactSummary>();
        foreach (var id in ids)
        {
            foreach (var match in graph.ResolveArtifact(id))
            {
                if (!string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (lookup.TryGetValue(match.ArtifactId, out var summary))
                {
                    list.Add(summary);
                }
            }
        }

        return list
            .GroupBy(item => item.ArtifactId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.ArtifactId, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static bool IsFindingForArtifact(ValidationFinding finding, string artifactId, string path)
    {
        if (string.Equals(finding.ArtifactId, artifactId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (finding.File is not null && string.Equals(NormalizePathForComparison(finding.File), NormalizePathForComparison(path), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (finding.TargetFile is not null && string.Equals(NormalizePathForComparison(finding.TargetFile), NormalizePathForComparison(path), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string NormalizePathForComparison(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static string FormatFinding(ValidationFinding finding)
    {
        var prefix = string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase) ? "[warning]" : "[error]";
        string location;
        if (!string.IsNullOrWhiteSpace(finding.File))
        {
            location = finding.File;
        }
        else if (!string.IsNullOrWhiteSpace(finding.ArtifactId))
        {
            location = finding.ArtifactId;
        }
        else if (!string.IsNullOrWhiteSpace(finding.TargetId))
        {
            location = finding.TargetId;
        }
        else
        {
            location = string.Empty;
        }
        return string.IsNullOrWhiteSpace(location)
            ? $"{prefix} {finding.Category}: {finding.Message}"
            : $"{prefix} {finding.Category} {location}: {finding.Message}";
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

    private static void AddLinkedArtifactIds(ValidationGraph graph, string id, string expectedType, ISet<string> ids)
    {
        foreach (var match in graph.ResolveArtifact(id))
        {
            if (string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(match.ArtifactId);
            }
        }
    }
}
