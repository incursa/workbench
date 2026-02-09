namespace Workbench.Core;

/// <summary>
/// JSON response envelope for worktree start output.
/// </summary>
/// <param name="Ok">True when worktree setup succeeded.</param>
/// <param name="Data">Worktree and launch details.</param>
public sealed record WorktreeStartOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] WorktreeStartData Data);
