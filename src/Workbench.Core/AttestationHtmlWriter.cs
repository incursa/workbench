using System.Net;
using System.Text;

namespace Workbench.Core;

public static class AttestationHtmlWriter
{
    public static void WriteSummary(string path, AttestationSnapshot snapshot, string detailsLink, string jsonLink)
    {
        File.WriteAllText(path, BuildSummaryHtml(path, snapshot, detailsLink, jsonLink));
    }

    public static void WriteDetails(string path, AttestationSnapshot snapshot, string summaryLink, string jsonLink)
    {
        File.WriteAllText(path, BuildDetailsHtml(path, snapshot, summaryLink, jsonLink));
    }

    private static string BuildSummaryHtml(string reportPath, AttestationSnapshot snapshot, string detailsLink, string jsonLink)
    {
        var builder = new StringBuilder();
        AppendDocumentStart(builder, $"Attestation Summary - {snapshot.Repository.DisplayName}");
        AppendHeader(builder, "Attestation Summary", detailsLink, jsonLink);
        AppendRepositorySection(builder, reportPath, snapshot);
        AppendValidationSection(builder, snapshot);
        AppendCoverageSection(builder, snapshot);
        AppendWorkItemSection(builder, snapshot);
        AppendVerificationSection(builder, snapshot);
        AppendEvidenceSection(builder, snapshot);
        AppendGapSection(builder, snapshot);
        if (snapshot.DerivedRollups is not null)
        {
            AppendDerivedRollupSection(builder, snapshot);
        }
        AppendFooter(builder);
        return builder.ToString();
    }

    private static string BuildDetailsHtml(string reportPath, AttestationSnapshot snapshot, string summaryLink, string jsonLink)
    {
        var builder = new StringBuilder();
        AppendDocumentStart(builder, $"Attestation Details - {snapshot.Repository.DisplayName}");
        AppendHeader(builder, "Attestation Details", summaryLink, jsonLink);
        AppendRequirementOverview(builder, snapshot);
        AppendArtifactIndexes(builder, reportPath, snapshot);
        AppendValidationFindingsSection(builder, reportPath, snapshot);
        foreach (var requirement in snapshot.Requirements.OrderBy(requirement => requirement.RequirementId, StringComparer.OrdinalIgnoreCase))
        {
            AppendRequirementDetails(builder, reportPath, snapshot.Repository.Root, requirement);
        }
        AppendFooter(builder);
        return builder.ToString();
    }

    private static void AppendDocumentStart(StringBuilder builder, string title)
    {
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.AppendLine($"<title>{Encode(title)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.45;margin:0 auto;max-width:1200px;padding:1.5rem;color:#111;background:#fff;}");
        builder.AppendLine("header,section,details{margin:0 0 1.25rem 0;}");
        builder.AppendLine("table{border-collapse:collapse;width:100%;margin:0.5rem 0 1rem 0;}");
        builder.AppendLine("th,td{border:1px solid #c8c8c8;padding:0.35rem 0.5rem;vertical-align:top;text-align:left;}");
        builder.AppendLine("th{background:#f5f5f5;}");
        builder.AppendLine("details{border:1px solid #ddd;padding:0.5rem 0.75rem;background:#fafafa;}");
        builder.AppendLine("summary{font-weight:600;cursor:pointer;}");
        builder.AppendLine("summary a{color:inherit;text-decoration:none;}");
        builder.AppendLine("summary a:hover{text-decoration:underline;}");
        builder.AppendLine("code,pre{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;}");
        builder.AppendLine(".muted{color:#666;}");
        builder.AppendLine(".error{color:#8b0000;}");
        builder.AppendLine(".warning{color:#8a5a00;}");
        builder.AppendLine(".validation-findings{margin:0.25rem 0 0.75rem 1.25rem;padding:0;}");
        builder.AppendLine(".validation-findings li{margin:0.15rem 0;}");
        builder.AppendLine(".compact-links{white-space:normal;}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
    }

    private static void AppendFooter(StringBuilder builder)
    {
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
    }

    private static void AppendHeader(StringBuilder builder, string heading, string secondaryLink, string jsonLink)
    {
        builder.AppendLine("<header>");
        builder.AppendLine($"<h1>{Encode(heading)}</h1>");
        builder.AppendLine("<p class=\"muted\">Derived attestation snapshot. This report is read-only and does not mutate canonical artifacts.</p>");
        builder.AppendLine("<p>");
        builder.AppendLine($"<a href=\"{Encode(secondaryLink)}\">{Encode(secondaryLink)}</a> | <a href=\"{Encode(jsonLink)}\">{Encode(jsonLink)}</a>");
        builder.AppendLine("</p>");
        builder.AppendLine("</header>");
    }

    private static void AppendRepositorySection(StringBuilder builder, string reportPath, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Repository</h2>");
        AppendTable(builder, new[]
        {
            new[]
            {
                "Repository",
                snapshot.Repository.DisplayName
            },
            new[]
            {
                "Commit",
                snapshot.Repository.Commit ?? "unavailable"
            },
            new[]
            {
                "Branch",
                snapshot.Repository.Branch ?? "unavailable"
            },
            new[]
            {
                "Generated",
                snapshot.GeneratedAt
            },
            new[]
            {
                "Selected profile",
                snapshot.Selection.Profile
            },
            new[]
            {
                "Selected scope",
                snapshot.Selection.Scope.Count == 0 ? "entire repository" : string.Join(", ", snapshot.Selection.Scope)
            },
            new[]
            {
                "Config path",
                LinkToRepoPath(reportPath, snapshot.Repository.Root, null, snapshot.Repository.ConfigPath)
            },
            new[]
            {
                "Workbench config",
                LinkToRepoPath(reportPath, snapshot.Repository.Root, null, snapshot.Repository.WorkbenchConfigPath)
            }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendValidationSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Validation</h2>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Profile</th><th>Errors</th><th>Warnings</th></tr></thead>");
        builder.AppendLine("<tbody>");
        foreach (var profile in snapshot.Validation.Profiles.OrderBy(profile => profile.Profile, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{Encode(profile.Profile)}</td>");
            builder.AppendLine($"<td>{FormatInt(profile.Errors)}</td>");
            builder.AppendLine($"<td>{FormatInt(profile.Warnings)}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</section>");
    }

    private static void AppendCoverageSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        var coverage = snapshot.Aggregates.TraceCoverage;
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Requirement Coverage</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Requirements", FormatInt(coverage.Requirements) },
            new[] { "Satisfied By", $"{coverage.WithSatisfiedBy} ({FormatPercent(coverage.SatisfiedByPercent)})" },
            new[] { "Implemented By", $"{coverage.WithImplementedBy} ({FormatPercent(coverage.ImplementedByPercent)})" },
            new[] { "Verified By", $"{coverage.WithVerifiedBy} ({FormatPercent(coverage.VerifiedByPercent)})" },
            new[] { "Test Refs", $"{coverage.WithTestRefs} ({FormatPercent(coverage.TestRefsPercent)})" },
            new[] { "Code Refs", $"{coverage.WithCodeRefs} ({FormatPercent(coverage.CodeRefsPercent)})" },
            new[] { "Any downstream trace", $"{coverage.WithDownstreamTrace} ({FormatPercent(coverage.DownstreamTracePercent)})" }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendWorkItemSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        var workItems = snapshot.Aggregates.WorkItemStatuses;
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Work Items</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Linked work items", FormatInt(workItems.TotalArtifacts) },
            new[] { "Requirements with work items", FormatInt(workItems.LinkedRequirementCount) },
            new[] { "Done", FormatInt(workItems.Done) },
            new[] { "In progress", FormatInt(workItems.InProgress) },
            new[] { "Open", FormatInt(workItems.Open) },
            new[] { "Blocked", FormatInt(workItems.Blocked) },
            new[] { "Unknown", FormatInt(workItems.Unknown) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendVerificationSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        var verifications = snapshot.Aggregates.VerificationStatuses;
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Verification Artifacts</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Linked verification artifacts", FormatInt(verifications.TotalArtifacts) },
            new[] { "Requirements with verification artifacts", FormatInt(verifications.LinkedRequirementCount) },
            new[] { "Passing", FormatInt(verifications.Passing) },
            new[] { "Failing", FormatInt(verifications.Failing) },
            new[] { "Pending", FormatInt(verifications.Pending) },
            new[] { "Stale", FormatInt(verifications.Stale) },
            new[] { "Unknown", FormatInt(verifications.Unknown) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendEvidenceSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Evidence</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Quality report", DescribeQualityReport(snapshot.Evidence.QualityReport) },
            new[] { "Test results", DescribeTestResults(snapshot.Evidence.TestResults) },
            new[] { "Coverage", DescribeCoverage(snapshot.Evidence.Coverage) },
            new[] { "Benchmarks", DescribeSimpleEvidence(snapshot.Evidence.Benchmarks) },
            new[] { "Manual QA", DescribeSimpleEvidence(snapshot.Evidence.ManualQa) },
            new[] { "Execution", DescribeExecution(snapshot.Evidence.Execution) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendGapSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Gaps</h2>");
        AppendList(builder, snapshot.Gaps.RequirementsWithoutDownstreamTrace, "Requirements without downstream trace");
        AppendList(builder, snapshot.Gaps.RequirementsWithoutImplementationEvidence, "Requirements without implementation evidence");
        AppendList(builder, snapshot.Gaps.RequirementsWithoutVerificationCoverage, "Requirements without verification coverage");
        AppendList(builder, snapshot.Gaps.RequirementsWithFailingOrStaleEvidence, "Requirements with failing or stale evidence");
        AppendList(builder, snapshot.Gaps.OrphanArtifacts, "Orphan artifacts");
        AppendList(builder, snapshot.Gaps.UnresolvedReferences, "Unresolved references");
        builder.AppendLine("</section>");
    }

    private static void AppendDerivedRollupSection(StringBuilder builder, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Derived Rollups</h2>");
        AppendTable(builder, new[]
        {
            new[] { "Implemented enabled", FormatBool(snapshot.DerivedRollups!.ImplementedEnabled) },
            new[] { "Verified enabled", FormatBool(snapshot.DerivedRollups.VerifiedEnabled) },
            new[] { "Release-ready enabled", FormatBool(snapshot.DerivedRollups.ReleaseReadyEnabled) },
            new[] { "Implemented rule", snapshot.DerivedRollups.ImplementedRule ?? "n/a" },
            new[] { "Verified rule", snapshot.DerivedRollups.VerifiedRule ?? "n/a" },
            new[] { "Release-ready rule", snapshot.DerivedRollups.ReleaseReadyRule ?? "n/a" },
            new[] { "Implemented requirements", FormatInt(snapshot.DerivedRollups.ImplementedRequirements) },
            new[] { "Verified requirements", FormatInt(snapshot.DerivedRollups.VerifiedRequirements) },
            new[] { "Release-ready requirements", FormatInt(snapshot.DerivedRollups.ReleaseReadyRequirements) }
        });
        builder.AppendLine("</section>");
    }

    private static void AppendRequirementOverview(StringBuilder builder, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Requirements</h2>");
        builder.AppendLine($"<p>{FormatInt(snapshot.Requirements.Count)} requirement(s), {FormatInt(snapshot.Validation.Findings.Count)} grouped validation finding(s).</p>");
        builder.AppendLine("</section>");
    }

    private static void AppendRequirementDetails(
        StringBuilder builder,
        string reportPath,
        string repoRoot,
        AttestationRequirementRecord requirement)
    {
        builder.AppendLine($"<details id=\"{Encode(BuildRequirementAnchor(requirement.RequirementId))}\">");
        builder.AppendLine($"<summary>{LinkToRepoPath(reportPath, repoRoot, null, requirement.SpecificationRepoRelativePath, $"{requirement.RequirementId} {requirement.Title}")}</summary>");
        builder.AppendLine("<div>");
        AppendDefinitionList(builder, new[]
        {
            ("Clause", requirement.Clause),
            ("Specification", $"{requirement.SpecificationId} - {requirement.SpecificationTitle}"),
            ("Specification status", requirement.SpecificationStatus),
            ("Test refs", RenderRepoPathLinkList(reportPath, repoRoot, (IReadOnlyList<string>)requirement.DirectRefs.TestRefs)),
            ("Code refs", RenderRepoPathLinkList(reportPath, repoRoot, (IReadOnlyList<string>)requirement.DirectRefs.CodeRefs)),
            ("Validation refs", RenderLinkList((IReadOnlyList<string>)(requirement.ValidationFindingIds ?? Array.Empty<string>()), value => LinkToAnchor(BuildFindingAnchor(value), value))),
            ("Test evidence", requirement.TestEvidenceStatus),
            ("Coverage evidence", requirement.CoverageEvidenceStatus),
            ("Benchmark evidence", requirement.BenchmarkEvidenceStatus),
            ("Manual QA", requirement.ManualQaStatus)
        });

        AppendRequirementTrace(builder, "Downstream trace", requirement.Trace.SatisfiedBy, requirement.Trace.ImplementedBy, requirement.Trace.VerifiedBy);
        AppendList(builder, requirement.Gaps, "Gaps");

        if (requirement.DerivedRollups is not null)
        {
            AppendDefinitionList(builder, new[]
            {
                ("Implemented", FormatNullableBool(requirement.DerivedRollups.Implemented)),
                ("Verified", FormatNullableBool(requirement.DerivedRollups.Verified)),
                ("Release-ready", FormatNullableBool(requirement.DerivedRollups.ReleaseReady)),
                ("Rule", requirement.DerivedRollups.Rule ?? "n/a")
            });
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</details>");
    }

    private static void AppendRequirementTrace(StringBuilder builder, string title, IList<string> satisfiedBy, IList<string> implementedBy, IList<string> verifiedBy)
    {
        builder.AppendLine($"<section><h3>{Encode(title)}</h3>");
        builder.AppendLine("<dl>");
        builder.AppendLine($"<dt>Satisfied By</dt><dd>{RenderLinkList((IReadOnlyList<string>)satisfiedBy, value => LinkToAnchor(BuildArtifactAnchor(value), value))}</dd>");
        builder.AppendLine($"<dt>Implemented By</dt><dd>{RenderLinkList((IReadOnlyList<string>)implementedBy, value => LinkToAnchor(BuildArtifactAnchor(value), value))}</dd>");
        builder.AppendLine($"<dt>Verified By</dt><dd>{RenderLinkList((IReadOnlyList<string>)verifiedBy, value => LinkToAnchor(BuildArtifactAnchor(value), value))}</dd>");
        builder.AppendLine("</dl>");
        builder.AppendLine("</section>");
    }

    private static void AppendArtifactIndexes(StringBuilder builder, string reportPath, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Artifacts</h2>");
        AppendArtifactSection(builder, reportPath, snapshot.Repository.Root, "Architectures", snapshot.Artifacts.Architectures);
        AppendArtifactSection(builder, reportPath, snapshot.Repository.Root, "Work Items", snapshot.Artifacts.WorkItems);
        AppendArtifactSection(builder, reportPath, snapshot.Repository.Root, "Verifications", snapshot.Artifacts.Verifications);
        builder.AppendLine("</section>");
    }

    private static void AppendArtifactSection(
        StringBuilder builder,
        string reportPath,
        string repoRoot,
        string title,
        IList<AttestationArtifactSummary> artifacts)
    {
        builder.AppendLine($"<section><h3>{Encode(title)}</h3>");
        if (artifacts.Count == 0)
        {
            builder.AppendLine("<p class=\"muted\">none</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>ID</th><th>Title</th><th>Status</th><th>Path</th><th>Requirements</th></tr></thead>");
        builder.AppendLine("<tbody>");
        foreach (var artifact in artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<tr id=\"{Encode(BuildArtifactAnchor(artifact.ArtifactId))}\">");
            builder.AppendLine($"<td><a href=\"#{Encode(BuildArtifactAnchor(artifact.ArtifactId))}\">{Encode(artifact.ArtifactId)}</a></td>");
            builder.AppendLine($"<td>{Encode(artifact.Title)}</td>");
            builder.AppendLine($"<td>{Encode(artifact.Status)}</td>");
            builder.AppendLine($"<td>{LinkToRepoPath(reportPath, repoRoot, null, artifact.RepoRelativePath)}</td>");
            builder.AppendLine($"<td class=\"compact-links\">{RenderLinkList((IReadOnlyList<string>)(artifact.RequirementIds ?? Array.Empty<string>()), value => LinkToAnchor(BuildRequirementAnchor(value), value))}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</section>");
    }

    private static void AppendValidationFindingsSection(StringBuilder builder, string reportPath, AttestationSnapshot snapshot)
    {
        builder.AppendLine("<section>");
        builder.AppendLine("<h2>Validation Findings</h2>");
        var findings = snapshot.Validation.Findings;
        if (findings.Count == 0)
        {
            builder.AppendLine("<p class=\"muted\">none</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>ID</th><th>Severity</th><th>Category</th><th>Location</th><th>Requirements</th><th>Message</th></tr></thead>");
        builder.AppendLine("<tbody>");
        foreach (var finding in findings.OrderBy(finding => finding.FindingId, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<tr id=\"{Encode(BuildFindingAnchor(finding.FindingId))}\">");
            builder.AppendLine($"<td><a href=\"#{Encode(BuildFindingAnchor(finding.FindingId))}\">{Encode(finding.FindingId)}</a></td>");
            builder.AppendLine($"<td><span class=\"{Encode(finding.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "warning" : "error")}\">[{Encode(finding.Severity)}]</span></td>");
            builder.AppendLine($"<td>{Encode(finding.Category)}</td>");
            builder.AppendLine($"<td>{RenderFindingLocation(reportPath, snapshot.Repository.Root, finding)}</td>");
            builder.AppendLine($"<td class=\"compact-links\">{RenderLinkList((IReadOnlyList<string>)(finding.RequirementIds ?? Array.Empty<string>()), value => LinkToAnchor(BuildRequirementAnchor(value), value))}</td>");
            builder.AppendLine($"<td>{Encode(finding.Message)}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</section>");
    }

    private static void AppendList(StringBuilder builder, IList<string> values, string label)
    {
        builder.AppendLine($"<section><h3>{Encode(label)}</h3>");
        if (values.Count == 0)
        {
            builder.AppendLine("<p class=\"muted\">none</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<ul>");
        foreach (var value in values)
        {
            builder.AppendLine($"<li>{Encode(value)}</li>");
        }
        builder.AppendLine("</ul>");
        builder.AppendLine("</section>");
    }

    private static string RenderLinkList(IReadOnlyList<string> values, Func<string, string> linkFactory)
    {
        if (values.Count == 0)
        {
            return "<span class=\"muted\">none</span>";
        }

        var links = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => linkFactory(value.Trim()))
            .ToList();

        if (links.Count == 0)
        {
            return "<span class=\"muted\">none</span>";
        }

        return string.Join(", ", links);
    }

    private static string RenderRepoPathLinkList(string reportPath, string repoRoot, IReadOnlyList<string> values)
    {
        return RenderLinkList(values, value => LinkToRepoPath(reportPath, repoRoot, null, value));
    }

    private static string LinkToAnchor(string anchor, string label)
    {
        return $"<a href=\"#{Encode(anchor)}\">{Encode(label)}</a>";
    }

    private static string BuildRequirementAnchor(string requirementId)
    {
        return $"requirement-{NormalizeAnchor(requirementId)}";
    }

    private static string BuildArtifactAnchor(string artifactId)
    {
        return $"artifact-{NormalizeAnchor(artifactId)}";
    }

    private static string BuildFindingAnchor(string findingId)
    {
        return $"finding-{NormalizeAnchor(findingId)}";
    }

    private static string NormalizeAnchor(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "item"
            : value.Trim().Replace(' ', '-').Replace('_', '-');
    }

    private static string RenderFindingLocation(string reportPath, string repoRoot, AttestationValidationFindingSummary finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.RepoRelativePath))
        {
            return LinkToRepoPath(reportPath, repoRoot, null, finding.RepoRelativePath);
        }

        if (!string.IsNullOrWhiteSpace(finding.TargetFile))
        {
            return LinkToRepoPath(reportPath, repoRoot, null, finding.TargetFile);
        }

        if (!string.IsNullOrWhiteSpace(finding.ArtifactId))
        {
            return $"<code>{Encode(finding.ArtifactId)}</code>";
        }

        if (!string.IsNullOrWhiteSpace(finding.TargetId))
        {
            return $"<code>{Encode(finding.TargetId)}</code>";
        }

        return "<span class=\"muted\">unavailable</span>";
    }

    private static void AppendDefinitionList(StringBuilder builder, IEnumerable<(string Label, string Value)> items)
    {
        builder.AppendLine("<dl>");
        foreach (var (label, value) in items)
        {
            builder.AppendLine($"<dt>{Encode(label)}</dt>");
            builder.AppendLine($"<dd>{RenderCell(value)}</dd>");
        }
        builder.AppendLine("</dl>");
    }

    private static void AppendTable(StringBuilder builder, IEnumerable<IEnumerable<string>> rows)
    {
        var materialized = rows.Select(row => row.ToList()).ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        var first = materialized[0];
        if (first.Count == 2 && string.Equals(first[0], "Profile", StringComparison.Ordinal))
        {
            builder.AppendLine("<table>");
            builder.AppendLine("<thead><tr><th>Profile</th><th>Errors</th><th>Warnings</th><th>Findings</th></tr></thead>");
            builder.AppendLine("<tbody>");
            foreach (var row in materialized.Skip(1))
            {
                builder.AppendLine("<tr>");
                builder.AppendLine($"<td>{RenderCell(row.ElementAtOrDefault(0))}</td>");
                builder.AppendLine($"<td>{RenderCell(row.ElementAtOrDefault(1))}</td>");
                builder.AppendLine($"<td>{RenderCell(row.ElementAtOrDefault(2))}</td>");
                builder.AppendLine($"<td>{RenderCell(row.ElementAtOrDefault(3))}</td>");
                builder.AppendLine("</tr>");
            }
            builder.AppendLine("</tbody></table>");
            return;
        }

        builder.AppendLine("<table>");
        builder.AppendLine("<tbody>");
        foreach (var row in materialized)
        {
            if (row.Count == 0)
            {
                continue;
            }

            builder.AppendLine("<tr>");
            builder.AppendLine($"<th>{Encode(row[0])}</th>");
            builder.AppendLine($"<td>{RenderCell(row.Count > 1 ? row[1] : string.Empty)}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static string DescribeQualityReport(AttestationQualityReportEvidenceSummary qualityReport)
    {
        if (!qualityReport.Present)
        {
            return "missing";
        }

        return $"{qualityReport.Status ?? "unknown"} / {qualityReport.ConfidenceVerdict ?? "n/a"}";
    }

    private static string DescribeTestResults(AttestationTestEvidenceSummary testResults)
    {
        if (!testResults.Present)
        {
            return "missing";
        }

        return $"{testResults.Status ?? "unknown"} ({FormatInt(testResults.Passed ?? 0)} passed, {FormatInt(testResults.Failed ?? 0)} failed, {FormatInt(testResults.Skipped ?? 0)} skipped)";
    }

    private static string DescribeCoverage(AttestationCoverageEvidenceSummary coverage)
    {
        if (!coverage.Present)
        {
            return "missing";
        }

        return $"{coverage.Status ?? "unknown"} (line {FormatPercent(coverage.LineRate ?? 0)}, branch {FormatPercent(coverage.BranchRate ?? 0)})";
    }

    private static string DescribeSimpleEvidence(AttestationSimpleEvidenceSummary evidence)
    {
        return evidence.Present
            ? $"{evidence.Status ?? "unknown"} ({FormatInt(evidence.Paths.Count)} file(s))"
            : "missing";
    }

    private static string DescribeExecution(AttestationExecutionSummary execution)
    {
        if (!execution.Requested)
        {
            return "not requested";
        }

        return execution.Commands.Count == 0
            ? "requested, no commands configured"
            : $"{FormatInt(execution.Commands.Count)} command(s) executed";
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatInt(int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string FormatNullableBool(bool? value)
    {
        return value.HasValue ? FormatBool(value.Value) : "n/a";
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string RenderCell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.StartsWith("<a ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("<span ", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return Encode(text);
    }

    private static string LinkToRepoPath(string reportPath, string repoRoot, string? absolutePath, string? repoRelativePath, string? displayText = null)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) && string.IsNullOrWhiteSpace(repoRelativePath))
        {
            return "<span class=\"muted\">unavailable</span>";
        }

        var targetPath = !string.IsNullOrWhiteSpace(absolutePath)
            ? absolutePath!
            : Path.Combine(repoRoot, repoRelativePath!.Replace('/', Path.DirectorySeparatorChar));

        string linkText;
        if (!string.IsNullOrWhiteSpace(displayText))
        {
            linkText = displayText!;
        }
        else if (!string.IsNullOrWhiteSpace(repoRelativePath))
        {
            linkText = repoRelativePath!;
        }
        else
        {
            linkText = targetPath;
        }

        var reportDirectory = Path.GetDirectoryName(reportPath) ?? Directory.GetCurrentDirectory();
        var relative = Path.GetRelativePath(reportDirectory, targetPath).Replace('\\', '/');
        return $"<a href=\"{Encode(relative)}\">{Encode(linkText)}</a>";
    }

}
