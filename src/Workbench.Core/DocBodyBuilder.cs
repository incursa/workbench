// Builds default document bodies and enforces title headers.
// Invariants: title header is always the first line in markdown output.
namespace Workbench.Core;

public static class DocBodyBuilder
{
    public static string EnsureTitle(string body, string title, string? artifactId = null)
    {
        var normalizedBody = body?.Trim() ?? string.Empty;
        var heading = SpecTraceMarkdown.BuildHeading(title, artifactId);
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return heading + "\n";
        }

        var lines = normalizedBody.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("# ", StringComparison.Ordinal))
        {
            lines[0] = heading;
            return string.Join("\n", lines).TrimEnd() + "\n";
        }

        return $"{heading}\n\n{normalizedBody}".TrimEnd() + "\n";
    }

    public static string BuildSkeleton(string docType, string title)
    {
        var header = $"{SpecTraceMarkdown.BuildHeading(title)}\n\n";
        return docType.Trim().ToLowerInvariant() switch
        {
            "spec" or "specification" => SpecTraceMarkdown.BuildSpecificationBody(
                title,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty),
            "architecture" => SpecTraceMarkdown.BuildArchitectureBody(
                title,
                string.Empty),
            "runbook" => header + "## Purpose\n\n## Scope\n\n## Procedure\n\n## Verification\n\n## Rollback\n",
            "work_item" or "work-item" => SpecTraceMarkdown.BuildWorkItemTemplateBody(),
            "verification" => SpecTraceMarkdown.BuildVerificationTemplateBody(),
            "doc" => header + "## Summary\n\n## Scope\n\n## Context\n\n## Notes\n",
            _ => header + "## Notes\n"
        };
    }

}
