namespace Workbench.Core;

internal static class RequirementTraceSyncService
{
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildRequirementTestRefs(TestInventory inventory)
    {
        var refsByRequirement = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var test in inventory.Tests)
        {
            var requirementIds = GetTraitValues(test.Traits, "Requirement");
            if (requirementIds.Count == 0)
            {
                continue;
            }

            var testRef = BuildTestReference(test);
            foreach (var requirementId in requirementIds)
            {
                if (!refsByRequirement.TryGetValue(requirementId, out var refs))
                {
                    refs = new List<string>();
                    refsByRequirement[requirementId] = refs;
                }

                AddValue(refs, testRef);
            }
        }

        return refsByRequirement.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    internal static RequirementTraceSyncResult SyncRequirementTestRefs(
        string repoRoot,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requirementTestRefs,
        bool dryRun)
    {
        var warnings = new List<string>();
        var filesUpdated = 0;
        var requirementsUpdated = 0;
        var requirementsRoot = Path.Combine(repoRoot, SpecTraceLayout.RequirementsRoot);
        if (!Directory.Exists(requirementsRoot) || requirementTestRefs.Count == 0)
        {
            return new RequirementTraceSyncResult(filesUpdated, requirementsUpdated, warnings);
        }

        foreach (var file in Directory.EnumerateFiles(requirementsRoot, "*.md", SearchOption.AllDirectories))
        {
            var repoRelative = SpecTraceLayout.NormalizePath(Path.GetRelativePath(repoRoot, file));
            if (!SpecTraceLayout.IsSpecificationRootFile(repoRelative))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(file);
                var updatedContent = SpecTraceMarkdown.BackfillRequirementTestRefs(content, requirementTestRefs, out var updatedRequirementCount);
                if (updatedRequirementCount == 0)
                {
                    continue;
                }

                requirementsUpdated += updatedRequirementCount;
                filesUpdated++;
                if (!dryRun)
                {
                    File.WriteAllText(file, updatedContent);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{repoRelative}: requirement test ref backfill failed: {ex}");
            }
        }

        return new RequirementTraceSyncResult(filesUpdated, requirementsUpdated, warnings);
    }

    private static string BuildTestReference(TestInventoryTest test)
    {
        return string.IsNullOrWhiteSpace(test.SourcePath)
            ? test.DisplayName
            : $"{test.SourcePath}::{test.DisplayName}";
    }

    private static IReadOnlyList<string> GetTraitValues(IReadOnlyDictionary<string, string[]> traits, string key)
    {
        if (traits.TryGetValue(key, out var values))
        {
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return Array.Empty<string>();
    }

    private static void AddValue(ICollection<string> values, string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        if (values.Any(entry => string.Equals(entry, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        values.Add(normalized);
    }
}
