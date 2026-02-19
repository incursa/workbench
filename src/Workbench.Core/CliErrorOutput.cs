namespace Workbench.Core;

/// <summary>
/// JSON response envelope for command failures.
/// </summary>
/// <param name="Ok">Always false for error responses.</param>
/// <param name="Error">Error details.</param>
public sealed record CliErrorOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] CliErrorData Error);
