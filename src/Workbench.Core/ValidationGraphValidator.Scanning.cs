using System.Collections;
using System.Text.RegularExpressions;

namespace Workbench.Core;

internal static partial class ValidationGraphValidator
{
    private static readonly Regex approvedKeywordRegex = new(
        @"\b(?:MUST NOT|SHALL NOT|SHOULD NOT|MUST|SHALL|SHOULD|MAY)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    internal static ValidationGraph BuildGraph(
        string repoRoot,
        WorkbenchConfig config,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        List<string> scopePrefixes,
        List<string> docExcludes)
    {
        var graph = new ValidationGraph();
        foreach (var source in CanonicalArtifactDiscovery.EnumerateCanonicalSources(repoRoot, config))
        {
            if (docExcludes.Count > 0 &&
                docExcludes.Any(prefix => source.SourceRepoRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ScanCanonicalDocument(
                repoRoot,
                options,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                scopePrefixes);
        }

        return graph;
    }

    private static void ScanCanonicalDocument(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        List<string> scopePrefixes)
    {
        if (string.Equals(source.Format, "cue", StringComparison.OrdinalIgnoreCase))
        {
            ScanCueArtifact(
                repoRoot,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                scopePrefixes);
            return;
        }

        var file = source.SourcePath;
        var repoRelative = source.SourceRepoRelativePath;
        var shouldEmit = ShouldEmitForScope(repoRelative, scopePrefixes);

        var content = File.ReadAllText(file);
        if (!LooksLikeFrontMatter(content))
        {
            return;
        }

        if (!FrontMatter.TryParse(content, out var frontMatter, out var parseError))
        {
            if (shouldEmit)
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    StripLocationPrefix(parseError ?? string.Empty, file),
                    file: file);
            }

            return;
        }

        var data = frontMatter!.Data;
        var artifactType = GetString(data, "artifact_type");
        if (string.IsNullOrWhiteSpace(artifactType))
        {
            return;
        }

        var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
        var title = GetString(data, "title");
        var domain = GetString(data, "domain");
        var capability = GetString(data, "capability");

        if (string.Equals(artifactType, "specification", StringComparison.OrdinalIgnoreCase))
        {
            ScanSpecificationDocument(
                repoRoot,
                options,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                file,
                repoRelative,
                shouldEmit,
                artifactId,
                title,
                domain,
                capability,
                data,
                frontMatter.Body);
            return;
        }

        if (string.Equals(artifactType, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            ScanArchitectureDocument(
                repoRoot,
                options,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                file,
                repoRelative,
                shouldEmit,
                artifactId,
                title,
                domain,
                GetString(data, "capability"),
                data,
                frontMatter.Body);
            return;
        }

        if (string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase))
        {
            ScanWorkItemDocument(
                repoRoot,
                options,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                file,
                repoRelative,
                shouldEmit,
                artifactId,
                title,
                domain,
                GetString(data, "capability"),
                data,
                frontMatter.Body);
            return;
        }

        if (string.Equals(artifactType, "verification", StringComparison.OrdinalIgnoreCase))
        {
            ScanVerificationDocument(
                repoRoot,
                options,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                file,
                repoRelative,
                shouldEmit,
                artifactId,
                title,
                domain,
                GetString(data, "capability"),
                data,
                frontMatter.Body);
        }
    }

    private static void ScanCueArtifact(
        string repoRoot,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        List<string> scopePrefixes)
    {
        var shouldEmit = ShouldEmitForScope(source.SourceRepoRelativePath, scopePrefixes);
        CueArtifactModel artifact;
        try
        {
            artifact = CueCli.ExportArtifact(repoRoot, source.SourcePath);
        }
        catch (Exception ex)
        {
            if (shouldEmit)
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    ex.ToString(),
                    file: source.SourcePath);
            }

            return;
        }

        var artifactType = artifact.ArtifactType;
        if (string.IsNullOrWhiteSpace(artifactType))
        {
            if (shouldEmit)
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    "cue artifact export did not include artifact_type.",
                    file: source.SourcePath,
                    artifactId: artifact.ArtifactId);
            }

            return;
        }

        if (string.Equals(artifactType, "specification", StringComparison.OrdinalIgnoreCase))
        {
            ScanCueSpecificationDocument(
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                shouldEmit,
                artifact);
            return;
        }

        if (string.Equals(artifactType, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            ScanCueArchitectureDocument(
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                shouldEmit,
                artifact);
            return;
        }

        if (string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase))
        {
            ScanCueWorkItemDocument(
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                shouldEmit,
                artifact);
            return;
        }

        if (string.Equals(artifactType, "verification", StringComparison.OrdinalIgnoreCase))
        {
            ScanCueVerificationDocument(
                repoRoot,
                enforceArtifactIdPolicy,
                artifactIdPolicy,
                result,
                graph,
                source,
                shouldEmit,
                artifact);
        }
    }

    private static void ScanCueSpecificationDocument(
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        bool shouldEmit,
        CueArtifactModel artifact)
    {
        if (shouldEmit && !SpecTraceLayout.IsSpecificationRootFile(source.SourceRepoRelativePath))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"specifications must live under '{SpecTraceLayout.RequirementsRoot}/<domain>/'.",
                file: source.SourcePath,
                artifactId: artifact.ArtifactId);
        }

        EmitCueArtifactValidation(
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            source.SourcePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Capability);

        var artifactNode = CreateArtifactNode(
            source.SourcePath,
            source.SourceRepoRelativePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Title,
            artifact.Status);
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var relatedArtifacts = NormalizeList(artifact.RelatedArtifacts);
        var requirements = new List<RequirementNode>();
        foreach (var clause in artifact.Requirements ?? [])
        {
            if (shouldEmit)
            {
                if (string.IsNullOrWhiteSpace(clause.Id))
                {
                    result.AddError(
                        ValidationProfiles.Core,
                        ValidationCategories.Schema,
                        "requirement_id is missing.",
                        file: source.SourcePath,
                        artifactId: artifact.ArtifactId);
                }

                if (string.IsNullOrWhiteSpace(clause.Title))
                {
                    result.AddError(
                        ValidationProfiles.Core,
                        ValidationCategories.Schema,
                        $"requirement '{clause.Id}' is missing title.",
                        file: source.SourcePath,
                        artifactId: artifact.ArtifactId,
                        field: clause.Id);
                }

                if (string.IsNullOrWhiteSpace(clause.Statement))
                {
                    result.AddError(
                        ValidationProfiles.Core,
                        ValidationCategories.Schema,
                        $"requirement '{clause.Id}' is missing statement.",
                        file: source.SourcePath,
                        artifactId: artifact.ArtifactId,
                        field: clause.Id);
                }

                var keywordCount = approvedKeywordRegex.Matches(clause.Statement ?? string.Empty).Count;
                if (keywordCount != 1)
                {
                    result.AddError(
                        ValidationProfiles.Core,
                        ValidationCategories.Keyword,
                        $"requirement '{clause.Id}' must contain exactly one approved normative keyword, found {keywordCount}.",
                        file: source.SourcePath,
                        artifactId: artifact.ArtifactId,
                        field: clause.Id);
                }
            }

            var trace = BuildRequirementTrace(clause.Trace);
            requirements.Add(new RequirementNode(
                clause.Id,
                artifactNode.ArtifactId,
                source.SourcePath,
                source.SourceRepoRelativePath,
                clause.Title,
                clause.Statement ?? string.Empty,
                trace,
                relatedArtifacts,
                ReadTraceList(trace, "Source Refs"),
                ReadTraceList(trace, "Test Refs"),
                ReadTraceList(trace, "Code Refs")));
        }

        if (shouldEmit && requirements.Count == 0)
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Schema,
                "no requirement clauses found in exported specification.",
                file: source.SourcePath,
                artifactId: artifact.ArtifactId);
        }

        graph.Specifications.Add(new SpecificationNode(artifactNode, relatedArtifacts, requirements));
        foreach (var requirement in requirements)
        {
            graph.AddRequirement(requirement);
        }
    }

    private static void ScanCueArchitectureDocument(
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        bool shouldEmit,
        CueArtifactModel artifact)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalArchitecturePath(source.SourceRepoRelativePath))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"architecture artifacts must live under '{SpecTraceLayout.ArchitectureRoot}/<domain>/'.",
                file: source.SourcePath,
                artifactId: artifact.ArtifactId);
        }

        EmitCueArtifactValidation(
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            source.SourcePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Capability);

        var artifactNode = CreateArtifactNode(
            source.SourcePath,
            source.SourceRepoRelativePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Title,
            artifact.Status);
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var satisfies = NormalizeList(artifact.Satisfies);
        var relatedArtifacts = NormalizeList(artifact.RelatedArtifacts);
        graph.Architectures.Add(new ArchitectureNode(artifactNode, satisfies, satisfies, relatedArtifacts));
    }

    private static void ScanCueWorkItemDocument(
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        bool shouldEmit,
        CueArtifactModel artifact)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalWorkItemPath(source.SourceRepoRelativePath))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"work-item artifacts must live under '{SpecTraceLayout.WorkItemsRoot}/<domain>/'.",
                file: source.SourcePath,
                artifactId: artifact.ArtifactId);
        }

        EmitCueArtifactValidation(
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            source.SourcePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Capability);

        var artifactNode = CreateArtifactNode(
            source.SourcePath,
            source.SourceRepoRelativePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Title,
            artifact.Status);
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var addresses = NormalizeList(artifact.Addresses);
        var designLinks = NormalizeList(artifact.DesignLinks);
        var verificationLinks = NormalizeList(artifact.VerificationLinks);
        var relatedArtifacts = NormalizeList(artifact.RelatedArtifacts);
        graph.WorkItems.Add(new WorkItemNode(
            artifactNode,
            addresses,
            designLinks,
            verificationLinks,
            addresses,
            designLinks,
            verificationLinks,
            addresses,
            designLinks,
            verificationLinks,
            relatedArtifacts));
    }

    private static void ScanCueVerificationDocument(
        string repoRoot,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        CanonicalArtifactSource source,
        bool shouldEmit,
        CueArtifactModel artifact)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalVerificationPath(source.SourceRepoRelativePath))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"verification artifacts must live under '{SpecTraceLayout.VerificationRoot}/<domain>/'.",
                file: source.SourcePath,
                artifactId: artifact.ArtifactId);
        }

        EmitCueArtifactValidation(
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            source.SourcePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Capability);

        var artifactNode = CreateArtifactNode(
            source.SourcePath,
            source.SourceRepoRelativePath,
            artifact.ArtifactId,
            artifact.ArtifactType,
            artifact.Domain,
            artifact.Title,
            artifact.Status);
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var verifies = NormalizeList(artifact.Verifies);
        var relatedArtifacts = NormalizeList(artifact.RelatedArtifacts);
        var evidenceRefs = NormalizeEvidenceRefs(repoRoot, source.SourcePath, artifact.Evidence);
        var benchmarkNotApplicable = artifact.Evidence?.Any(TryParseBenchmarkNotApplicable) == true;

        graph.Verifications.Add(new VerificationNode(
            artifactNode,
            verifies,
            verifies,
            relatedArtifacts,
            relatedArtifacts,
            evidenceRefs,
            benchmarkNotApplicable));
    }

    private static void ScanSpecificationDocument(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        string file,
        string repoRelative,
        bool shouldEmit,
        string? artifactId,
        string? title,
        string? domain,
        string? capability,
        IDictionary<string, object?> data,
        string body)
    {
        if (shouldEmit && !SpecTraceLayout.IsSpecificationRootFile(repoRelative))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"specifications must live under '{SpecTraceLayout.RequirementsRoot}/<domain>/'.",
                file: file,
                artifactId: artifactId);
        }

        EmitFrontMatterValidation(
            repoRoot,
            options,
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            file,
            artifactId,
            "specification",
            domain,
            capability,
            data);

        var artifactNode = CreateArtifactNode(file, repoRelative, artifactId, "specification", domain, title, GetString(data, "status"));
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var relatedArtifacts = ReadStringList(data, "related_artifacts");
        var requirementClauses = SpecTraceMarkdown.ParseRequirementClauses(body, out var parseErrors);
        if (shouldEmit)
        {
            foreach (var error in parseErrors)
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    StripLocationPrefix(error, file),
                    file: file,
                    artifactId: artifactId);
            }

            if (requirementClauses.Count == 0)
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    "no requirement clauses found in specification body.",
                    file: file,
                    artifactId: artifactId);
            }
        }

        var requirements = new List<RequirementNode>();
        foreach (var clause in requirementClauses)
        {
            var clauseContext = $"{file}::{clause.RequirementId}";
            if (shouldEmit)
            {
                foreach (var error in SchemaValidationService.ValidateRequirementClause(repoRoot, clauseContext, clause))
                {
                    var category = error.Contains("unsupported normative keyword", StringComparison.OrdinalIgnoreCase)
                        ? ValidationCategories.Keyword
                        : ValidationCategories.Schema;
                    result.AddError(
                        ValidationProfiles.Core,
                        category,
                        StripLocationPrefix(error, clauseContext),
                        file: file,
                        artifactId: artifactId,
                        field: clause.RequirementId);
                }
            }

            if (clause.Trace is not null && !options.SkipDocSchema && shouldEmit)
            {
                var traceData = clause.Trace.ToDictionary(
                    entry => entry.Key,
                    entry => (object?)entry.Value,
                    StringComparer.OrdinalIgnoreCase);
                foreach (var error in SchemaValidationService.ValidateRequirementTraceFields(repoRoot, clauseContext, traceData))
                {
                    result.AddError(
                        ValidationProfiles.Core,
                        ValidationCategories.Schema,
                        StripLocationPrefix(error, clauseContext),
                        file: file,
                        artifactId: artifactId,
                        field: clause.RequirementId);
                }
            }

            requirements.Add(new RequirementNode(
                clause.RequirementId,
                artifactNode.ArtifactId,
                file,
                repoRelative,
                clause.Title,
                clause.Clause,
                clause.Trace ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                relatedArtifacts,
                ReadTraceList(clause.Trace, "Source Refs"),
                ReadTraceList(clause.Trace, "Test Refs"),
                ReadTraceList(clause.Trace, "Code Refs")));
        }

        graph.Specifications.Add(new SpecificationNode(artifactNode, relatedArtifacts, requirements));
        foreach (var requirement in requirements)
        {
            graph.AddRequirement(requirement);
        }
    }

    private static void ScanArchitectureDocument(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        string file,
        string repoRelative,
        bool shouldEmit,
        string? artifactId,
        string? title,
        string? domain,
        string? capability,
        IDictionary<string, object?> data,
        string body)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalArchitecturePath(repoRelative))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"architecture artifacts must live under '{SpecTraceLayout.ArchitectureRoot}/<domain>/'.",
                file: file,
                artifactId: artifactId);
        }

        EmitFrontMatterValidation(
            repoRoot,
            options,
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            file,
            artifactId,
            "architecture",
            domain,
            capability,
            data);

        var artifactNode = CreateArtifactNode(file, repoRelative, artifactId, "architecture", domain, title, GetString(data, "status"));
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var satisfies = ReadStringList(data, "satisfies");
        var relatedArtifacts = ReadStringList(data, "related_artifacts");
        var bodySatisfies = ParseLooseBulletList(SpecTraceMarkdown.ExtractSection(body, "Requirements Satisfied"));

        graph.Architectures.Add(new ArchitectureNode(artifactNode, satisfies, bodySatisfies, relatedArtifacts));
    }

    private static void ScanWorkItemDocument(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        string file,
        string repoRelative,
        bool shouldEmit,
        string? artifactId,
        string? title,
        string? domain,
        string? capability,
        IDictionary<string, object?> data,
        string body)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalWorkItemPath(repoRelative))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"work-item artifacts must live under '{SpecTraceLayout.WorkItemsRoot}/<domain>/'.",
                file: file,
                artifactId: artifactId);
        }

        EmitFrontMatterValidation(
            repoRoot,
            options,
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            file,
            artifactId,
            "work_item",
            domain,
            capability,
            data);

        var artifactNode = CreateArtifactNode(file, repoRelative, artifactId, "work_item", domain, title, GetString(data, "status"));
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var addresses = ReadStringList(data, "addresses");
        var designLinks = ReadStringList(data, "design_links");
        var verificationLinks = ReadStringList(data, "verification_links");
        var relatedArtifacts = ReadStringList(data, "related_artifacts");
        var bodyAddresses = ParseLooseBulletList(SpecTraceMarkdown.ExtractSection(body, "Requirements Addressed"));
        var bodyDesignLinks = ParseLooseBulletList(SpecTraceMarkdown.ExtractSection(body, "Design Inputs"));
        var traceLinks = ParseTraceLinksSection(SpecTraceMarkdown.ExtractSection(body, "Trace Links"));
        var traceAddresses = traceLinks.TryGetValue("Addresses", out var addressesValues) ? addressesValues : Array.Empty<string>();
        var traceDesignLinks = traceLinks.TryGetValue("Uses Design", out var designValues) ? designValues : Array.Empty<string>();
        var traceVerificationLinks = traceLinks.TryGetValue("Verified By", out var verifiedValues) ? verifiedValues : Array.Empty<string>();

        graph.WorkItems.Add(new WorkItemNode(
            artifactNode,
            addresses,
            designLinks,
            verificationLinks,
            bodyAddresses,
            bodyDesignLinks,
            traceVerificationLinks,
            traceAddresses,
            traceDesignLinks,
            traceVerificationLinks,
            relatedArtifacts));
    }

    private static void ScanVerificationDocument(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        string file,
        string repoRelative,
        bool shouldEmit,
        string? artifactId,
        string? title,
        string? domain,
        string? capability,
        IDictionary<string, object?> data,
        string body)
    {
        if (shouldEmit && !SpecTraceLayout.IsCanonicalVerificationPath(repoRelative))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Placement,
                $"verification artifacts must live under '{SpecTraceLayout.VerificationRoot}/<domain>/'.",
                file: file,
                artifactId: artifactId);
        }

        EmitFrontMatterValidation(
            repoRoot,
            options,
            enforceArtifactIdPolicy,
            artifactIdPolicy,
            result,
            shouldEmit,
            file,
            artifactId,
            "verification",
            domain,
            capability,
            data);

        var artifactNode = CreateArtifactNode(file, repoRelative, artifactId, "verification", domain, title, GetString(data, "status"));
        if (!string.IsNullOrWhiteSpace(artifactNode.ArtifactId))
        {
            graph.AddArtifact(artifactNode);
        }

        var verifies = ReadStringList(data, "verifies");
        var relatedArtifacts = ReadStringList(data, "related_artifacts");
        var bodyVerifies = ParseLooseBulletList(SpecTraceMarkdown.ExtractSection(body, "Requirements Verified"));
        var bodyRelatedArtifacts = ParseLooseBulletList(SpecTraceMarkdown.ExtractSection(body, "Related Artifacts"));
        var (evidenceRefs, benchmarkNotApplicable) = ParseVerificationEvidenceSection(
            repoRoot,
            file,
            SpecTraceMarkdown.ExtractSection(body, "Evidence"));

        graph.Verifications.Add(new VerificationNode(
            artifactNode,
            verifies,
            bodyVerifies,
            bodyRelatedArtifacts,
            relatedArtifacts,
            evidenceRefs,
            benchmarkNotApplicable));
    }

    private static void EmitFrontMatterValidation(
        string repoRoot,
        ValidationOptions options,
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        bool shouldEmit,
        string file,
        string? artifactId,
        string artifactType,
        string? domain,
        string? capability,
        IDictionary<string, object?> data)
    {
        if (!shouldEmit)
        {
            return;
        }

        if (!options.SkipDocSchema)
        {
            foreach (var error in SchemaValidationService.ValidateArtifactFrontMatter(repoRoot, file, data))
            {
                result.AddError(
                    ValidationProfiles.Core,
                    ValidationCategories.Schema,
                    StripLocationPrefix(error, file),
                    file: file,
                    artifactId: artifactId);
            }
        }

        if (enforceArtifactIdPolicy &&
            !string.IsNullOrWhiteSpace(artifactId) &&
            !artifactIdPolicy.MatchesArtifactId(artifactType, artifactId, domain, capability))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Identifier,
                $"artifact_id '{artifactId}' does not match the configured artifact ID policy.",
                file: file,
                artifactId: artifactId);
        }
    }

    private static void EmitCueArtifactValidation(
        bool enforceArtifactIdPolicy,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        bool shouldEmit,
        string file,
        string? artifactId,
        string artifactType,
        string? domain,
        string? capability)
    {
        _ = enforceArtifactIdPolicy;
        _ = artifactIdPolicy;
        _ = artifactType;
        _ = domain;
        _ = capability;

        if (!shouldEmit)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(artifactId))
        {
            result.AddError(
                ValidationProfiles.Core,
                ValidationCategories.Schema,
                "cue artifact export did not include artifact_id.",
                file: file);
        }
    }

    private static CanonicalArtifactNode CreateArtifactNode(
        string file,
        string repoRelative,
        string? artifactId,
        string artifactType,
        string? domain,
        string? title,
        string? status)
    {
        return new CanonicalArtifactNode(
            artifactId ?? string.Empty,
            artifactType,
            file,
            repoRelative,
            domain ?? string.Empty,
            title ?? string.Empty,
            status ?? string.Empty);
    }

    private static IReadOnlyList<string> ParseLooseBulletList(string section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        foreach (var rawLine in SpecTraceMarkdown.NormalizeNewlines(section).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                line = line[2..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                items.Add(line);
            }
        }

        return items;
    }

    private static (IReadOnlyList<string> EvidenceRefs, bool BenchmarkNotApplicable) ParseVerificationEvidenceSection(
        string repoRoot,
        string file,
        string section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return (Array.Empty<string>(), false);
        }

        var evidenceRefs = new List<string>();
        var benchmarkNotApplicable = false;

        foreach (var rawLine in SpecTraceMarkdown.NormalizeNewlines(section).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                line = line[2..].Trim();
            }

            if (TryParseBenchmarkNotApplicable(line))
            {
                benchmarkNotApplicable = true;
                continue;
            }

            var normalized = NormalizeVerificationEvidenceReference(repoRoot, file, line);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                evidenceRefs.Add(normalized);
            }
        }

        return (
            evidenceRefs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            benchmarkNotApplicable);
    }

    private static bool TryParseBenchmarkNotApplicable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("benchmark:", StringComparison.Ordinal))
        {
            normalized = normalized["benchmark:".Length..].Trim();
        }

        normalized = normalized
            .Replace('-', '_')
            .Replace(' ', '_');

        return normalized is "not_applicable" or "n_a" or "na" or "none" or "optional" or "not_required";
    }

    private static string? NormalizeVerificationEvidenceReference(string repoRoot, string file, string reference)
    {
        var candidate = reference.Trim();
        if (TryExtractMarkdownLinkTarget(candidate, out var target))
        {
            candidate = target;
        }

        candidate = candidate
            .Trim()
            .Trim('<', '>', '"', '\'')
            .Trim();

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

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (Path.IsPathRooted(candidate))
        {
            return NormalizeRepoRelative(repoRoot, candidate);
        }

        if (candidate.StartsWith("./", StringComparison.Ordinal) ||
            candidate.StartsWith("../", StringComparison.Ordinal))
        {
            var combined = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file) ?? repoRoot, candidate));
            return NormalizeRepoRelative(repoRoot, combined);
        }

        return candidate.Replace('\\', '/').TrimStart('/');
    }

    private static bool TryExtractMarkdownLinkTarget(string value, out string target)
    {
        var open = value.IndexOf("](", StringComparison.Ordinal);
        if (value.StartsWith("[", StringComparison.Ordinal) && open > 0 && value.EndsWith(")", StringComparison.Ordinal))
        {
            target = value[(open + 2)..^1].Trim();
            return true;
        }

        target = string.Empty;
        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseTraceLinksSection(string section)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(section))
        {
            return result;
        }

        var working = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentLabel = null;
        foreach (var rawLine in SpecTraceMarkdown.NormalizeNewlines(section).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                currentLabel = trimmed[..^1].Trim();
                if (!working.ContainsKey(currentLabel))
                {
                    working[currentLabel] = new List<string>();
                }

                continue;
            }

            if (currentLabel is null)
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                trimmed = trimmed[2..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                working[currentLabel].Add(trimmed);
            }
        }

        foreach (var entry in working)
        {
            result[entry.Key] = entry.Value;
        }

        return result;
    }

    private static IReadOnlyList<string> ReadStringList(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : new[] { text.Trim() };
        }

        if (value is not IEnumerable enumerable)
        {
            return Array.Empty<string>();
        }

        var items = new List<string>();
        foreach (var item in enumerable)
        {
            if (item is string entry && !string.IsNullOrWhiteSpace(entry))
            {
                items.Add(entry.Trim());
            }
        }

        return items;
    }

    private static IReadOnlyList<string> ReadTraceList(IReadOnlyDictionary<string, IReadOnlyList<string>>? trace, string label)
    {
        if (trace is null)
        {
            return Array.Empty<string>();
        }

        return trace.TryGetValue(label, out var values) ? values : Array.Empty<string>();
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

    private static string NormalizeRepoRelative(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        return relative.TrimStart('/');
    }

    private static bool ShouldEmitForScope(string repoRelative, List<string> scopePrefixes)
    {
        if (scopePrefixes.Count == 0)
        {
            return true;
        }

        return scopePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRequirementTrace(CueRequirementTraceModel? trace)
    {
        var values = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        AddTraceValues(values, "Satisfied By", trace?.SatisfiedBy);
        AddTraceValues(values, "Implemented By", trace?.ImplementedBy);
        AddTraceValues(values, "Verified By", trace?.VerifiedBy);
        AddTraceValues(values, "Derived From", trace?.DerivedFrom);
        AddTraceValues(values, "Supersedes", trace?.Supersedes);
        AddTraceValues(values, "Source Refs", trace?.UpstreamRefs);
        AddTraceValues(values, "Related", trace?.Related);
        return values;
    }

    private static IReadOnlyList<string> NormalizeEvidenceRefs(string repoRoot, string sourcePath, IEnumerable<string>? evidence)
    {
        var values = new List<string>();
        foreach (var entry in evidence ?? [])
        {
            if (TryParseBenchmarkNotApplicable(entry))
            {
                continue;
            }

            var normalized = NormalizeVerificationEvidenceReference(repoRoot, sourcePath, entry);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                values.Add(normalized);
            }
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddTraceValues(
        IDictionary<string, IReadOnlyList<string>> output,
        string label,
        IEnumerable<string>? values)
    {
        var normalized = NormalizeList(values);
        if (normalized.Count > 0)
        {
            output[label] = normalized;
        }
    }

    private static bool LooksLikeFrontMatter(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
               trimmed.StartsWith("---\r\n", StringComparison.Ordinal);
    }

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string StripLocationPrefix(string message, string location)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(location))
        {
            return message;
        }

        var prefix = $"{location}: ";
        if (message.StartsWith(prefix, StringComparison.Ordinal))
        {
            return message[prefix.Length..];
        }

        return message;
    }
}
