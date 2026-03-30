using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Workbench.Core;

public static partial class AttestationHtmlWriter
{
    private const int RepositoryGapSampleLimit = 10;

    private sealed record IssueBreakdown(
        int RequirementsWithIssues,
        int DownstreamMissing,
        int ImplementationMissing,
        int VerificationMissing,
        int TestEvidenceIssues,
        int CoverageEvidenceIssues,
        int BenchmarkEvidenceIssues,
        int ManualQaIssues,
        int ValidationErrors,
        int ValidationWarnings);

    private sealed record RequirementCard(
        AttestationRequirementRecord Requirement,
        int IssueCount,
        IList<string> Tags);

    private sealed record SpecificationSummary(
        string SpecificationId,
        string SpecificationTitle,
        string SpecificationRepoRelativePath,
        string SpecificationStatus,
        string PagePath,
        int TotalRequirements,
        int CleanRequirements,
        IssueBreakdown Issues,
        IList<RequirementCard> RequirementCards);

    private static string BuildGroupedSummaryHtml(string reportPath, AttestationSnapshot snapshot, string detailsLink, string jsonLink)
    {
        var builder = new StringBuilder();
        var reportRoot = Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory();
        var specifications = BuildSpecificationSummaries(reportRoot, snapshot);

        AppendDocumentStart(builder, $"Attestation Summary - {snapshot.Repository.DisplayName}");
        AppendGroupedHeader(builder, "Attestation Summary", new[]
        {
            ("index.html", "index.html"),
            ("details.html", detailsLink),
            ("attestation.json", jsonLink)
        });

        AppendRepositorySection(builder, reportPath, snapshot);
        AppendValidationSection(builder, snapshot);
        AppendCoverageSection(builder, snapshot);
        AppendWorkItemSection(builder, snapshot);
        AppendVerificationSection(builder, snapshot);
        AppendEvidenceSection(builder, snapshot);
        AppendRepositoryIssueOverviewSection(builder, snapshot, specifications);
        AppendSpecificationIndexSection(builder, reportPath, snapshot.Repository.Root, specifications, issueOnly: true, includeEvidenceColumns: false, "Specifications with issues");
        AppendGapOverviewSection(builder, snapshot, includeSamples: false);
        if (snapshot.DerivedRollups is not null)
        {
            AppendDerivedRollupSection(builder, snapshot);
        }

        AppendFooter(builder);
        return builder.ToString();
    }

    private static string BuildGroupedDetailsHtml(string reportPath, AttestationSnapshot snapshot, string summaryLink, string jsonLink)
    {
        var builder = new StringBuilder();
        var reportRoot = Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory();
        var specifications = BuildSpecificationSummaries(reportRoot, snapshot);

        AppendDocumentStart(builder, $"Attestation Details - {snapshot.Repository.DisplayName}");
        AppendGroupedHeader(builder, "Attestation Details", new[]
        {
            ("index.html", "index.html"),
            ("summary.html", summaryLink),
            ("attestation.json", jsonLink)
        });

        AppendRepositorySection(builder, reportPath, snapshot);
        AppendValidationSection(builder, snapshot);
        AppendCoverageSection(builder, snapshot);
        AppendWorkItemSection(builder, snapshot);
        AppendVerificationSection(builder, snapshot);
        AppendEvidenceSection(builder, snapshot);
        AppendRepositoryIssueOverviewSection(builder, snapshot, specifications);
        AppendSpecificationIndexSection(builder, reportPath, snapshot.Repository.Root, specifications, issueOnly: false, includeEvidenceColumns: true, "Specification breakdown");
        AppendGapOverviewSection(builder, snapshot, includeSamples: true);
        AppendFooter(builder);
        return builder.ToString();
    }

    private static void WriteSpecificationPages(string reportPath, AttestationSnapshot snapshot, string summaryLink, string jsonLink)
    {
        var reportRoot = Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory();
        var specifications = BuildSpecificationSummaries(reportRoot, snapshot);

        foreach (var specification in specifications)
        {
            var pagePath = specification.PagePath;
            Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
            File.WriteAllText(pagePath, BuildSpecificationPageHtml(pagePath, reportRoot, snapshot, specification, summaryLink, jsonLink));
        }
    }

    private static string BuildSpecificationPageHtml(
        string pagePath,
        string reportRoot,
        AttestationSnapshot snapshot,
        SpecificationSummary specification,
        string summaryLink,
        string jsonLink)
    {
        var builder = new StringBuilder();

        AppendDocumentStart(builder, $"Attestation Specification - {specification.SpecificationId} - {snapshot.Repository.DisplayName}");
        AppendGroupedHeader(builder, $"Attestation Specification - {specification.SpecificationId}", new[]
        {
            ("index.html", GetRelativeGeneratedReportHref(pagePath, Path.Combine(reportRoot, "index.html"))),
            ("details.html", GetRelativeGeneratedReportHref(pagePath, Path.Combine(reportRoot, "details.html"))),
            ("summary.html", GetRelativeGeneratedReportHref(pagePath, Path.Combine(reportRoot, summaryLink))),
            ("attestation.json", GetRelativeGeneratedReportHref(pagePath, Path.Combine(reportRoot, jsonLink)))
        });

        AppendSpecificationOverviewSection(builder, pagePath, snapshot, specification);
        AppendSpecificationIssueOverviewSection(builder, specification);
        AppendRequirementCardsSection(builder, pagePath, snapshot, specification);
        AppendFooter(builder);
        return builder.ToString();
    }

    private static void AppendGroupedHeader(StringBuilder builder, string heading, IReadOnlyList<(string Text, string Href)> links)
    {
        builder.AppendLine("<header>");
        builder.AppendLine($"<h1>{Encode(heading)}</h1>");
        builder.AppendLine("<p class=\"muted\">Derived attestation snapshot. This report is read-only and does not mutate canonical artifacts.</p>");
        builder.AppendLine("<nav class=\"nav-links\">");
        foreach (var (text, href) in links)
        {
            builder.AppendLine($"<a href=\"{Encode(href)}\">{Encode(text)}</a>");
        }
        builder.AppendLine("</nav>");
        builder.AppendLine("</header>");
    }

    private static void AppendRepositoryIssueOverviewSection(StringBuilder builder, AttestationSnapshot snapshot, IReadOnlyList<SpecificationSummary> specifications)
    {
        var breakdown = BuildIssueBreakdown(snapshot.Requirements);
        var issueBearingSpecifications = specifications.Count(specification => specification.Issues.RequirementsWithIssues > 0);

        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Issue Overview</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Requirements in snapshot", FormatInt(snapshot.Requirements.Count) },
            new[] { "Requirements with any issue", FormatInt(breakdown.RequirementsWithIssues) },
            new[] { "Clean requirements", FormatInt(snapshot.Requirements.Count - breakdown.RequirementsWithIssues) },
            new[] { "Specifications with issues", FormatInt(issueBearingSpecifications) },
            new[] { "Requirements without downstream trace", FormatInt(breakdown.DownstreamMissing) },
            new[] { "Requirements without implementation evidence", FormatInt(breakdown.ImplementationMissing) },
            new[] { "Requirements without verification coverage", FormatInt(breakdown.VerificationMissing) },
            new[] { "Requirements with test evidence issues", FormatInt(breakdown.TestEvidenceIssues) },
            new[] { "Requirements with coverage evidence issues", FormatInt(breakdown.CoverageEvidenceIssues) },
            new[] { "Requirements with benchmark evidence issues", FormatInt(breakdown.BenchmarkEvidenceIssues) },
            new[] { "Requirements with manual QA issues", FormatInt(breakdown.ManualQaIssues) },
            new[] { "Requirements with validation errors", FormatInt(breakdown.ValidationErrors) },
            new[] { "Requirements with validation warnings", FormatInt(breakdown.ValidationWarnings) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendSpecificationIndexSection(
        StringBuilder builder,
        string reportPath,
        string repoRoot,
        IReadOnlyList<SpecificationSummary> specifications,
        bool issueOnly,
        bool includeEvidenceColumns,
        string title)
    {
        var filtered = issueOnly
            ? specifications.Where(specification => specification.Issues.RequirementsWithIssues > 0).ToList()
            : specifications.ToList();

        builder.AppendLine("<section>");
        builder.AppendLine($"<h2>{Encode(title)}</h2>");

        if (filtered.Count == 0)
        {
            builder.AppendLine("<p class=\"muted\">none</p>");
            builder.AppendLine("</section>");
            return;
        }

        if (issueOnly)
        {
            var cleanSpecifications = specifications.Count - filtered.Count;
            if (cleanSpecifications > 0)
            {
                builder.AppendLine($"<p class=\"muted\">{FormatInt(cleanSpecifications)} specification(s) have no issue-bearing requirements and are omitted from this view.</p>");
            }
        }

        var headers = includeEvidenceColumns
            ? new[]
            {
                "Specification",
                "Total reqs",
                "Issue-bearing",
                "Clean",
                "Downstream",
                "Implementation",
                "Verification",
                "Test evidence",
                "Coverage evidence",
                "Benchmark evidence",
                "Manual QA",
                "Validation errors",
                "Validation warnings",
                "Page"
            }
            : new[]
            {
                "Specification",
                "Total reqs",
                "Issue-bearing",
                "Clean",
                "Downstream",
                "Implementation",
                "Verification",
                "Validation errors",
                "Validation warnings",
                "Page"
            };

        var rows = filtered
            .OrderByDescending(specification => specification.Issues.RequirementsWithIssues)
            .ThenBy(specification => specification.SpecificationId, StringComparer.OrdinalIgnoreCase)
            .Select(specification => BuildSpecificationRow(reportPath, repoRoot, specification, includeEvidenceColumns))
            .ToList();

        AppendTable(builder, headers, rows);
        builder.AppendLine("</section>");
    }

    private static IReadOnlyList<string> BuildSpecificationRow(string reportPath, string repoRoot, SpecificationSummary specification, bool includeEvidenceColumns)
    {
        var specDisplay = $"{specification.SpecificationId} - {specification.SpecificationTitle}";
        var specLink = LinkToRepoPath(reportPath, repoRoot, null, specification.SpecificationRepoRelativePath);
        var pageLink = specification.Issues.RequirementsWithIssues > 0
            ? LinkToGeneratedReportPath(reportPath, specification.PagePath, "Open")
            : "<span class=\"muted\">clean</span>";

        var row = new List<string>
        {
            $"<span class=\"spec-cell\"><span class=\"spec-name\">{Encode(specDisplay)}</span><br /><span class=\"muted\">{specLink}</span></span>",
            FormatInt(specification.TotalRequirements),
            FormatInt(specification.Issues.RequirementsWithIssues),
            FormatInt(specification.CleanRequirements),
            FormatInt(specification.Issues.DownstreamMissing),
            FormatInt(specification.Issues.ImplementationMissing),
            FormatInt(specification.Issues.VerificationMissing)
        };

        if (includeEvidenceColumns)
        {
            row.Add(FormatInt(specification.Issues.TestEvidenceIssues));
            row.Add(FormatInt(specification.Issues.CoverageEvidenceIssues));
            row.Add(FormatInt(specification.Issues.BenchmarkEvidenceIssues));
            row.Add(FormatInt(specification.Issues.ManualQaIssues));
        }

        row.Add(FormatInt(specification.Issues.ValidationErrors));
        row.Add(FormatInt(specification.Issues.ValidationWarnings));
        row.Add(pageLink);
        return row;
    }

    private static void AppendGapOverviewSection(StringBuilder builder, AttestationSnapshot snapshot, bool includeSamples)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Repository Gaps</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Requirements without downstream trace", FormatInt(snapshot.Gaps.RequirementsWithoutDownstreamTrace.Count), includeSamples ? SummarizeList(snapshot.Gaps.RequirementsWithoutDownstreamTrace) : string.Empty },
            new[] { "Requirements without implementation evidence", FormatInt(snapshot.Gaps.RequirementsWithoutImplementationEvidence.Count), includeSamples ? SummarizeList(snapshot.Gaps.RequirementsWithoutImplementationEvidence) : string.Empty },
            new[] { "Requirements without verification coverage", FormatInt(snapshot.Gaps.RequirementsWithoutVerificationCoverage.Count), includeSamples ? SummarizeList(snapshot.Gaps.RequirementsWithoutVerificationCoverage) : string.Empty },
            new[] { "Requirements with failing or stale evidence", FormatInt(snapshot.Gaps.RequirementsWithFailingOrStaleEvidence.Count), includeSamples ? SummarizeList(snapshot.Gaps.RequirementsWithFailingOrStaleEvidence) : string.Empty },
            new[] { "Orphan artifacts", FormatInt(snapshot.Gaps.OrphanArtifacts.Count), includeSamples ? SummarizeList(snapshot.Gaps.OrphanArtifacts) : string.Empty },
            new[] { "Unresolved references", FormatInt(snapshot.Gaps.UnresolvedReferences.Count), includeSamples ? SummarizeList(snapshot.Gaps.UnresolvedReferences) : string.Empty }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendSpecificationOverviewSection(StringBuilder builder, string reportPath, AttestationSnapshot snapshot, SpecificationSummary specification)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Specification Overview</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Specification", $"{specification.SpecificationId} - {specification.SpecificationTitle}" },
            new[] { "Source", LinkToRepoPath(reportPath, snapshot.Repository.Root, null, specification.SpecificationRepoRelativePath) },
            new[] { "Status", specification.SpecificationStatus },
            new[] { "Total requirements", FormatInt(specification.TotalRequirements) },
            new[] { "Issue-bearing requirements", FormatInt(specification.Issues.RequirementsWithIssues) },
            new[] { "Clean requirements", FormatInt(specification.CleanRequirements) },
            new[] { "Downstream missing", FormatInt(specification.Issues.DownstreamMissing) },
            new[] { "Implementation missing", FormatInt(specification.Issues.ImplementationMissing) },
            new[] { "Verification missing", FormatInt(specification.Issues.VerificationMissing) },
            new[] { "Validation errors", FormatInt(specification.Issues.ValidationErrors) },
            new[] { "Validation warnings", FormatInt(specification.Issues.ValidationWarnings) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendSpecificationIssueOverviewSection(StringBuilder builder, SpecificationSummary specification)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Issue Breakdown</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Requirements with any issue", FormatInt(specification.Issues.RequirementsWithIssues) },
            new[] { "Requirements without downstream trace", FormatInt(specification.Issues.DownstreamMissing) },
            new[] { "Requirements without implementation evidence", FormatInt(specification.Issues.ImplementationMissing) },
            new[] { "Requirements without verification coverage", FormatInt(specification.Issues.VerificationMissing) },
            new[] { "Requirements with test evidence issues", FormatInt(specification.Issues.TestEvidenceIssues) },
            new[] { "Requirements with coverage evidence issues", FormatInt(specification.Issues.CoverageEvidenceIssues) },
            new[] { "Requirements with benchmark evidence issues", FormatInt(specification.Issues.BenchmarkEvidenceIssues) },
            new[] { "Requirements with manual QA issues", FormatInt(specification.Issues.ManualQaIssues) },
            new[] { "Requirements with validation errors", FormatInt(specification.Issues.ValidationErrors) },
            new[] { "Requirements with validation warnings", FormatInt(specification.Issues.ValidationWarnings) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendRequirementCardsSection(StringBuilder builder, string reportPath, AttestationSnapshot snapshot, SpecificationSummary specification)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Requirements</h2>");
        builder.AppendLine($"<p class=\"muted\">Showing {FormatInt(specification.RequirementCards.Count)} issue-bearing requirement(s) of {FormatInt(specification.TotalRequirements)} total.</p>");

        if (specification.RequirementCards.Count == 0)
        {
            builder.AppendLine("<p class=\"muted\">No issue-bearing requirements were found in this specification.</p>");
            builder.AppendLine("</section>");
            return;
        }

        foreach (var card in specification.RequirementCards)
        {
            AppendRequirementCard(builder, reportPath, snapshot, specification, card);
        }

        builder.AppendLine("</section>");
    }

    private static void AppendRequirementCard(
        StringBuilder builder,
        string reportPath,
        AttestationSnapshot snapshot,
        SpecificationSummary specification,
        RequirementCard card)
    {
        var requirement = card.Requirement;
        var requirementId = requirement.RequirementId;

        builder.AppendLine($"<details class=\"requirement-card\" id=\"requirement-{Encode(requirementId)}\">");
        builder.AppendLine("<summary>");
        builder.AppendLine("<span class=\"requirement-summary\">");
        builder.AppendLine($"<span class=\"requirement-id\"><a href=\"#requirement-{Encode(requirementId)}\">{Encode(requirementId)}</a></span>");
        builder.AppendLine($"<span class=\"requirement-title\">{Encode(requirement.Title)}</span>");
        builder.AppendLine($"<span class=\"requirement-meta\">Trace {FormatInt(requirement.Trace.SatisfiedBy.Count)}/{FormatInt(requirement.Trace.ImplementedBy.Count)}/{FormatInt(requirement.Trace.VerifiedBy.Count)} · Refs {FormatInt(requirement.DirectRefs.TestRefs.Count)}/{FormatInt(requirement.DirectRefs.CodeRefs.Count)}</span>");
        builder.AppendLine($"<span class=\"requirement-tags\">{RenderTags(card.Tags)}</span>");
        builder.AppendLine("</span>");
        builder.AppendLine("</summary>");
        builder.AppendLine("<div class=\"requirement-body\">");
        AppendDefinitionList(builder, new[]
        {
            ("Source", LinkToRepoPath(reportPath, snapshot.Repository.Root, null, specification.SpecificationRepoRelativePath)),
            ("Clause", requirement.Clause),
            ("Trace", $"Satisfied {FormatInt(requirement.Trace.SatisfiedBy.Count)} · Implemented {FormatInt(requirement.Trace.ImplementedBy.Count)} · Verified {FormatInt(requirement.Trace.VerifiedBy.Count)}"),
            ("Direct refs", $"Tests {FormatInt(requirement.DirectRefs.TestRefs.Count)} · Code {FormatInt(requirement.DirectRefs.CodeRefs.Count)}"),
            ("Test refs", requirement.DirectRefs.TestRefs.Count == 0 ? "none" : string.Join(", ", requirement.DirectRefs.TestRefs)),
            ("Code refs", requirement.DirectRefs.CodeRefs.Count == 0 ? "none" : string.Join(", ", requirement.DirectRefs.CodeRefs)),
            ("Validation refs", requirement.ValidationFindingIds is null || requirement.ValidationFindingIds.Count == 0 ? "none" : string.Join(", ", requirement.ValidationFindingIds)),
            ("Evidence", $"Test {requirement.TestEvidenceStatus} · Coverage {requirement.CoverageEvidenceStatus} · Benchmark {requirement.BenchmarkEvidenceStatus} · Manual QA {requirement.ManualQaStatus}"),
            ("Issues", string.Join(", ", card.Tags))
        });

        builder.AppendLine("</div>");
        builder.AppendLine("</details>");
    }

    private static IReadOnlyList<SpecificationSummary> BuildSpecificationSummaries(string reportRoot, AttestationSnapshot snapshot)
    {
        return snapshot.Requirements
            .GroupBy(requirement => NormalizeSpecificationKey(requirement.SpecificationRepoRelativePath, requirement.SpecificationId), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var requirements = group
                    .OrderBy(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var first = requirements[0];
                var issueCards = BuildRequirementCards(requirements);
                var issueCounts = BuildIssueBreakdown(requirements);
                var pagePath = ResolveSpecificationPagePath(reportRoot, first.SpecificationRepoRelativePath, first.SpecificationId);

                return new SpecificationSummary(
                    first.SpecificationId,
                    first.SpecificationTitle,
                    first.SpecificationRepoRelativePath,
                    first.SpecificationStatus,
                    pagePath,
                    requirements.Count,
                    requirements.Count - issueCards.Count,
                    issueCounts,
                    issueCards);
            })
            .OrderByDescending(specification => specification.Issues.RequirementsWithIssues)
            .ThenBy(specification => specification.SpecificationId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IssueBreakdown BuildIssueBreakdown(IList<AttestationRequirementRecord> requirements)
    {
        var downstreamMissing = 0;
        var implementationMissing = 0;
        var verificationMissing = 0;
        var testEvidenceIssues = 0;
        var coverageEvidenceIssues = 0;
        var benchmarkEvidenceIssues = 0;
        var manualQaIssues = 0;
        var validationErrors = 0;
        var validationWarnings = 0;
        var requirementsWithIssues = 0;

        foreach (var requirement in requirements)
        {
            var hasIssue = false;

            if (HasGap(requirement.Gaps, "no downstream trace links"))
            {
                downstreamMissing++;
                hasIssue = true;
            }

            if (HasGap(requirement.Gaps, "no implementation evidence or direct refs"))
            {
                implementationMissing++;
                hasIssue = true;
            }

            if (HasGap(requirement.Gaps, "no verification coverage"))
            {
                verificationMissing++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "test evidence "))
            {
                testEvidenceIssues++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "coverage evidence "))
            {
                coverageEvidenceIssues++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "benchmark evidence "))
            {
                benchmarkEvidenceIssues++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "manual QA evidence "))
            {
                manualQaIssues++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "validation errors "))
            {
                validationErrors++;
                hasIssue = true;
            }

            if (HasGapPrefix(requirement.Gaps, "validation warnings "))
            {
                validationWarnings++;
                hasIssue = true;
            }

            if (hasIssue)
            {
                requirementsWithIssues++;
            }
        }

        return new IssueBreakdown(
            requirementsWithIssues,
            downstreamMissing,
            implementationMissing,
            verificationMissing,
            testEvidenceIssues,
            coverageEvidenceIssues,
            benchmarkEvidenceIssues,
            manualQaIssues,
            validationErrors,
            validationWarnings);
    }

    private static IList<RequirementCard> BuildRequirementCards(IList<AttestationRequirementRecord> requirements)
    {
        return requirements
            .Select(requirement =>
            {
                var tags = BuildRequirementTags(requirement);
                return new RequirementCard(requirement, tags.Count, tags);
            })
            .Where(card => card.IssueCount > 0)
            .OrderByDescending(card => card.IssueCount)
            .ThenBy(card => card.Requirement.RequirementId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IList<string> BuildRequirementTags(AttestationRequirementRecord requirement)
    {
        var tags = new List<string>();

        foreach (var gap in requirement.Gaps)
        {
            var compact = CompactGapLabel(gap);
            if (!string.IsNullOrWhiteSpace(compact))
            {
                tags.Add(compact);
            }
        }

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool HasGap(IList<string> gaps, string gap)
    {
        return gaps.Any(candidate => string.Equals(candidate, gap, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasGapPrefix(IList<string> gaps, string prefix)
    {
        return gaps.Any(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string CompactGapLabel(string gap)
    {
        if (string.Equals(gap, "no downstream trace links", StringComparison.OrdinalIgnoreCase))
        {
            return "downstream trace missing";
        }

        if (string.Equals(gap, "no implementation evidence or direct refs", StringComparison.OrdinalIgnoreCase))
        {
            return "implementation missing";
        }

        if (string.Equals(gap, "no verification coverage", StringComparison.OrdinalIgnoreCase))
        {
            return "verification missing";
        }

        return gap
            .Replace("test evidence ", "test ", StringComparison.OrdinalIgnoreCase)
            .Replace("coverage evidence ", "coverage ", StringComparison.OrdinalIgnoreCase)
            .Replace("benchmark evidence ", "benchmark ", StringComparison.OrdinalIgnoreCase)
            .Replace("manual QA evidence ", "manual QA ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSpecificationKey(string? specificationRepoRelativePath, string specificationId)
    {
        return string.IsNullOrWhiteSpace(specificationRepoRelativePath)
            ? specificationId
            : specificationRepoRelativePath;
    }

    private static string ResolveSpecificationPagePath(string reportRoot, string specificationRepoRelativePath, string specificationId)
    {
        var normalized = string.IsNullOrWhiteSpace(specificationRepoRelativePath)
            ? specificationId
            : specificationRepoRelativePath.Replace('\\', '/');

        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            normalized = Path.ChangeExtension(normalized, null) ?? normalized;
        }

        return Path.GetFullPath(Path.Combine(reportRoot, normalized, "index.html"));
    }

    private static string LinkToGeneratedReportPath(string reportPath, string targetPath, string displayText)
    {
        var relative = GetRelativeGeneratedReportHref(reportPath, targetPath);
        return $"<a href=\"{Encode(relative)}\">{Encode(displayText)}</a>";
    }

    private static string GetRelativeGeneratedReportHref(string reportPath, string targetPath)
    {
        var reportDirectory = Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory();
        return Path.GetRelativePath(reportDirectory, targetPath).Replace('\\', '/');
    }

    private static string RenderTags(IEnumerable<string> tags)
    {
        return string.Join(" ", tags.Select(tag =>
        {
            var cssClass = tag.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("stale", StringComparison.OrdinalIgnoreCase)
                ? "tag tag-warning"
                : "tag tag-error";

            return $"<span class=\"{cssClass}\">{Encode(tag)}</span>";
        }));
    }

    private static string SummarizeList(IList<string> values)
    {
        if (values.Count == 0)
        {
            return "none";
        }

        var sample = values.Take(RepositoryGapSampleLimit).ToList();
        var summary = string.Join(", ", sample);
        if (values.Count > sample.Count)
        {
            summary += $" … and {FormatInt(values.Count - sample.Count)} more";
        }

        return summary;
    }

    private static void AppendTable(StringBuilder builder, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr>");
        foreach (var header in headers)
        {
            builder.AppendLine($"<th>{Encode(header)}</th>");
        }
        builder.AppendLine("</tr></thead>");
        builder.AppendLine("<tbody>");
        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            for (var index = 0; index < headers.Count; index++)
            {
                builder.AppendLine($"<td>{RenderCell(row.ElementAtOrDefault(index))}</td>");
            }
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

}
