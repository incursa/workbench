namespace Workbench.Core;

/// <summary>
/// Structured CLI error details for machine-readable failure responses.
/// </summary>
/// <param name="Code">Stable error code identifier.</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Hint">Optional recovery hint.</param>
public sealed record CliErrorData(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("hint")] string? Hint);
