using System.Diagnostics;

namespace Workbench.Core;

public static class CodexService
{
    public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

    private const string FullAutoFlag = "--full-auto";
    private const string WebSearchFlag = "--web-search";
    private const string PromptFlag = "--prompt";

    public static CommandResult Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo("codex")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start codex.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static bool TryGetVersion(string repoRoot, out string? version, out string? error)
    {
        version = null;
        error = null;
        try
        {
            var result = Run(repoRoot, "--version");
            if (result.ExitCode != 0)
            {
                error = result.StdErr.Length > 0 ? result.StdErr : "codex --version failed.";
                return false;
            }

            version = result.StdOut;
            return !string.IsNullOrWhiteSpace(version);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    public static void StartFullAuto(string repoRoot, string prompt)
    {
        var psi = new ProcessStartInfo("codex")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(FullAutoFlag);
        psi.ArgumentList.Add(WebSearchFlag);
        psi.ArgumentList.Add(PromptFlag);
        psi.ArgumentList.Add(prompt);

        var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start codex.");
        }
    }

    public static void StartFullAutoInTerminal(string repoRoot, string prompt)
    {
        var command = OperatingSystem.IsWindows()
            ? $"codex {FullAutoFlag} {WebSearchFlag} {PromptFlag} {EscapeForCmdArg(prompt)}"
            : $"codex {FullAutoFlag} {WebSearchFlag} {PromptFlag} {EscapeForShellArg(prompt)}";
        if (OperatingSystem.IsWindows())
        {
            StartInWindowsTerminal(repoRoot, command);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            StartInMacTerminal(command);
            return;
        }

        StartInLinuxTerminal(repoRoot, command);
    }

    private static void StartInWindowsTerminal(string repoRoot, string command)
    {
        var psi = new ProcessStartInfo("cmd")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("start");
        psi.ArgumentList.Add("cmd");
        psi.ArgumentList.Add("/k");
        psi.ArgumentList.Add(command);
        StartProcess(psi, "Failed to launch Windows terminal for codex.");
    }

    private static void StartInMacTerminal(string command)
    {
        var escaped = EscapeForAppleScript(command);
        var script = $"tell application \"Terminal\" to do script \"{escaped}\"";
        var psi = new ProcessStartInfo("osascript")
        {
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add("tell application \"Terminal\" to activate");
        StartProcess(psi, "Failed to launch macOS Terminal for codex.");
    }

    private static void StartInLinuxTerminal(string repoRoot, string command)
    {
        var bashCommand = $"cd \"{repoRoot}\" && {command}; exec bash";
        var terminals = new (string Name, string[] Args)[]
        {
            ("x-terminal-emulator", new[] { "-e", "bash", "-lc", bashCommand }),
            ("gnome-terminal", new[] { "--", "bash", "-lc", bashCommand }),
            ("konsole", new[] { "-e", "bash", "-lc", bashCommand }),
            ("xfce4-terminal", new[] { "--command", $"bash -lc \"{EscapeForDoubleQuotes(bashCommand)}\"" }),
            ("xterm", new[] { "-e", "bash", "-lc", bashCommand })
        };

        foreach (var candidate in terminals)
        {
            try
            {
                var psi = new ProcessStartInfo(candidate.Name)
                {
                    UseShellExecute = false
                };
                foreach (var arg in candidate.Args)
                {
                    psi.ArgumentList.Add(arg);
                }
                if (Process.Start(psi) is not null)
                {
                    return;
                }
            }
            catch
#pragma warning disable ERP022 // Unobserved exception in a generic exception handler
            {
                // Try next terminal.
            }
#pragma warning restore ERP022 // Unobserved exception in a generic exception handler
        }

        throw new InvalidOperationException("Failed to launch a terminal for codex. Install a terminal emulator.");
    }

    private static void StartProcess(ProcessStartInfo psi, string errorMessage)
    {
        if (Process.Start(psi) is null)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static string EscapeForShellArg(string value)
    {
        var escaped = value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
        return $"'{escaped}'";
    }

    private static string EscapeForDoubleQuotes(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeForAppleScript(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeForCmdArg(string value)
    {
        var escaped = value.Replace("\"", "^\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
