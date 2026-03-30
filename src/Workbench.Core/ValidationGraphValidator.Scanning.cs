using System.Collections;

namespace Workbench.Core;

internal static partial class ValidationGraphValidator
{
    internal static ValidationGraph BuildGraph(
        string repoRoot,
        WorkbenchConfig config,
        ValidationOptions options,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        List<string> scopePrefixes,
        List<string> docExcludes)
    {
        var graph = new ValidationGraph();
        var specsRoot = ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.SpecsRoot) ? SpecTraceLayout.SpecsRoot : config.Paths.SpecsRoot);
        var requirementsRoot = Path.Combine(specsRoot, "requirements");
        var architectureRoot = ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.ArchitectureDir) ? SpecTraceLayout.ArchitectureRoot : config.Paths.ArchitectureDir);
        var workItemsRoot = ResolveRepoPath(repoRoot, string.IsNullOrWhiteSpace(config.Paths.WorkItemsSpecsDir) ? SpecTraceLayout.WorkItemsRoot : config.Paths.WorkItemsSpecsDir);
        var verificationRoot = Path.Combine(specsRoot, "verification");

        foreach (var root in new[] { requirementsRoot, architectureRoot, workItemsRoot, verificationRoot })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                if (ShouldSkipCanonicalMarkdown(file))
                {
                    continue;
                }

                var repoRelative = NormalizeRepoRelative(repoRoot, file);
                if (docExcludes.Count > 0 &&
                    docExcludes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                ScanCanonicalDocument(
                    repoRoot,
                    options,
                    artifactIdPolicy,
                    result,
                    graph,
                    file,
                    scopePrefixes);
            }
        }

        return graph;
    }

    private static void ScanCanonicalDocument(
        string repoRoot,
        ValidationOptions options,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result,
        ValidationGraph graph,
        string file,
        List<string> scopePrefixes)
    {
        var repoRelative = NormalizeRepoRelative(repoRoot, file);
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

    private static void ScanSpecificationDocument(
        string repoRoot,
        ValidationOptions options,
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

        if (!string.IsNullOrWhiteSpace(artifactId) &&
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

    private static string ResolveRepoPath(string repoRoot, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(repoRoot, path));
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

    private static bool ShouldSkipCanonicalMarkdown(string file)
    {
        var fileName = Path.GetFileName(file);
        return fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase);
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
