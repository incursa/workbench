namespace Workbench.Core;

/// <summary>
/// Payload describing a worktree start operation.
/// </summary>
/// <param name="Branch">Branch name used for the worktree.</param>
/// <param name="WorktreePath">Absolute path to the created or reused worktree.</param>
/// <param name="Reused">True when an existing worktree path was reused.</param>
/// <param name="CodexLaunched">True when Codex was launched for this worktree.</param>
public sealed record WorktreeStartData(
    [property: JsonPropertyName("branch")] string Branch,
    [property: JsonPropertyName("worktreePath")] string WorktreePath,
    [property: JsonPropertyName("reused")] bool Reused,
    [property: JsonPropertyName("codexLaunched")] bool CodexLaunched);
