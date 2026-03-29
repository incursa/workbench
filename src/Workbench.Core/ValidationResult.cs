namespace Workbench.Core;

public sealed class ValidationResult
{
    public string Profile { get; set; } = ValidationProfiles.Core;
    public IList<string> Scope { get; } = new List<string>();
    public IList<ValidationFinding> Findings { get; } = new List<ValidationFinding>();
    public IList<string> Errors { get; } = new List<string>();
    public IList<string> Warnings { get; } = new List<string>();
    public int WorkItemCount { get; set; }
    public int MarkdownFileCount { get; set; }

    public void AddError(
        string profile,
        string category,
        string message,
        string? file = null,
        string? artifactId = null,
        string? field = null,
        string? targetId = null,
        string? targetType = null,
        string? targetFile = null)
    {
        AddFinding(profile, category, "error", message, file, artifactId, field, targetId, targetType, targetFile);
    }

    public void AddWarning(
        string profile,
        string category,
        string message,
        string? file = null,
        string? artifactId = null,
        string? field = null,
        string? targetId = null,
        string? targetType = null,
        string? targetFile = null)
    {
        AddFinding(profile, category, "warning", message, file, artifactId, field, targetId, targetType, targetFile);
    }

    public void AddFinding(
        string profile,
        string category,
        string severity,
        string message,
        string? file = null,
        string? artifactId = null,
        string? field = null,
        string? targetId = null,
        string? targetType = null,
        string? targetFile = null)
    {
        var normalizedProfile = ValidationProfiles.NormalizeOrDefault(profile, ValidationProfiles.RepoState);
        var normalizedSeverity = string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase)
            ? "warning"
            : "error";
        var finding = new ValidationFinding(
            normalizedProfile,
            category,
            normalizedSeverity,
            message,
            file,
            artifactId,
            field,
            targetId,
            targetType,
            targetFile);
        Findings.Add(finding);
        var formatted = FormatFinding(finding);
        if (string.Equals(normalizedSeverity, "warning", StringComparison.OrdinalIgnoreCase))
        {
            Warnings.Add(formatted);
        }
        else
        {
            Errors.Add(formatted);
        }
    }

    private static string FormatFinding(ValidationFinding finding)
    {
        var location = string.Empty;
        if (!string.IsNullOrWhiteSpace(finding.File))
        {
            location = finding.File;
        }
        else if (!string.IsNullOrWhiteSpace(finding.ArtifactId))
        {
            location = finding.ArtifactId;
        }

        var prefix = $"[{finding.Profile}/{finding.Category}]";
        if (string.IsNullOrWhiteSpace(location))
        {
            return $"{prefix} {finding.Message}";
        }

        return $"{prefix} {location}: {finding.Message}";
    }
}
