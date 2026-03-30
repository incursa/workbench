using System.Text.RegularExpressions;

namespace Workbench.Core;

public static class SpecTraceMarkdown
{
    private static readonly Regex requirementHeadingRegex = new(
        @"^##\s+(?<id>REQ-[A-Z][A-Z0-9]*(?:-[A-Z][A-Z0-9]*)*-\d{4,})\s+(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex requirementKeywordRegex = new(
        @"\b(?:MUST NOT|SHALL NOT|SHOULD NOT|MUST|SHALL|SHOULD|MAY)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex requirementTraceLabelRegex = new(
        @"^(?:-\s*)?(?<label>[^:]+):\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    public static readonly IReadOnlySet<string> CanonicalRequirementTraceLabels =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Satisfied By",
            "Implemented By",
            "Verified By",
            "Derived From",
            "Supersedes",
            "Source Refs",
            "Test Refs",
            "Code Refs",
            "Related"
        };

    public static readonly IReadOnlySet<string> CanonicalWorkItemTraceLabels =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Addresses",
            "Uses Design",
            "Verified By"
        };

    public static readonly IReadOnlySet<string> CanonicalWorkItemStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "planned",
            "in_progress",
            "blocked",
            "complete",
            "cancelled",
            "superseded"
        };

    public sealed record RequirementClause(
        [property: JsonPropertyName("requirement_id")] string RequirementId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("clause")] string Clause,
        [property: JsonPropertyName("normative_keyword")] string NormativeKeyword,
        [property: JsonPropertyName("trace")] IReadOnlyDictionary<string, IReadOnlyList<string>>? Trace,
        [property: JsonPropertyName("notes")] IReadOnlyList<string>? Notes);

    public static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    public static string BuildHeading(string title, string? artifactId = null)
    {
        var normalizedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return $"# {normalizedTitle}";
        }

        return $"# {artifactId.Trim()} - {normalizedTitle}";
    }

    public static string BuildSpecificationBody(string title, string purpose, string scope, string context, string requirementBlocks, string? artifactId = null)
    {
        var blocks = new List<string>
        {
            BuildHeading(title, artifactId),
            string.Empty,
            "## Purpose",
            string.Empty,
            NormalizeBodySection(purpose),
            string.Empty,
            "## Scope",
            string.Empty,
            NormalizeBodySection(scope),
            string.Empty,
            "## Context",
            string.Empty,
            NormalizeBodySection(context)
        };

        var normalizedRequirements = NormalizeRequirementBlocks(requirementBlocks);
        if (normalizedRequirements.Count == 0)
        {
            normalizedRequirements = GetRequirementSkeletonBlocks();
        }

        blocks.Add(string.Empty);
        blocks.AddRange(normalizedRequirements);
        return JoinBlocks(blocks);
    }

    public static string BuildArchitectureBody(string title, string satisfies, string? artifactId = null)
    {
        var blocks = new List<string>
        {
            BuildHeading(title, artifactId),
            string.Empty,
            "## Purpose",
            string.Empty,
            "State how this design satisfies the named requirements.",
            string.Empty,
            "## Requirements Satisfied",
            string.Empty,
            NormalizeListOrPlaceholder(satisfies ?? string.Empty, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>")
        };

        blocks.AddRange([
            string.Empty,
            "## Design Summary",
            string.Empty,
            "Summarize the chosen design and the core mechanism that satisfies the requirement set.",
            string.Empty,
            "## Key Components",
            string.Empty,
            "- <component or concept>",
            "- <component or concept>",
            string.Empty,
            "## Data and State Considerations",
            string.Empty,
            "Describe the state, data, and ordering rules that materially affect requirement satisfaction.",
            string.Empty,
            "## Edge Cases and Constraints",
            string.Empty,
            "Call out boundary cases, failure paths, retries, or invariants that matter to the requirements.",
            string.Empty,
            "## Alternatives Considered",
            string.Empty,
            "- <alternative and reason rejected>",
            string.Empty,
            "## Risks",
            string.Empty,
            "- <risk or follow-up>",
            string.Empty,
            "## Open Questions",
            string.Empty,
            "- <question>"
        ]);

        return JoinBlocks(blocks);
    }

    public static string BuildWorkItemBody(
        string title,
        string addresses,
        string designLinks,
        string? artifactId = null,
        string? summary = null,
        string? plannedChanges = null,
        string? outOfScope = null,
        string? completionNotes = null,
        string? relatedArtifacts = null)
    {
        var summaryText = NormalizeBodySection(summary);
        if (string.IsNullOrWhiteSpace(summaryText))
        {
            summaryText = "State the implementation work in plain language.";
        }

        var plannedChangesText = NormalizeBodySection(plannedChanges);
        if (string.IsNullOrWhiteSpace(plannedChangesText))
        {
            plannedChangesText = "Describe the code, configuration, or operational changes to be made.";
        }

        var completionNotesText = NormalizeBodySection(completionNotes);
        if (string.IsNullOrWhiteSpace(completionNotesText))
        {
            completionNotesText = "Optional implementation notes, deviations, or follow-up items.";
        }

        var blocks = new List<string>
        {
            BuildHeading(title, artifactId),
            string.Empty,
            "Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.",
            string.Empty,
            "## Summary",
            string.Empty,
            summaryText,
            string.Empty,
            "## Requirements Addressed",
            string.Empty,
            NormalizeListOrPlaceholder(addresses, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty,
            "## Design Inputs",
            string.Empty,
            NormalizeListOrPlaceholder(designLinks, "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty,
            "## Planned Changes",
            string.Empty,
            plannedChangesText,
            string.Empty,
            "## Out of Scope",
            string.Empty,
            NormalizeListOrPlaceholder(outOfScope ?? string.Empty, "- <item>"),
            string.Empty,
            "## Verification Plan",
            string.Empty,
            "State how the work will be proven and link the verification artifact.",
            string.Empty,
            "## Completion Notes",
            string.Empty,
            completionNotesText,
            string.Empty,
            "## Trace Links",
            string.Empty,
            "Addresses:",
            string.Empty,
            NormalizeListOrPlaceholder(addresses, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty,
            "Uses Design:",
            string.Empty,
            NormalizeListOrPlaceholder(designLinks, "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty,
            "Verified By:",
            string.Empty,
            NormalizeListOrPlaceholder(string.Empty, "- VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty
        };

        return JoinBlocks(blocks);
    }

    public static string BuildRequirementSkeleton()
    {
        return string.Join(
            "\n",
            "## REQ-EXAMPLE-0001 Example requirement",
            "The system MUST replace this example with a concrete, testable requirement.",
            string.Empty,
            "Trace:",
            "- Satisfied By:",
            "  - ARC-EXAMPLE-0001",
            "- Implemented By:",
            "  - WI-EXAMPLE-0001",
            "- Verified By:",
            "  - VER-EXAMPLE-0001",
            "- Derived From:",
            "  - REQ-EXAMPLE-0000",
            "- Supersedes:",
            "  - REQ-EXAMPLE-0002",
            "- Source Refs:",
            "  - <external reference>",
            "- Test Refs:",
            "  - tests/Example",
            "- Code Refs:",
            "  - src/Example.cs",
            "- Related:",
            "  - SPEC-EXAMPLE",
            string.Empty,
            "Notes:",
            "- Replace this example with the real requirement before review.");
    }

    public static string BuildRequirementSection(RequirementClause requirement)
    {
        var blocks = new List<string>
        {
            $"## {requirement.RequirementId} {requirement.Title.Trim()}",
            NormalizeBodySection(requirement.Clause)
        };

        if (requirement.Trace is not null && requirement.Trace.Count > 0)
        {
            blocks.Add(string.Empty);
            blocks.Add("Trace:");
            foreach (var label in CanonicalRequirementTraceLabels)
            {
                if (!requirement.Trace.TryGetValue(label, out var values) || values.Count == 0)
                {
                    continue;
                }

                blocks.Add($"- {label}:");
                blocks.AddRange(values.Select(value => $"  - {value}"));
            }
        }

        if (requirement.Notes is not null && requirement.Notes.Count > 0)
        {
            blocks.Add(string.Empty);
            blocks.Add("Notes:");
            blocks.AddRange(requirement.Notes.Select(note => $"- {note}"));
        }

        return string.Join("\n", blocks);
    }

    public static string BackfillRequirementTestRefs(
        string body,
        IReadOnlyDictionary<string, IReadOnlyList<string>> requirementTestRefs,
        out int updatedRequirements)
    {
        updatedRequirements = 0;
        if (requirementTestRefs.Count == 0)
        {
            return body;
        }

        var normalizedBody = NormalizeNewlines(body);
        var lines = normalizedBody.Split('\n').ToList();
        var headingIndices = new List<int>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            if (requirementHeadingRegex.IsMatch(lines[lineIndex].Trim()))
            {
                headingIndices.Add(lineIndex);
            }
        }

        if (headingIndices.Count == 0)
        {
            return body;
        }

        var output = new List<string>();
        var changed = false;
        var consumed = 0;

        for (var headingCursor = 0; headingCursor < headingIndices.Count; headingCursor++)
        {
            var headingIndex = headingIndices[headingCursor];
            if (headingIndex > consumed)
            {
                output.AddRange(lines[consumed..headingIndex]);
            }

            var line = lines[headingIndex];
            var headingMatch = requirementHeadingRegex.Match(line.Trim());
            var requirementId = headingMatch.Groups["id"].Value;
            var title = headingMatch.Groups["title"].Value.Trim();
            var sectionStart = headingIndex + 1;
            var sectionEnd = sectionStart;
            var nextHeadingIndex = headingCursor + 1 < headingIndices.Count
                ? headingIndices[headingCursor + 1]
                : lines.Count;

            while (sectionEnd < nextHeadingIndex)
            {
                sectionEnd++;
            }

            var sectionLines = lines[sectionStart..sectionEnd];
            if (!TryParseRequirementSection(requirementId, title, sectionLines, out var requirement, out _))
            {
                output.AddRange(lines[headingIndex..sectionEnd]);
                consumed = sectionEnd;
                continue;
            }

            if (!requirementTestRefs.TryGetValue(requirementId, out var refs) || refs.Count == 0)
            {
                output.AddRange(lines[headingIndex..sectionEnd]);
                consumed = sectionEnd;
                continue;
            }

            var mergedTrace = MergeRequirementTrace(requirement.Trace, refs);
            if (TraceEquals(requirement.Trace, mergedTrace))
            {
                output.AddRange(lines[headingIndex..sectionEnd]);
                consumed = sectionEnd;
                continue;
            }

            var updatedRequirement = requirement with { Trace = mergedTrace };
            var rebuilt = BuildRequirementSection(updatedRequirement);
            output.AddRange(NormalizeNewlines(rebuilt).Split('\n'));
            if (sectionEnd < lines.Count)
            {
                output.Add(string.Empty);
            }

            updatedRequirements++;
            changed = true;
            consumed = sectionEnd;
        }

        if (consumed < lines.Count)
        {
            output.AddRange(lines[consumed..]);
        }

        if (changed && normalizedBody.EndsWith("\n", StringComparison.Ordinal) && (output.Count == 0 || !string.IsNullOrWhiteSpace(output[^1])))
        {
            output.Add(string.Empty);
        }

        if (!changed)
        {
            return body;
        }

        return string.Join("\n", output);
    }

    public static string BuildSpecTemplateBody()
    {
        return string.Join(
            "\n",
            BuildHeading("<Specification Title>", "SPEC-<DOMAIN>[-<GROUPING>...]"),
            string.Empty,
            "## Purpose",
            string.Empty,
            "State the purpose of the specification in one or two direct paragraphs.",
            string.Empty,
            "## Scope",
            string.Empty,
            "Optional. State what is in scope and, if useful, what is out of scope.",
            string.Empty,
            "## Context",
            string.Empty,
            "Optional. Capture the business or technical context shared by the grouped requirements.",
            string.Empty,
            BuildRequirementSkeleton(),
            string.Empty,
            "## Open Questions",
            string.Empty,
            "- <question>",
            string.Empty,
            "## Authoring Rules",
            string.Empty,
            "- Every requirement clause must contain exactly one approved normative keyword in all caps: `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`, `SHOULD NOT`, or `MAY`.",
            "- The standard uses BCP 14-style uppercase requirement language inspired by RFC 2119 and RFC 8174; only uppercase approved forms carry normative meaning, and lowercase forms are plain English.",
            "- The keyword does not need to be the first word, but it must appear in the clause and it must be the only approved keyword in that clause.",
            "- The clause should express one obligation, rule, or constraint and should usually be a single sentence.",
            "- Each specification Markdown file contains one specification and one or more related requirement clauses.",
            "- `Trace` and `Notes` are optional.",
            "- `Derived From` and `Supersedes` capture requirement lineage; `Source Refs` captures external upstream material.",
            "- The clause is the normative content. Do not bury it under a required metadata block.",
            "- If you add richer local metadata, keep it clearly optional and do not place it between the requirement heading and the clause.",
            "- Front matter describes the document as a whole, not individual requirements.",
            "- `Test Refs` and `Code Refs` stay implementation-specific. The standard does not prescribe a framework or comment syntax.");
    }

    public static string BuildArchitectureTemplateBody()
    {
        return BuildArchitectureBody(
            "<Architecture or Design Title>",
            "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
            "ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
    }

    public static string BuildWorkItemTemplateBody()
    {
        return BuildWorkItemBody(
            "<Work Item Title>",
            "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
            "ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
            artifactId: "WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
    }

    public static string BuildVerificationBody(
        string title,
        string verifies,
        string? artifactId = null)
    {
        var blocks = new List<string>
        {
            BuildHeading(title, artifactId),
            string.Empty,
            "Use one of the approved verification statuses: `planned`, `passed`, `failed`, `blocked`, `waived`, or `obsolete`.",
            string.Empty,
            "## Scope",
            string.Empty,
            "State what is being verified.",
            string.Empty,
            "## Requirements Verified",
            string.Empty,
            NormalizeListOrPlaceholder(verifies ?? string.Empty, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"),
            string.Empty,
            "## Verification Method",
            string.Empty,
            "Describe the method in tooling-agnostic terms, such as execution, inspection, analysis, or manual review.",
            string.Empty,
            "## Preconditions",
            string.Empty,
            "- <precondition>",
            string.Empty,
            "## Procedure or Approach",
            string.Empty,
            "Describe the steps or approach used to verify the requirement set.",
            string.Empty,
            "## Expected Result",
            string.Empty,
            "Describe the expected outcome in plain language.",
            string.Empty,
            "## Evidence",
            string.Empty,
            "- <test reference, code reference, or benchmark marker>",
            "- `benchmark: not-applicable` when benchmark evidence is intentionally out of scope",
            string.Empty,
            "## Status",
            string.Empty,
            "The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.",
            string.Empty,
            "planned",
            string.Empty,
            "## Related Artifacts",
            string.Empty,
            "- SPEC-<DOMAIN>[-<GROUPING>...]",
            "- ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
            "- WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>"
        };

        return JoinBlocks(blocks);
    }

    public static string BuildVerificationTemplateBody()
    {
        return BuildVerificationBody(
            "<Verification Title>",
            "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>",
            artifactId: "VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
    }

    public static string ExtractSection(string body, string heading)
    {
        var lines = NormalizeNewlines(body).Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals($"## {heading}", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }

        if (start == -1)
        {
            return string.Empty;
        }

        var collected = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            collected.Add(line);
        }

        return string.Join("\n", collected).Trim();
    }

    public static string ExtractRequirementBlocks(string body)
    {
        var lines = NormalizeNewlines(body).Split('\n');
        var blocks = new List<string>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index].TrimEnd();
            if (!requirementHeadingRegex.IsMatch(line.Trim()))
            {
                index++;
                continue;
            }

            var start = index;
            index++;
            while (index < lines.Length && !lines[index].TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                index++;
            }

            var block = string.Join("\n", lines[start..index]).Trim();
            if (!string.IsNullOrWhiteSpace(block))
            {
                blocks.Add(block);
            }
        }

        return string.Join("\n\n", blocks);
    }

    public static IReadOnlyList<RequirementClause> ParseRequirementClauses(string body, out IList<string> errors)
    {
        var parsed = new List<RequirementClause>();
        var collectedErrors = new List<string>();
        var lines = NormalizeNewlines(body).Split('\n');
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index].TrimEnd();
            var headingMatch = requirementHeadingRegex.Match(line.Trim());
            if (!headingMatch.Success)
            {
                index++;
                continue;
            }

            var requirementId = headingMatch.Groups["id"].Value;
            var title = headingMatch.Groups["title"].Value.Trim();
            var sectionStart = index + 1;
            var sectionEnd = sectionStart;
            while (sectionEnd < lines.Length && !lines[sectionEnd].TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                sectionEnd++;
            }

            var sectionLines = lines[sectionStart..sectionEnd];
            if (TryParseRequirementSection(requirementId, title, sectionLines, out var requirement, out var sectionErrors))
            {
                parsed.Add(requirement);
            }
            else
            {
                collectedErrors.AddRange(sectionErrors.Select(error => $"{requirementId}: {error}"));
            }

            index = sectionEnd;
        }

        errors = collectedErrors;
        return parsed;
    }

    public static bool TryParseRequirementSection(
        string requirementId,
        string title,
        IReadOnlyList<string> sectionLines,
        out RequirementClause requirement,
        out IList<string> errors)
    {
        requirement = new RequirementClause(requirementId, title, string.Empty, string.Empty, null, null);
        var collectedErrors = new List<string>();
        var lines = sectionLines.ToList();

        var firstContentIndex = 0;
        while (firstContentIndex < lines.Count && string.IsNullOrWhiteSpace(lines[firstContentIndex]))
        {
            firstContentIndex++;
        }

        var traceIndex = FindMarkerIndex(lines, firstContentIndex, "Trace:");
        var notesIndex = FindMarkerIndex(lines, firstContentIndex, "Notes:");

        if (traceIndex >= 0 && notesIndex >= 0 && notesIndex < traceIndex)
        {
            collectedErrors.Add("Notes must follow Trace.");
            errors = collectedErrors;
            return false;
        }

        var clauseEnd = lines.Count;
        if (traceIndex >= 0)
        {
            clauseEnd = traceIndex;
        }
        else if (notesIndex >= 0)
        {
            clauseEnd = notesIndex;
        }

        var clauseLines = TrimTrailingBlankLines(lines.Skip(firstContentIndex).Take(Math.Max(0, clauseEnd - firstContentIndex)).ToList());
        var clause = string.Join("\n", clauseLines).Trim();
        if (string.IsNullOrWhiteSpace(clause))
        {
            collectedErrors.Add("Requirement clause is empty.");
        }

        var keywordMatches = requirementKeywordRegex.Matches(clause);
        if (keywordMatches.Count != 1)
        {
            collectedErrors.Add("Requirement clause must contain exactly one approved normative keyword.");
        }

        var normativeKeyword = keywordMatches.Count == 1
            ? keywordMatches[0].Value
            : string.Empty;

        IReadOnlyDictionary<string, IReadOnlyList<string>>? trace = null;
        if (traceIndex >= 0)
        {
            var traceEnd = notesIndex >= 0 ? notesIndex : lines.Count;
            var traceLines = lines[(traceIndex + 1)..traceEnd];
            if (!TryParseTraceBlock(traceLines, out trace, out var traceErrors))
            {
                collectedErrors.AddRange(traceErrors);
            }
        }

        IReadOnlyList<string>? notes = null;
        if (notesIndex >= 0)
        {
            var notesLines = lines[(notesIndex + 1)..];
            notes = ParseLooseBulletList(notesLines);
        }

        requirement = new RequirementClause(requirementId, title, clause, normativeKeyword, trace, notes);
        errors = collectedErrors;
        return collectedErrors.Count == 0;
    }

    public static string? GetCanonicalArtifactType(string docType)
    {
        return docType.Trim().ToLowerInvariant() switch
        {
            "spec" or "specification" => "specification",
            "architecture" => "architecture",
            "work_item" or "work-item" or "work item" => "work_item",
            "verification" => "verification",
            _ => null
        };
    }

    private static int FindMarkerIndex(IReadOnlyList<string> lines, int startIndex, string marker)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(marker, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseTraceBlock(
        IReadOnlyList<string> traceLines,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> trace,
        out IList<string> errors)
    {
        var workingTrace = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var collectedErrors = new List<string>();
        string? currentLabel = null;

#pragma warning disable S3267
        foreach (var rawLine in traceLines)
#pragma warning restore S3267
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var labelMatch = requirementTraceLabelRegex.Match(line.Trim());
            if (labelMatch.Success)
            {
                var label = labelMatch.Groups["label"].Value.Trim();
                if (!CanonicalRequirementTraceLabels.Contains(label))
                {
                    collectedErrors.Add($"Trace label '{label}' is not canonical.");
                    currentLabel = null;
                    continue;
                }

                currentLabel = label;
                if (workingTrace.ContainsKey(label))
                {
                    collectedErrors.Add($"Trace label '{label}' appears more than once.");
                    continue;
                }

                workingTrace[label] = new List<string>();
                continue;
            }

            if (currentLabel is null)
            {
                collectedErrors.Add("Trace values must follow a canonical label.");
                continue;
            }

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                collectedErrors.Add($"Trace values for '{currentLabel}' must use bullet items.");
                continue;
            }

            var value = trimmed[2..].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                collectedErrors.Add($"Trace value for '{currentLabel}' is empty.");
                continue;
            }

            workingTrace[currentLabel].Add(value);
        }

        var readonlyTrace = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in workingTrace)
        {
            readonlyTrace[entry.Key] = entry.Value.AsReadOnly();
        }

        trace = readonlyTrace;
        errors = collectedErrors;
        return collectedErrors.Count == 0;
    }

    private static IReadOnlyList<string> ParseLooseBulletList(IEnumerable<string> lines)
    {
        var items = new List<string>();
        foreach (var rawLine in lines)
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

    private static string NormalizeBodySection(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static List<string> NormalizeRequirementBlocks(string requirementBlocks)
    {
        var normalized = NormalizeNewlines(requirementBlocks ?? string.Empty)
            .Replace("\t", "    ", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return [normalized];
    }

    private static List<string> GetRequirementSkeletonBlocks()
    {
        return [BuildRequirementSkeleton()];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeRequirementTrace(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? trace,
        IReadOnlyList<string> additionalTestRefs)
    {
        var merged = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (trace is not null)
        {
            foreach (var entry in trace)
            {
                merged[entry.Key] = entry.Value.ToList();
            }
        }

        if (additionalTestRefs.Count > 0)
        {
            var mergedTestRefs = new List<string>();
            if (merged.TryGetValue("Test Refs", out var existingTestRefs))
            {
                mergedTestRefs.AddRange(existingTestRefs);
            }

            foreach (var value in additionalTestRefs)
            {
                AddUniqueValue(mergedTestRefs, value);
            }

            merged["Test Refs"] = mergedTestRefs;
        }

        return merged.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.ToList().AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool TraceEquals(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? left,
        IReadOnlyDictionary<string, IReadOnlyList<string>> right)
    {
        if (left is null)
        {
            return right.Count == 0;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var entry in left)
        {
            if (!right.TryGetValue(entry.Key, out var otherValues))
            {
                return false;
            }

            if (!SequenceEquals(entry.Value, otherValues))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SequenceEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddUniqueValue(ICollection<string> values, string value)
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

    private static string NormalizeListOrPlaceholder(string text, string placeholderLine)
    {
        var normalized = NormalizeNewlines(text ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.StartsWith("-", StringComparison.Ordinal) ? line : $"- {line}")
            .ToList();

        if (normalized.Count == 0)
        {
            normalized.Add(placeholderLine);
        }

        return string.Join("\n", normalized);
    }

    private static List<string> TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string JoinBlocks(IEnumerable<string> blocks)
    {
        var lines = new List<string>();
        foreach (var block in blocks)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            var blockLines = NormalizeNewlines(block).Split('\n').ToList();
            lines.AddRange(blockLines);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines) + "\n";
    }
}
