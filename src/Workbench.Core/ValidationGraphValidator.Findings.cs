using System.Text;

namespace Workbench.Core;

internal static partial class ValidationGraphValidator
{
    private static void EmitDuplicateIdFindings(
        ValidationGraph graph,
        string selectedProfile,
        List<string> scopePrefixes,
        ValidationResult result)
    {
        foreach (var group in graph.ArtifactsById.Where(entry => entry.Value.Count > 1))
        {
            var first = group.Value[0];
            foreach (var duplicate in group.Value.Skip(1))
            {
                if (!ShouldEmitForScope(duplicate.RepoRelativePath, scopePrefixes))
                {
                    continue;
                }

                result.AddError(
                    selectedProfile,
                    ValidationCategories.DuplicateId,
                    $"duplicate artifact_id '{group.Key}' also appears in '{first.RepoRelativePath}'.",
                    file: duplicate.Path,
                    artifactId: group.Key,
                    targetFile: first.Path,
                    targetType: first.ArtifactType);
            }
        }

        foreach (var group in graph.RequirementsById.Where(entry => entry.Value.Count > 1))
        {
            var first = group.Value[0];
            foreach (var duplicate in group.Value.Skip(1))
            {
                if (!ShouldEmitForScope(duplicate.SpecRepoRelativePath, scopePrefixes))
                {
                    continue;
                }

                result.AddError(
                    selectedProfile,
                    ValidationCategories.DuplicateId,
                    $"duplicate requirement_id '{group.Key}' also appears in '{first.SpecRepoRelativePath}'.",
                    file: duplicate.SpecPath,
                    artifactId: group.Key,
                    targetFile: first.SpecPath,
                    targetType: "requirement");
            }
        }
    }

    private static void EmitTraceableFindings(
        ValidationGraph graph,
        string selectedProfile,
        List<string> scopePrefixes,
        ValidationResult result)
    {
        foreach (var requirement in graph.Requirements)
        {
            if (!ShouldEmitForScope(requirement.SpecRepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitRequirementDownstreamFindings(graph, selectedProfile, result, requirement);
            EmitRequirementReferenceFindings(graph, selectedProfile, result, requirement);
        }

        foreach (var specification in graph.Specifications)
        {
            if (!ShouldEmitForScope(specification.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitArtifactReferenceFindings(
                graph,
                selectedProfile,
                result,
                specification.Artifact,
                "specification",
                relatedArtifacts: specification.RelatedArtifacts);
        }

        foreach (var architecture in graph.Architectures)
        {
            if (!ShouldEmitForScope(architecture.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitArtifactReferenceFindings(
                graph,
                selectedProfile,
                result,
                architecture.Artifact,
                "architecture",
                architecture.Satisfies,
                architecture.RelatedArtifacts,
                addresses: null,
                designLinks: null,
                verificationLinks: null);
        }

        foreach (var workItem in graph.WorkItems)
        {
            if (!ShouldEmitForScope(workItem.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitArtifactReferenceFindings(
                graph,
                selectedProfile,
                result,
                workItem.Artifact,
                "work_item",
                addresses: workItem.Addresses,
                relatedArtifacts: workItem.RelatedArtifacts,
                designLinks: workItem.DesignLinks,
                verificationLinks: workItem.VerificationLinks);
        }

        foreach (var verification in graph.Verifications)
        {
            if (!ShouldEmitForScope(verification.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitArtifactReferenceFindings(
                graph,
                selectedProfile,
                result,
                verification.Artifact,
                "verification",
                verifies: verification.Verifies,
                relatedArtifacts: verification.RelatedArtifacts,
                addresses: null,
                designLinks: null,
                verificationLinks: null);
        }
    }

    private static void EmitAuditableFindings(
        ValidationGraph graph,
        string selectedProfile,
        List<string> scopePrefixes,
        ValidationResult result)
    {
        var downstreamTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in graph.Requirements)
        {
            if (!ShouldEmitForScope(requirement.SpecRepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitRequirementVerificationCoverage(graph, selectedProfile, result, requirement);
            CollectRequirementDownstreamTargets(graph, selectedProfile, result, requirement, downstreamTargets);
        }

        foreach (var architecture in graph.Architectures)
        {
            if (!ShouldEmitForScope(architecture.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitArchitectureReciprocalFindings(graph, selectedProfile, result, architecture);
            EmitArchitectureBodyConsistency(selectedProfile, result, architecture);
        }

        foreach (var workItem in graph.WorkItems)
        {
            if (!ShouldEmitForScope(workItem.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitWorkItemReciprocalFindings(graph, selectedProfile, result, workItem);
            EmitWorkItemBodyConsistency(selectedProfile, result, workItem);
        }

        foreach (var verification in graph.Verifications)
        {
            if (!ShouldEmitForScope(verification.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            EmitVerificationReciprocalFindings(graph, selectedProfile, result, verification);
            EmitVerificationBodyConsistency(selectedProfile, result, verification);
        }

        foreach (var architecture in graph.Architectures)
        {
            if (!ShouldEmitForScope(architecture.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            if (!downstreamTargets.Contains(architecture.Artifact.ArtifactId))
            {
                result.AddError(
                    selectedProfile,
                    ValidationCategories.OrphanArtifact,
                    $"architecture '{architecture.Artifact.ArtifactId}' is not targeted by any requirement downstream trace link.",
                    file: architecture.Artifact.Path,
                    artifactId: architecture.Artifact.ArtifactId);
            }
        }

        foreach (var workItem in graph.WorkItems)
        {
            if (!ShouldEmitForScope(workItem.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            if (!downstreamTargets.Contains(workItem.Artifact.ArtifactId))
            {
                result.AddError(
                    selectedProfile,
                    ValidationCategories.OrphanArtifact,
                    $"work item '{workItem.Artifact.ArtifactId}' is not targeted by any requirement downstream trace link.",
                    file: workItem.Artifact.Path,
                    artifactId: workItem.Artifact.ArtifactId);
            }
        }

        foreach (var verification in graph.Verifications)
        {
            if (!ShouldEmitForScope(verification.Artifact.RepoRelativePath, scopePrefixes))
            {
                continue;
            }

            if (!downstreamTargets.Contains(verification.Artifact.ArtifactId))
            {
                result.AddError(
                    selectedProfile,
                    ValidationCategories.OrphanArtifact,
                    $"verification '{verification.Artifact.ArtifactId}' is not targeted by any requirement downstream trace link.",
                    file: verification.Artifact.Path,
                    artifactId: verification.Artifact.ArtifactId);
            }
        }
    }

    private static void EmitRequirementDownstreamFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement)
    {
        var downstreamCount = 0;
        downstreamCount += EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Satisfied By",
            requirement.Trace.TryGetValue("Satisfied By", out var satisfiedBy) ? satisfiedBy : Array.Empty<string>(),
            expectedType: "architecture");
        downstreamCount += EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Implemented By",
            requirement.Trace.TryGetValue("Implemented By", out var implementedBy) ? implementedBy : Array.Empty<string>(),
            expectedType: "work_item");
        downstreamCount += EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Verified By",
            requirement.Trace.TryGetValue("Verified By", out var verifiedBy) ? verifiedBy : Array.Empty<string>(),
            expectedType: "verification");

        if (downstreamCount == 0)
        {
            result.AddError(
                profile,
                ValidationCategories.DownstreamMissing,
                $"requirement '{requirement.RequirementId}' has no downstream trace link; at least one downstream trace link is required.",
                file: requirement.SpecPath,
                artifactId: requirement.RequirementId);
        }
    }

    private static void EmitRequirementVerificationCoverage(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement)
    {
        if (HasResolvableRequirementArtifactReference(graph, requirement, "Verified By", "verification"))
        {
            return;
        }

        result.AddError(
            profile,
            ValidationCategories.VerificationMissing,
            $"requirement '{requirement.RequirementId}' has no Verified By link.",
            file: requirement.SpecPath,
            artifactId: requirement.RequirementId,
            field: "Verified By");
    }

    private static void CollectRequirementDownstreamTargets(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement,
        ISet<string> downstreamTargets)
    {
        CollectRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Satisfied By",
            requirement.Trace.TryGetValue("Satisfied By", out var satisfiedBy) ? satisfiedBy : Array.Empty<string>(),
            expectedType: "architecture",
            downstreamTargets);
        CollectRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Implemented By",
            requirement.Trace.TryGetValue("Implemented By", out var implementedBy) ? implementedBy : Array.Empty<string>(),
            expectedType: "work_item",
            downstreamTargets);
        CollectRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Verified By",
            requirement.Trace.TryGetValue("Verified By", out var verifiedBy) ? verifiedBy : Array.Empty<string>(),
            expectedType: "verification",
            downstreamTargets);
    }

    private static void EmitRequirementReferenceFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement)
    {
        EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Satisfied By",
            requirement.Trace.TryGetValue("Satisfied By", out var satisfiedBy) ? satisfiedBy : Array.Empty<string>(),
            expectedType: "architecture");
        EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Implemented By",
            requirement.Trace.TryGetValue("Implemented By", out var implementedBy) ? implementedBy : Array.Empty<string>(),
            expectedType: "work_item");
        EmitRequirementArtifactReferences(
            graph,
            profile,
            result,
            requirement,
            "Verified By",
            requirement.Trace.TryGetValue("Verified By", out var verifiedBy) ? verifiedBy : Array.Empty<string>(),
            expectedType: "verification");

        if (requirement.Trace.TryGetValue("Related", out var related))
        {
            foreach (var targetId in related)
            {
                if (TryResolveRequirementOrArtifact(graph, targetId))
                {
                    continue;
                }

                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved related reference '{targetId}' in requirement '{requirement.RequirementId}'.",
                    file: requirement.SpecPath,
                    artifactId: requirement.RequirementId,
                    field: "Related",
                    targetId: targetId);
            }
        }
    }

    private static void EmitArtifactReferenceFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        CanonicalArtifactNode artifact,
        string artifactType,
        IReadOnlyList<string>? satisfies = null,
        IReadOnlyList<string>? relatedArtifacts = null,
        IReadOnlyList<string>? addresses = null,
        IReadOnlyList<string>? designLinks = null,
        IReadOnlyList<string>? verificationLinks = null,
        IReadOnlyList<string>? verifies = null)
    {
        if (string.Equals(artifactType, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            EmitRequirementListReferences(
                graph,
                profile,
                result,
                artifact,
                "satisfies",
                satisfies ?? Array.Empty<string>(),
                expectedType: "requirement");
        }

        if (string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase))
        {
            EmitRequirementListReferences(
                graph,
                profile,
                result,
                artifact,
                "addresses",
                addresses ?? Array.Empty<string>(),
                expectedType: "requirement");
            EmitArtifactListReferences(
                graph,
                profile,
                result,
                artifact,
                "design_links",
                designLinks ?? Array.Empty<string>(),
                expectedType: "architecture");
            EmitArtifactListReferences(
                graph,
                profile,
                result,
                artifact,
                "verification_links",
                verificationLinks ?? Array.Empty<string>(),
                expectedType: "verification");
        }

        if (string.Equals(artifactType, "verification", StringComparison.OrdinalIgnoreCase))
        {
            EmitRequirementListReferences(
                graph,
                profile,
                result,
                artifact,
                "verifies",
                verifies ?? Array.Empty<string>(),
                expectedType: "requirement");
        }

        if (string.Equals(artifactType, "specification", StringComparison.OrdinalIgnoreCase))
        {
            // Specification front matter only carries related_artifacts.
        }

        if (relatedArtifacts is not null)
        {
            foreach (var targetId in relatedArtifacts)
            {
                if (graph.ResolveArtifact(targetId).Count > 0)
                {
                    continue;
                }

                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved related_artifacts reference '{targetId}' in '{artifact.ArtifactId}'.",
                    file: artifact.Path,
                    artifactId: artifact.ArtifactId,
                    field: "related_artifacts",
                    targetId: targetId);
            }
        }
    }

    private static void EmitArtifactListReferences(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        CanonicalArtifactNode artifact,
        string field,
        IReadOnlyList<string> values,
        string expectedType)
    {
        foreach (var targetId in values)
        {
            var matches = graph.ResolveArtifact(targetId);
            if (matches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved {expectedType} reference '{targetId}' in '{field}'.",
                    file: artifact.Path,
                    artifactId: artifact.ArtifactId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            if (!matches.Any(match => string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase)))
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"reference '{targetId}' in '{field}' resolves to a non-{expectedType} artifact.",
                    file: artifact.Path,
                    artifactId: artifact.ArtifactId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
            }
        }
    }

    private static void EmitRequirementListReferences(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        CanonicalArtifactNode artifact,
        string field,
        IReadOnlyList<string> values,
        string expectedType)
    {
        foreach (var targetId in values)
        {
            var matches = graph.ResolveRequirement(targetId);
            if (matches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved {expectedType} reference '{targetId}' in '{field}'.",
                    file: artifact.Path,
                    artifactId: artifact.ArtifactId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            if (!matches.Any(match => string.Equals(match.RequirementId, targetId, StringComparison.OrdinalIgnoreCase)))
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"reference '{targetId}' in '{field}' resolves to a non-{expectedType} artifact.",
                    file: artifact.Path,
                    artifactId: artifact.ArtifactId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
            }
        }
    }

    private static void EmitArchitectureReciprocalFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        ArchitectureNode architecture)
    {
        foreach (var requirementId in architecture.Satisfies)
        {
            foreach (var requirement in graph.ResolveRequirement(requirementId))
            {
                if (requirement.Trace.TryGetValue("Satisfied By", out var satisfiedBy) &&
                    satisfiedBy.Any(entry => string.Equals(entry, architecture.Artifact.ArtifactId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.AddError(
                    profile,
                    ValidationCategories.ReciprocalMismatch,
                    $"architecture '{architecture.Artifact.ArtifactId}' does not reciprocate requirement '{requirement.RequirementId}'.",
                    file: architecture.Artifact.Path,
                    artifactId: architecture.Artifact.ArtifactId,
                    field: "satisfies",
                    targetId: requirement.RequirementId,
                    targetType: "requirement",
                    targetFile: requirement.SpecPath);
            }
        }
    }

    private static void EmitWorkItemReciprocalFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        WorkItemNode workItem)
    {
        foreach (var requirementId in workItem.Addresses)
        {
            foreach (var requirement in graph.ResolveRequirement(requirementId))
            {
                if (requirement.Trace.TryGetValue("Implemented By", out var implementedBy) &&
                    implementedBy.Any(entry => string.Equals(entry, workItem.Artifact.ArtifactId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.AddError(
                    profile,
                    ValidationCategories.ReciprocalMismatch,
                    $"work item '{workItem.Artifact.ArtifactId}' does not reciprocate requirement '{requirement.RequirementId}'.",
                    file: workItem.Artifact.Path,
                    artifactId: workItem.Artifact.ArtifactId,
                    field: "addresses",
                    targetId: requirement.RequirementId,
                    targetType: "requirement",
                    targetFile: requirement.SpecPath);
            }
        }
    }

    private static void EmitVerificationReciprocalFindings(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        VerificationNode verification)
    {
        foreach (var requirementId in verification.Verifies)
        {
            foreach (var requirement in graph.ResolveRequirement(requirementId))
            {
                if (requirement.Trace.TryGetValue("Verified By", out var verifiedBy) &&
                    verifiedBy.Any(entry => string.Equals(entry, verification.Artifact.ArtifactId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.AddError(
                    profile,
                    ValidationCategories.ReciprocalMismatch,
                    $"verification '{verification.Artifact.ArtifactId}' does not reciprocate requirement '{requirement.RequirementId}'.",
                    file: verification.Artifact.Path,
                    artifactId: verification.Artifact.ArtifactId,
                    field: "verifies",
                    targetId: requirement.RequirementId,
                    targetType: "requirement",
                    targetFile: requirement.SpecPath);
            }
        }
    }

    private static void EmitArchitectureBodyConsistency(string profile, ValidationResult result, ArchitectureNode architecture)
    {
        EmitBodyListComparison(
            result,
            profile,
            architecture.Artifact.Path,
            architecture.Artifact.ArtifactId,
            "Requirements Satisfied",
            "satisfies",
            architecture.BodySatisfies,
            architecture.Satisfies);
    }

    private static void EmitWorkItemBodyConsistency(string profile, ValidationResult result, WorkItemNode workItem)
    {
        EmitBodyListComparison(
            result,
            profile,
            workItem.Artifact.Path,
            workItem.Artifact.ArtifactId,
            "Requirements Addressed",
            "addresses",
            workItem.BodyAddresses,
            workItem.Addresses);
        EmitBodyListComparison(
            result,
            profile,
            workItem.Artifact.Path,
            workItem.Artifact.ArtifactId,
            "Design Inputs",
            "design_links",
            workItem.BodyDesignLinks,
            workItem.DesignLinks);
        EmitBodyListComparison(
            result,
            profile,
            workItem.Artifact.Path,
            workItem.Artifact.ArtifactId,
            "Trace Links / Addresses",
            "addresses",
            workItem.TraceAddresses,
            workItem.Addresses);
        EmitBodyListComparison(
            result,
            profile,
            workItem.Artifact.Path,
            workItem.Artifact.ArtifactId,
            "Trace Links / Uses Design",
            "design_links",
            workItem.TraceDesignLinks,
            workItem.DesignLinks);
        EmitBodyListComparison(
            result,
            profile,
            workItem.Artifact.Path,
            workItem.Artifact.ArtifactId,
            "Trace Links / Verified By",
            "verification_links",
            workItem.TraceVerificationLinks,
            workItem.VerificationLinks);
    }

    private static void EmitVerificationBodyConsistency(string profile, ValidationResult result, VerificationNode verification)
    {
        EmitBodyListComparison(
            result,
            profile,
            verification.Artifact.Path,
            verification.Artifact.ArtifactId,
            "Requirements Verified",
            "verifies",
            verification.BodyVerifies,
            verification.Verifies);
        EmitBodyListComparison(
            result,
            profile,
            verification.Artifact.Path,
            verification.Artifact.ArtifactId,
            "Related Artifacts",
            "related_artifacts",
            verification.BodyRelatedArtifacts,
            verification.RelatedArtifacts);
    }

    private static void EmitBodyListComparison(
        ValidationResult result,
        string profile,
        string file,
        string artifactId,
        string sourceLabel,
        string field,
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected)
    {
        if (ListsEquivalent(actual, expected))
        {
            return;
        }

        result.AddError(
            profile,
            ValidationCategories.BodyMismatch,
            DescribeListMismatch(sourceLabel, field, actual, expected),
            file: file,
            artifactId: artifactId,
            field: field);
    }

    private static void CollectRequirementArtifactReferences(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement,
        string field,
        IReadOnlyList<string> values,
        string expectedType,
        ISet<string> downstreamTargets)
    {
        foreach (var targetId in values)
        {
            var matches = graph.ResolveArtifact(targetId);
            if (matches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved {expectedType} reference '{targetId}' in '{field}'.",
                    file: requirement.SpecPath,
                    artifactId: requirement.RequirementId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            var typedMatches = matches.Where(match => string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (typedMatches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"reference '{targetId}' in '{field}' resolves to a non-{expectedType} artifact.",
                    file: requirement.SpecPath,
                    artifactId: requirement.RequirementId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            foreach (var match in typedMatches)
            {
                downstreamTargets.Add(match.ArtifactId);
            }
        }
    }

    private static int EmitRequirementArtifactReferences(
        ValidationGraph graph,
        string profile,
        ValidationResult result,
        RequirementNode requirement,
        string field,
        IReadOnlyList<string> values,
        string expectedType)
    {
        var count = 0;
        foreach (var targetId in values)
        {
            var matches = graph.ResolveArtifact(targetId);
            if (matches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"unresolved {expectedType} reference '{targetId}' in '{field}'.",
                    file: requirement.SpecPath,
                    artifactId: requirement.RequirementId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            var typedMatches = matches.Where(match => string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (typedMatches.Count == 0)
            {
                result.AddError(
                    profile,
                    ValidationCategories.UnresolvedReference,
                    $"reference '{targetId}' in '{field}' resolves to a non-{expectedType} artifact.",
                    file: requirement.SpecPath,
                    artifactId: requirement.RequirementId,
                    field: field,
                    targetId: targetId,
                    targetType: expectedType);
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool HasResolvableRequirementArtifactReference(
        ValidationGraph graph,
        RequirementNode requirement,
        string field,
        string expectedType)
    {
        if (!requirement.Trace.TryGetValue(field, out var values))
        {
            return false;
        }

        foreach (var targetId in values)
        {
            var matches = graph.ResolveArtifact(targetId);
            if (matches.Any(match => string.Equals(match.ArtifactType, expectedType, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveRequirementOrArtifact(ValidationGraph graph, string targetId)
    {
        return graph.ResolveRequirement(targetId).Count > 0 || graph.ResolveArtifact(targetId).Count > 0;
    }

    private static bool ListsEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var normalizedLeft = left
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedRight = right
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalizedLeft.SequenceEqual(normalizedRight, StringComparer.OrdinalIgnoreCase);
    }

    private static string DescribeListMismatch(string sourceLabel, string field, IReadOnlyList<string> actual, IReadOnlyList<string> expected)
    {
        var missing = expected.Where(expectedValue => !actual.Any(actualValue => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))).ToList();
        var extra = actual.Where(actualValue => !expected.Any(expectedValue => string.Equals(expectedValue, actualValue, StringComparison.OrdinalIgnoreCase))).ToList();
        var builder = new StringBuilder();
        builder.Append($"{sourceLabel} does not match '{field}'.");
        if (missing.Count > 0)
        {
            builder.Append($" Missing: {string.Join(", ", missing)}.");
        }
        if (extra.Count > 0)
        {
            builder.Append($" Extra: {string.Join(", ", extra)}.");
        }
        return builder.ToString();
    }
}
