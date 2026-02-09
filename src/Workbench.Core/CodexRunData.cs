namespace Workbench.Core;

/// <summary>
/// Payload describing a Codex run invocation.
/// </summary>
/// <param name="Started">True when Codex launch succeeded.</param>
/// <param name="Terminal">True when launched in a separate terminal window.</param>
/// <param name="ExitCode">Exit code for blocking runs; null for detached terminal launches.</param>
/// <param name="StdOut">Captured standard output for blocking runs.</param>
/// <param name="StdErr">Captured standard error for blocking runs.</param>
public sealed record CodexRunData(
    [property: JsonPropertyName("started")] bool Started,
    [property: JsonPropertyName("terminal")] bool Terminal,
    [property: JsonPropertyName("exitCode")] int? ExitCode,
    [property: JsonPropertyName("stdout")] string? StdOut,
    [property: JsonPropertyName("stderr")] string? StdErr);
