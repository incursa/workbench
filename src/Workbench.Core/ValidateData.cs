namespace Workbench.Core;

/// <summary>
/// Payload describing validation errors, warnings, and counts.
/// </summary>
/// <param name="Errors">Validation errors.</param>
/// <param name="Warnings">Validation warnings.</param>
/// <param name="Counts">Counts of scanned items and docs.</param>
/// <param name="Profile">Selected validation profile.</param>
/// <param name="Scope">Scoped validation prefixes.</param>
/// <param name="Findings">Structured findings.</param>
public sealed record ValidateData(
    [property: JsonPropertyName("errors")] IList<string> Errors,
    [property: JsonPropertyName("warnings")] IList<string> Warnings,
    [property: JsonPropertyName("counts")] ValidateCounts Counts,
    [property: JsonPropertyName("profile")] string? Profile = null,
    [property: JsonPropertyName("scope")] IList<string>? Scope = null,
    [property: JsonPropertyName("findings")] IList<ValidationFinding>? Findings = null);
