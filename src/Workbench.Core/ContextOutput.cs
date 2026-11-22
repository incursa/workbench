namespace Workbench.Core;

/// <summary>JSON response envelope for repository context.</summary>
public sealed record ContextOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] ContextData Data);
