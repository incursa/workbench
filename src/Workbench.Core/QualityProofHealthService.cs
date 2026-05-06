using System.Text.Json;

namespace Workbench.Core;

public static class QualityProofHealthService
{
    private static readonly IReadOnlyList<string> coverageDimensions = ["positive", "negative", "edge", "fuzz"];

    public static QualityProofHealthResult Analyze(string repoRoot, QualityProofHealthOptions options)
    {
        var warnings = new List<string>();
        var contractPath = ResolvePath(repoRoot, options.ContractPath ?? QualityService.DefaultContractPath);
        var authored = QualityService.LoadAuthoredIntent(repoRoot, contractPath);
        var inventory = QualityService.DiscoverTestInventory(repoRoot, authored, "workbench quality proof-health");
        var inventoryRefs = inventory.Tests
            .Select(BuildTestReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var evidenceByRequirement = BuildEvidenceByRequirement(inventory);
        var blockedRequirementIds = LoadBlockedRequirementIds(repoRoot, options.GapPaths, warnings);
        var defaultCoverageContract = BuildDefaultCoverageContract(options.DefaultRequiredEvidenceKinds, warnings);
        var requirements = LoadRequirements(repoRoot, options.Scope, warnings);

        var classified = requirements
            .Select(requirement => ClassifyRequirement(requirement, evidenceByRequirement, inventoryRefs, blockedRequirementIds, defaultCoverageContract))
            .OrderBy(requirement => requirement.State, StringComparer.OrdinalIgnoreCase)
            .ThenBy(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var states = classified
            .GroupBy(requirement => requirement.State, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var workQueueTags = classified
            .SelectMany(requirement => requirement.WorkQueueTags)
            .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new QualityProofHealthResult(
            new QualityProofHealthData(
                new QualityProofHealthSummary(classified.Count, states, workQueueTags),
                classified,
                warnings),
            inventory);
    }

    private static QualityProofHealthRequirement ClassifyRequirement(
        RequirementProofSource requirement,
        IReadOnlyDictionary<string, RequirementEvidenceSource> evidenceByRequirement,
        IReadOnlySet<string> inventoryRefs,
        IReadOnlySet<string> blockedRequirementIds,
        IReadOnlyDictionary<string, string> defaultCoverageContract)
    {
        _ = evidenceByRequirement.TryGetValue(requirement.RequirementId, out var evidence);
        var emptyEvidence = new RequirementEvidenceSource(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>(),
            Array.Empty<string>());
        evidence ??= emptyEvidence;

        var activeCoverageContract = requirement.HasCoverageContract
            ? requirement.CoverageContract
            : defaultCoverageContract;
        var coverageContractSource = GetCoverageContractSource(requirement.HasCoverageContract, defaultCoverageContract);
        var requiredEvidence = activeCoverageContract
            .Where(entry => string.Equals(entry.Value, "required", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var observedEvidence = evidence.FocusedKinds
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingEvidence = requiredEvidence
            .Where(kind => !evidence.FocusedKinds.Contains(kind))
            .OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unresolvedSpecRefs = requirement.SpecTestRefs
            .Where(testRef => !inventoryRefs.Contains(testRef))
            .OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var workQueueTags = new List<string>();
        var notes = new List<string>();
        string state;

        if (!requirement.HasCoverageContract)
        {
            workQueueTags.Add("author_coverage_contract");
        }

        if (!requirement.HasCoverageContract && activeCoverageContract.Count == 0)
        {
            state = "missing_coverage_contract";
            notes.Add("Requirement does not declare authored coverage expectations.");
        }
        else if (evidence.FocusedTestRefs.Count == 0 && evidence.BroadTestRefs.Count == 0)
        {
            if (blockedRequirementIds.Contains(requirement.RequirementId))
            {
                state = "uncovered_blocked";
                workQueueTags.Add("resolve_blocker");
                notes.Add("No observed test evidence is linked, and a gap ledger references this requirement.");
            }
            else
            {
                state = "uncovered_unblocked";
                workQueueTags.Add("add_focused_tests");
                notes.Add("No observed test evidence is linked.");
            }
        }
        else if (evidence.FocusedTestRefs.Count == 0)
        {
            state = "covered_but_proof_too_broad";
            workQueueTags.Add("split_requirement_home");
            notes.Add("Observed evidence is linked only through broad tests.");
        }
        else if (missingEvidence.Count > 0)
        {
            state = "partially_covered";
            workQueueTags.Add("add_missing_evidence_kind");
            notes.Add("Focused evidence exists, but at least one required evidence kind is missing.");
        }
        else if (requirement.SpecTestRefs.Count == 0 || unresolvedSpecRefs.Count > 0)
        {
            state = "covered_but_missing_xrefs";
            workQueueTags.Add("sync_requirement_xrefs");
            notes.Add(requirement.SpecTestRefs.Count == 0
                ? "Focused evidence satisfies the coverage contract, but the requirement has no direct test refs."
                : "Focused evidence satisfies the coverage contract, but at least one direct test ref does not resolve to the discovered inventory.");
        }
        else
        {
            state = "trace_clean";
            notes.Add(coverageContractSource.Equals("authored", StringComparison.OrdinalIgnoreCase)
                ? "Focused evidence satisfies the authored coverage contract and direct test refs resolve."
                : "Focused evidence satisfies the selected default coverage policy and direct test refs resolve.");
        }

        if (!requirement.HasCoverageContract && activeCoverageContract.Count > 0)
        {
            notes.Add("Coverage was evaluated using the caller-selected default policy because the requirement has no authored coverage contract.");
        }

        return new QualityProofHealthRequirement(
            requirement.RequirementId,
            requirement.ArtifactId,
            requirement.ArtifactPath,
            requirement.Title,
            state,
            workQueueTags
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            new QualityProofHealthEvidence(
                requiredEvidence,
                observedEvidence,
                missingEvidence,
                coverageContractSource,
                activeCoverageContract,
                evidence.FocusedTestRefs.OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase).ToList(),
                evidence.BroadTestRefs.OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase).ToList(),
                requirement.SpecTestRefs.OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase).ToList(),
                unresolvedSpecRefs),
            notes);
    }

    private static IReadOnlyDictionary<string, string> BuildDefaultCoverageContract(
        IReadOnlyList<string> defaultRequiredEvidenceKinds,
        IList<string> warnings)
    {
        if (defaultRequiredEvidenceKinds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in defaultRequiredEvidenceKinds.Where(kind => !string.IsNullOrWhiteSpace(kind)))
        {
            var normalized = NormalizeEvidenceKind(kind);
            if (normalized is null)
            {
                warnings.Add($"Ignoring unknown default evidence kind '{kind}'. Expected one of: {string.Join(", ", coverageDimensions)}.");
                continue;
            }

            requested.Add(normalized);
        }

        if (requested.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return coverageDimensions.ToDictionary(
            dimension => dimension,
            dimension => requested.Contains(dimension) ? "required" : "optional",
            StringComparer.OrdinalIgnoreCase);
    }

    private static string GetCoverageContractSource(
        bool hasCoverageContract,
        IReadOnlyDictionary<string, string> defaultCoverageContract)
    {
        if (hasCoverageContract)
        {
            return "authored";
        }

        return defaultCoverageContract.Count > 0 ? "default_policy" : "missing";
    }

    private static IReadOnlyDictionary<string, RequirementEvidenceSource> BuildEvidenceByRequirement(TestInventory inventory)
    {
        var builders = new Dictionary<string, RequirementEvidenceBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var test in inventory.Tests)
        {
            var requirementIds = GetTraitValues(test.Traits, "Requirement");
            if (requirementIds.Count == 0)
            {
                continue;
            }

            var testRef = BuildTestReference(test);
            var kinds = ClassifyTestKinds(test);
            var focused = IsFocusedRequirementTest(test, requirementIds);
            foreach (var requirementId in requirementIds)
            {
                if (!builders.TryGetValue(requirementId, out var builder))
                {
                    builder = new RequirementEvidenceBuilder();
                    builders[requirementId] = builder;
                }

                builder.Add(testRef, kinds, focused);
            }
        }

        return builders.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Build(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ClassifyTestKinds(TestInventoryTest test)
    {
        var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in GetTraitValues(test.Traits, "Category"))
        {
            switch (NormalizeEvidenceKind(category))
            {
                case "positive":
                    kinds.Add("positive");
                    break;
                case "negative":
                    kinds.Add("negative");
                    break;
                case "edge":
                case "property":
                    kinds.Add("edge");
                    break;
                case "fuzz":
                    kinds.Add("fuzz");
                    break;
            }
        }

        var sourcePath = test.SourcePath ?? string.Empty;
        if (sourcePath.EndsWith("FuzzTests.cs", StringComparison.OrdinalIgnoreCase))
        {
            kinds.Add("fuzz");
        }

        if (sourcePath.EndsWith("PropertyTests.cs", StringComparison.OrdinalIgnoreCase))
        {
            kinds.Add("edge");
        }

        return kinds.Count == 0 ? ["unspecified"] : kinds.OrderBy(kind => kind, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? NormalizeEvidenceKind(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "positive" => "positive",
            "negative" => "negative",
            "edge" or "property" => "edge",
            "fuzz" => "fuzz",
            _ => null
        };
    }

    private static bool IsFocusedRequirementTest(TestInventoryTest test, IReadOnlyList<string> requirementIds)
    {
        return requirementIds.Count == 1
            && !string.IsNullOrWhiteSpace(test.SourcePath)
            && test.SourcePath.Contains("/RequirementHomes/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<RequirementProofSource> LoadRequirements(
        string repoRoot,
        IReadOnlyList<string> scope,
        IList<string> warnings)
    {
        var requirementsRoot = Path.Combine(repoRoot, SpecTraceLayout.RequirementsRoot);
        if (!Directory.Exists(requirementsRoot))
        {
            warnings.Add($"No requirements root found at {SpecTraceLayout.RequirementsRoot}.");
            return Array.Empty<RequirementProofSource>();
        }

        var normalizedScope = scope
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => SpecTraceLayout.NormalizePath(entry.Trim()))
            .ToList();
        var pathScope = normalizedScope.Where(IsPathScope).ToList();
        var requirements = new List<RequirementProofSource>();
        foreach (var file in Directory.EnumerateFiles(requirementsRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var repoRelativePath = NormalizeRepoPath(repoRoot, file);
            if (!SpecTraceLayout.IsSpecificationRootFile(repoRelativePath))
            {
                continue;
            }

            if (pathScope.Count > 0 && !pathScope.Any(entry => IsPathWithinScope(repoRelativePath, entry)))
            {
                continue;
            }

            var loaded = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? LoadJsonRequirements(file, repoRelativePath, warnings)
                : LoadMarkdownRequirements(file, repoRelativePath, warnings);
            requirements.AddRange(loaded.Where(requirement => IsRequirementInScope(requirement, normalizedScope)));
        }

        return requirements;
    }

    private static IReadOnlyList<RequirementProofSource> LoadJsonRequirements(
        string file,
        string repoRelativePath,
        IList<string> warnings)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var root = document.RootElement;
            var artifactId = ReadString(root, "artifact_id") ?? Path.GetFileNameWithoutExtension(file);
            if (!root.TryGetProperty("requirements", out var requirementsElement) || requirementsElement.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"{repoRelativePath}: no JSON requirements array was found.");
                return Array.Empty<RequirementProofSource>();
            }

            var requirements = new List<RequirementProofSource>();
            foreach (var requirementElement in requirementsElement.EnumerateArray())
            {
                var requirementId = ReadString(requirementElement, "id");
                if (string.IsNullOrWhiteSpace(requirementId))
                {
                    warnings.Add($"{repoRelativePath}: a JSON requirement entry is missing id.");
                    continue;
                }

                var coverageContract = ReadCoverageContract(requirementElement, out var hasCoverageContract);
                requirements.Add(new RequirementProofSource(
                    requirementId,
                    artifactId,
                    repoRelativePath,
                    ReadString(requirementElement, "title") ?? requirementId,
                    hasCoverageContract,
                    coverageContract,
                    ReadJsonTestRefs(requirementElement)));
            }

            return requirements;
        }
        catch (Exception ex)
        {
            warnings.Add($"{repoRelativePath}: failed to parse requirements for proof health: {ex}");
            return Array.Empty<RequirementProofSource>();
        }
    }

    private static IReadOnlyList<RequirementProofSource> LoadMarkdownRequirements(
        string file,
        string repoRelativePath,
        IList<string> warnings)
    {
        if (!FrontMatter.TryParse(File.ReadAllText(file), out var frontMatter, out var error))
        {
            warnings.Add($"{repoRelativePath}: failed to parse front matter for proof health: {error}");
            return Array.Empty<RequirementProofSource>();
        }

        var artifactId = ReadFrontMatterString(frontMatter!.Data, "artifact_id") ?? Path.GetFileNameWithoutExtension(file);
        var clauses = SpecTraceMarkdown.ParseRequirementClauses(frontMatter.Body, out var errors);
        foreach (var parseError in errors)
        {
            warnings.Add($"{repoRelativePath}: {parseError}");
        }

        return clauses
            .Select(clause => new RequirementProofSource(
                clause.RequirementId,
                artifactId,
                repoRelativePath,
                clause.Title,
                false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ReadMarkdownTestRefs(clause)))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> ReadCoverageContract(JsonElement requirementElement, out bool hasCoverageContract)
    {
        hasCoverageContract = requirementElement.TryGetProperty("coverage", out var coverageElement)
            && coverageElement.ValueKind == JsonValueKind.Object;
        var coverageContract = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!hasCoverageContract)
        {
            return coverageContract;
        }

        foreach (var dimension in coverageDimensions)
        {
            coverageContract[dimension] = coverageElement.TryGetProperty(dimension, out var statusElement)
                && statusElement.ValueKind == JsonValueKind.String
                    ? statusElement.GetString() ?? "optional"
                    : "optional";
        }

        return coverageContract;
    }

    private static IReadOnlyList<string> ReadJsonTestRefs(JsonElement requirementElement)
    {
        var refs = new List<string>();
        AddJsonStringList(requirementElement, "x_test_refs", refs);
        if (requirementElement.TryGetProperty("trace", out var traceElement)
            && traceElement.ValueKind == JsonValueKind.Object)
        {
            AddJsonStringList(traceElement, "x_test_refs", refs);
        }

        return refs
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ReadMarkdownTestRefs(SpecTraceMarkdown.RequirementClause clause)
    {
        if (clause.Trace is null || !clause.Trace.TryGetValue("Test Refs", out var refs))
        {
            return Array.Empty<string>();
        }

        return refs
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlySet<string> LoadBlockedRequirementIds(
        string repoRoot,
        IReadOnlyList<string> gapPaths,
        IList<string> warnings)
    {
        var files = ResolveGapFiles(repoRoot, gapPaths).ToList();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                    content,
                    @"REQ-[A-Z][A-Z0-9]*(?:-[A-Z][A-Z0-9]*)*-\d{4,}",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1)))
                {
                    ids.Add(match.Value);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{NormalizeRepoPath(repoRoot, file)}: failed to read gap ledger for proof health: {ex}");
            }
        }

        return ids;
    }

    private static IEnumerable<string> ResolveGapFiles(string repoRoot, IReadOnlyList<string> gapPaths)
    {
        if (gapPaths.Count > 0)
        {
            foreach (var gapPath in gapPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var resolved = ResolvePath(repoRoot, gapPath);
                if (File.Exists(resolved))
                {
                    yield return resolved;
                }
                else if (Directory.Exists(resolved))
                {
                    foreach (var file in Directory.EnumerateFiles(resolved, "REQUIREMENT-GAPS.md", SearchOption.AllDirectories))
                    {
                        yield return file;
                    }
                }
            }

            yield break;
        }

        var requirementsRoot = Path.Combine(repoRoot, SpecTraceLayout.RequirementsRoot);
        if (!Directory.Exists(requirementsRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(requirementsRoot, "REQUIREMENT-GAPS.md", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static bool IsRequirementInScope(RequirementProofSource requirement, IReadOnlyList<string> scope)
    {
        if (scope.Count == 0)
        {
            return true;
        }

        return scope.Any(entry =>
            string.Equals(requirement.RequirementId, entry, StringComparison.OrdinalIgnoreCase)
            || string.Equals(requirement.ArtifactId, entry, StringComparison.OrdinalIgnoreCase)
            || string.Equals(requirement.ArtifactPath, entry, StringComparison.OrdinalIgnoreCase)
            || requirement.ArtifactPath.StartsWith(entry.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPathScope(string scope)
    {
        return scope.Contains('/', StringComparison.Ordinal)
            || scope.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || scope.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathWithinScope(string repoRelativePath, string scope)
    {
        var normalizedScope = scope.TrimEnd('/');
        return string.Equals(repoRelativePath, normalizedScope, StringComparison.OrdinalIgnoreCase)
            || repoRelativePath.StartsWith(normalizedScope + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddJsonStringList(JsonElement element, string propertyName, ICollection<string> values)
    {
        if (!element.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }
    }

    private static IReadOnlyList<string> GetTraitValues(IReadOnlyDictionary<string, string[]> traits, string key)
    {
        return traits.TryGetValue(key, out var values)
            ? values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : Array.Empty<string>();
    }

    private static string BuildTestReference(TestInventoryTest test)
    {
        return string.IsNullOrWhiteSpace(test.SourcePath)
            ? test.DisplayName
            : $"{test.SourcePath}::{test.DisplayName}";
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? ReadFrontMatterString(IDictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string NormalizeRepoPath(string repoRoot, string path)
    {
        var fullRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        return SpecTraceLayout.NormalizePath(relative);
    }

    private sealed record RequirementProofSource(
        string RequirementId,
        string ArtifactId,
        string ArtifactPath,
        string Title,
        bool HasCoverageContract,
        IReadOnlyDictionary<string, string> CoverageContract,
        IReadOnlyList<string> SpecTestRefs);

    private sealed record RequirementEvidenceSource(
        IReadOnlySet<string> FocusedKinds,
        IReadOnlyList<string> FocusedTestRefs,
        IReadOnlyList<string> BroadTestRefs);

    private sealed class RequirementEvidenceBuilder
    {
        private readonly HashSet<string> focusedKinds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> focusedTestRefs = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> broadTestRefs = new(StringComparer.OrdinalIgnoreCase);

        internal void Add(string testRef, IReadOnlyList<string> kinds, bool focused)
        {
            if (focused)
            {
                this.focusedTestRefs.Add(testRef);
                foreach (var kind in kinds)
                {
                    this.focusedKinds.Add(kind);
                }

                return;
            }

            this.broadTestRefs.Add(testRef);
        }

        internal RequirementEvidenceSource Build()
        {
            return new RequirementEvidenceSource(
                this.focusedKinds,
                this.focusedTestRefs.OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase).ToList(),
                this.broadTestRefs.OrderBy(testRef => testRef, StringComparer.OrdinalIgnoreCase).ToList());
        }
    }
}
