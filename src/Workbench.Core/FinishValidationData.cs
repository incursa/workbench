namespace Workbench.Core;

/// <summary>
/// Validation outcome observed after local finish reconciliation.
/// </summary>
public sealed record FinishValidationData(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("errors")] IList<string> Errors,
    [property: JsonPropertyName("warnings")] IList<string> Warnings);
