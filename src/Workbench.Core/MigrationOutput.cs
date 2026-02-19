namespace Workbench.Core;

/// <summary>
/// JSON response envelope for migration output.
/// </summary>
/// <param name="Ok">True when migration completed without fatal errors.</param>
/// <param name="Data">Migration summary data.</param>
public sealed record MigrationOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] MigrationData Data);
