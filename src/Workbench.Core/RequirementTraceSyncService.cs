using System.Text.RegularExpressions;

namespace Workbench.Core;

internal static class RequirementTraceSyncService
{
    private static readonly Regex requirementAttributeRegex = new(
        @"^\s*\[\s*Requirement(?:Attribute)?\s*\(\s*""(?<value>[^""]+)""\s*\)\s*\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex attributeLineRegex = new(
        @"^\s*\[",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex generatedBlockStartRegex = new(
        @"^\s*///\s*<workbench-requirements\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex generatedBlockEndRegex = new(
        @"^\s*///\s*</workbench-requirements>\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex typeRegex = new(
        @"^\s*(?:public|internal|private|protected|sealed|abstract|partial|static|\s)*(?:class|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex methodRegex = new(
        @"^\s*(?:public|internal|private|protected|static|async|virtual|override|sealed|partial|\s)+[\w<>\[\],?.]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

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

    internal static IReadOnlyDictionary<string, SpecTraceMarkdown.RequirementClause> BuildRequirementCatalog(
        string repoRoot,
        out IList<string> warnings)
    {
        warnings = new List<string>();
        var requirementsRoot = Path.Combine(repoRoot, SpecTraceLayout.RequirementsRoot);
        var catalog = new Dictionary<string, SpecTraceMarkdown.RequirementClause>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(requirementsRoot))
        {
            return catalog;
        }

        foreach (var file in Directory.EnumerateFiles(requirementsRoot, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var repoRelative = SpecTraceLayout.NormalizePath(Path.GetRelativePath(repoRoot, file));
            if (!SpecTraceLayout.IsSpecificationRootFile(repoRelative))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(file);
                if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
                {
                    warnings.Add($"{repoRelative}: requirement catalog load failed: {error}");
                    continue;
                }

                var requirementClauses = SpecTraceMarkdown.ParseRequirementClauses(frontMatter!.Body, out var parseErrors);
                foreach (var parseError in parseErrors)
                {
                    warnings.Add($"{repoRelative}: {parseError}");
                }

                foreach (var clause in requirementClauses)
                {
                    if (catalog.ContainsKey(clause.RequirementId))
                    {
                        warnings.Add($"{repoRelative}: duplicate requirement_id '{clause.RequirementId}' already exists in the catalog.");
                        continue;
                    }

                    catalog[clause.RequirementId] = clause;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{repoRelative}: requirement catalog load failed: {ex}");
            }
        }

        return catalog;
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

        foreach (var file in Directory.EnumerateFiles(requirementsRoot, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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

    internal static RequirementCommentSyncResult SyncRequirementComments(
        string repoRoot,
        TestInventory inventory,
        IReadOnlyDictionary<string, SpecTraceMarkdown.RequirementClause> requirementCatalog,
        bool dryRun)
    {
        var warnings = new List<string>();
        var filesUpdated = 0;
        var requirementsUpdated = 0;

        var sourceFiles = inventory.Tests
            .Select(test => test.SourcePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizePath(repoRoot, path!))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in sourceFiles)
        {
            var repoRelative = SpecTraceLayout.NormalizePath(Path.GetRelativePath(repoRoot, file));
            try
            {
                var content = File.ReadAllText(file);
                var updatedContent = BackfillRequirementComments(
                    content,
                    requirementCatalog,
                    repoRelative,
                    warnings,
                    out var updatedRequirementCount,
                    out var changed);

                if (!changed)
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
                warnings.Add($"{repoRelative}: requirement comment backfill failed: {ex}");
            }
        }

        return new RequirementCommentSyncResult(filesUpdated, requirementsUpdated, warnings);
    }

    private static string BackfillRequirementComments(
        string body,
        IReadOnlyDictionary<string, SpecTraceMarkdown.RequirementClause> requirementCatalog,
        string repoRelativePath,
        ICollection<string> warnings,
        out int updatedRequirements,
        out bool changed)
    {
        updatedRequirements = 0;
        changed = false;
        var normalizedBody = SpecTraceMarkdown.NormalizeNewlines(body);
        var hadTerminalNewline = normalizedBody.EndsWith("\n", StringComparison.Ordinal);
        var lines = normalizedBody.Split('\n').ToList();
        var strippedLines = RemoveGeneratedRequirementCommentBlocks(lines);
        if (!lines.SequenceEqual(strippedLines, StringComparer.Ordinal))
        {
            changed = true;
        }

        lines = strippedLines;
        var regions = FindRequirementCommentRegions(lines);
        if (regions.Count == 0)
        {
            return changed ? string.Join("\n", lines) : body;
        }

        var regionsChanged = false;
        for (var index = regions.Count - 1; index >= 0; index--)
        {
            var region = regions[index];
            var regionLines = lines.GetRange(region.Start, region.End - region.Start);
            var rewritten = RewriteRequirementCommentRegion(
                regionLines,
                requirementCatalog,
                repoRelativePath,
                warnings,
                out var regionRequirementCount,
                out var regionChanged);

            if (!regionChanged)
            {
                continue;
            }

            lines.RemoveRange(region.Start, region.End - region.Start);
            lines.InsertRange(region.Start, rewritten);
            updatedRequirements += regionRequirementCount;
            regionsChanged = true;
        }

        if (!changed && !regionsChanged)
        {
            return body;
        }

        changed |= regionsChanged;
        var output = string.Join("\n", lines);
        if (hadTerminalNewline && (lines.Count == 0 || !string.IsNullOrWhiteSpace(lines[^1])))
        {
            output += "\n";
        }

        return output;
    }

    private static List<string> RewriteRequirementCommentRegion(
        IReadOnlyList<string> regionLines,
        IReadOnlyDictionary<string, SpecTraceMarkdown.RequirementClause> requirementCatalog,
        string repoRelativePath,
        ICollection<string> warnings,
        out int updatedRequirements,
        out bool changed)
    {
        updatedRequirements = 0;
        changed = false;
        if (regionLines.Count == 0)
        {
            return regionLines.ToList();
        }

        var filtered = RemoveGeneratedRequirementCommentBlocks(regionLines);
        var requirementIds = ExtractRequirementIds(filtered);
        if (requirementIds.Count == 0)
        {
            changed = !regionLines.SequenceEqual(filtered, StringComparer.Ordinal);
            return filtered;
        }

        var requirementIndex = filtered.FindIndex(line => requirementAttributeRegex.IsMatch(line));
        if (requirementIndex < 0)
        {
            changed = !regionLines.SequenceEqual(filtered, StringComparer.Ordinal);
            return filtered;
        }

        var indent = GetLeadingWhitespace(filtered[requirementIndex]);
        var block = BuildRequirementCommentBlock(requirementIds, requirementCatalog, repoRelativePath, warnings, indent);
        var rewritten = new List<string>(filtered.Count + block.Count);
        rewritten.AddRange(filtered.Take(requirementIndex));
        rewritten.AddRange(block);
        rewritten.AddRange(filtered.Skip(requirementIndex));

        changed = !regionLines.SequenceEqual(rewritten, StringComparer.Ordinal);
        if (changed)
        {
            updatedRequirements = requirementIds.Count;
        }

        return rewritten;
    }

    private static List<string> BuildRequirementCommentBlock(
        IReadOnlyList<string> requirementIds,
        IReadOnlyDictionary<string, SpecTraceMarkdown.RequirementClause> requirementCatalog,
        string repoRelativePath,
        ICollection<string> warnings,
        string indent)
    {
        var lines = new List<string>
        {
            $"{indent}/// <workbench-requirements generated=\"true\" source=\"workbench quality sync\">"
        };

        foreach (var requirementId in requirementIds)
        {
            if (!requirementCatalog.TryGetValue(requirementId, out var clause))
            {
                warnings.Add($"{repoRelativePath}: requirement '{requirementId}' was not found while backfilling test comments.");
                lines.Add($"{indent}///   <workbench-requirement requirementId=\"{XmlEscape(requirementId)}\" missing=\"true\">Requirement text not found.</workbench-requirement>");
                continue;
            }

            var requirementText = NormalizeRequirementCommentText(clause.Clause);
            if (string.IsNullOrWhiteSpace(requirementText))
            {
                requirementText = "Requirement text not found.";
            }

            lines.Add($"{indent}///   <workbench-requirement requirementId=\"{XmlEscape(requirementId)}\">{XmlEscape(requirementText)}</workbench-requirement>");
        }

        lines.Add($"{indent}/// </workbench-requirements>");
        return lines;
    }

    private static List<string> RemoveGeneratedRequirementCommentBlocks(IReadOnlyList<string> lines)
    {
        var filtered = new List<string>(lines.Count);
        var index = 0;
        while (index < lines.Count)
        {
            if (generatedBlockStartRegex.IsMatch(lines[index]))
            {
                var endIndex = index + 1;
                while (endIndex < lines.Count && !generatedBlockEndRegex.IsMatch(lines[endIndex]))
                {
                    endIndex++;
                }

                index = endIndex < lines.Count ? endIndex + 1 : lines.Count;
                continue;
            }

            filtered.Add(lines[index]);
            index++;
        }

        return filtered;
    }

    private static List<(int Start, int End)> FindRequirementCommentRegions(IReadOnlyList<string> lines)
    {
        var regions = new List<(int Start, int End)>();
        for (var index = 0; index < lines.Count; index++)
        {
            if (!IsDeclarationLine(lines[index]))
            {
                continue;
            }

            var start = index;
            while (start > 0 && IsHeaderTrivia(lines[start - 1]))
            {
                start--;
            }

            if (start == index)
            {
                continue;
            }

            if (!ContainsRequirementAttribute(lines, start, index))
            {
                continue;
            }

            regions.Add((start, index));
        }

        return regions;
    }

    private static bool ContainsRequirementAttribute(IReadOnlyList<string> lines, int start, int endExclusive)
    {
        for (var index = start; index < endExclusive; index++)
        {
            if (requirementAttributeRegex.IsMatch(lines[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHeaderTrivia(string line)
    {
        var trimmed = line.TrimStart();
        return string.IsNullOrWhiteSpace(trimmed)
            || trimmed.StartsWith("///", StringComparison.Ordinal)
            || attributeLineRegex.IsMatch(trimmed);
    }

    private static bool IsDeclarationLine(string line)
    {
        return typeRegex.IsMatch(line) || methodRegex.IsMatch(line);
    }

    private static List<string> ExtractRequirementIds(IReadOnlyList<string> lines)
    {
        var ids = new List<string>();
        foreach (var line in lines)
        {
            var match = requirementAttributeRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var requirementId = match.Groups["value"].Value.Trim();
            if (requirementId.Length == 0)
            {
                continue;
            }

            if (ids.Any(existing => string.Equals(existing, requirementId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ids.Add(requirementId);
        }

        return ids;
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

    private static string BuildTestReference(TestInventoryTest test)
    {
        return string.IsNullOrWhiteSpace(test.SourcePath)
            ? test.DisplayName
            : $"{test.SourcePath}::{test.DisplayName}";
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

    private static string GetLeadingWhitespace(string value)
    {
        var count = 0;
        while (count < value.Length && char.IsWhiteSpace(value[count]))
        {
            count++;
        }

        return count == 0 ? string.Empty : value[..count];
    }

    private static string NormalizeRequirementCommentText(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(trimmed.Length);
        var pendingWhitespace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingWhitespace = builder.Length > 0;
                continue;
            }

            if (pendingWhitespace)
            {
                builder.Append(' ');
                pendingWhitespace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string XmlEscape(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }

    private static string NormalizePath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
    }
}
