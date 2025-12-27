using System.Diagnostics;

namespace Workbench;

public static class GithubService
{
    public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);
    public sealed record AuthStatus(string Status, string? Reason, string? Version);

    public static CommandResult Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo("gh")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start gh.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static AuthStatus CheckAuthStatus(string repoRoot)
    {
        try
        {
            var versionResult = Run(repoRoot, "--version");
            var version = versionResult.ExitCode == 0 ? versionResult.StdOut : null;
            if (versionResult.ExitCode != 0)
            {
                return new AuthStatus("warn", versionResult.StdErr.Length > 0 ? versionResult.StdErr : "gh --version failed.", version);
            }

            var authResult = Run(repoRoot, "auth", "status");
            if (authResult.ExitCode != 0)
            {
                var reason = authResult.StdErr.Length > 0 ? authResult.StdErr : "gh auth status failed.";
                return new AuthStatus("warn", reason, version);
            }

            return new AuthStatus("ok", null, version);
        }
        catch (Exception)
        {
#pragma warning disable ERP022
            return new AuthStatus("skip", "gh not installed or not on PATH.", null);
#pragma warning restore ERP022
        }
    }

    public static void EnsureAuthenticated(string repoRoot)
    {
        AuthStatus status;
        try
        {
            status = CheckAuthStatus(repoRoot);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"gh auth check failed: {ex}");
        }

        if (string.Equals(status.Status, "skip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("gh is not installed or not on PATH.");
        }

        if (string.Equals(status.Status, "warn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("gh is installed but not authenticated. Run `gh auth login`.");
        }
    }

    public static string CreatePullRequest(string repoRoot, string title, string body, string? baseBranch, bool draft)
    {
        EnsureAuthenticated(repoRoot);

        var args = new List<string> { "pr", "create", "--title", title, "--body", body };
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            args.Add("--base");
            args.Add(baseBranch);
        }
        if (draft)
        {
            args.Add("--draft");
        }

        var result = Run(repoRoot, args.ToArray());
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "gh pr create failed.");
        }
        return result.StdOut.Trim();
    }
}
