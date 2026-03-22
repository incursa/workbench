namespace Workbench.Core;

/// <summary>
/// ID configuration for work items, including numeric formatting and prefixes.
/// </summary>
public sealed record IdsConfig
{
    /// <summary>Numeric width for generated IDs (zero-padded).</summary>
    public int Width { get; init; } = 4;
}
