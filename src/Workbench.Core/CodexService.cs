using System.Collections.Generic;
using System.Diagnostics;

namespace Workbench.Core;

public static class CodexService
{
    public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

    private const string FullAutoFlag = "--full-auto";
    private const string WebSearchFlag = "--search";
    private const int VersionTimeoutMs = 5000;
    private static readonly string[] windowsExecutableExtensions = { ".exe", ".cmd", ".bat", ".com" };

    public static CommandResult Run(string repoRoot, params string[] args)
    {
        var psi = CreateCodexProcessStartInfo(repoRoot, redirectOutput: true, args);

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
            var result = RunWithTimeout(repoRoot, VersionTimeoutMs, "--version");
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
            var diagnostic = ex.ToString();
            var firstLineEnd = diagnostic.IndexOfAny(['\r', '\n']);
            error = firstLineEnd < 0 ? diagnostic : diagnostic[..firstLineEnd];
            return false;
        }
    }

    private static CommandResult RunWithTimeout(string repoRoot, int timeoutMs, params string[] args)
    {
        var psi = CreateCodexProcessStartInfo(repoRoot, redirectOutput: true, args);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start codex.");
        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            process.WaitForExit();
            var stdoutTimeout = process.StandardOutput.ReadToEnd();
            var stderrTimeout = process.StandardError.ReadToEnd();
            var timeoutError = $"codex --version timed out after {timeoutMs}ms.";
            var timeoutStderr = string.IsNullOrWhiteSpace(stderrTimeout) ? timeoutError : $"{timeoutError} {stderrTimeout}";
            return new CommandResult(-1, stdoutTimeout.Trim(), timeoutStderr.Trim());
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static void StartFullAuto(string repoRoot, string prompt)
    {
        var psi = CreateCodexProcessStartInfo(repoRoot, redirectOutput: false, FullAutoFlag, WebSearchFlag, prompt);

        var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start codex.");
        }
    }

    public static void StartFullAutoInTerminal(string repoRoot, string prompt)
    {
        var command = OperatingSystem.IsWindows()
            ? $"codex {FullAutoFlag} {WebSearchFlag} {EscapeForCmdArg(prompt)}"
            : $"codex {FullAutoFlag} {WebSearchFlag} {EscapeForShellArg(prompt)}";
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

    private static ProcessStartInfo CreateCodexProcessStartInfo(string repoRoot, bool redirectOutput, params string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            var codexPath = FindCodexExecutable();
            if (!string.IsNullOrWhiteSpace(codexPath))
            {
                var extension = Path.GetExtension(codexPath);
                if (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateCmdProcessStartInfo(repoRoot, redirectOutput, codexPath, args);
                }

                return CreateDirectProcessStartInfo(repoRoot, redirectOutput, codexPath, args);
            }

            return CreateCmdProcessStartInfo(repoRoot, redirectOutput, "codex", args);
        }

        return CreateDirectProcessStartInfo(repoRoot, redirectOutput, "codex", args);
    }

    private static ProcessStartInfo CreateDirectProcessStartInfo(string repoRoot, bool redirectOutput, string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    private static ProcessStartInfo CreateCmdProcessStartInfo(string repoRoot, bool redirectOutput, string commandName, params string[] args)
    {
        var psi = new ProcessStartInfo("cmd")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(commandName);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        return psi;
    }

    private static string? FindCodexExecutable()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var extensions = GetWindowsExecutableExtensions();
        foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = path.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(trimmed, $"codex{extension}");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string[] GetWindowsExecutableExtensions()
    {
        var pathext = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathext))
        {
            return windowsExecutableExtensions;
        }

        var extensions = new List<string>();
        foreach (var extension in pathext.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = extension.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            extensions.Add(trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}");
        }

        if (extensions.Count == 0)
        {
            return windowsExecutableExtensions;
        }

        return extensions.ToArray();
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
