using System.Diagnostics;
using System.Linq;

namespace Workbench;

public static class GitService
{
    public sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

    public static CommandResult Run(string repoRoot, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
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

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public static bool IsClean(string repoRoot)
    {
        var result = Run(repoRoot, "status", "--porcelain");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git status failed.");
        }
        return string.IsNullOrWhiteSpace(result.StdOut);
    }

    public static void CheckoutNewBranch(string repoRoot, string branchName)
    {
        var result = Run(repoRoot, "checkout", "-b", branchName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git checkout failed.");
        }
    }

    public static bool BranchExists(string repoRoot, string branchName)
    {
        var result = Run(repoRoot, "show-ref", "--verify", "--quiet", $"refs/heads/{branchName}");
        return result.ExitCode == 0;
    }

    public static void CreateBranch(string repoRoot, string branchName)
    {
        var result = Run(repoRoot, "branch", branchName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git branch failed.");
        }
    }

    public static void Add(string repoRoot, string path)
    {
        var result = Run(repoRoot, "add", path);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git add failed.");
        }
    }

    public static string Commit(string repoRoot, string message)
    {
        var result = Run(repoRoot, "commit", "-m", message);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git commit failed.");
        }

        var sha = Run(repoRoot, "rev-parse", "HEAD");
        if (sha.ExitCode != 0)
        {
            return string.Empty;
        }
        return sha.StdOut.Trim();
    }

    public static void Push(string repoRoot, string branchName)
    {
        var result = Run(repoRoot, "push", "-u", "origin", branchName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git push failed.");
        }
    }

    public static string GetCurrentBranch(string repoRoot)
    {
        var result = Run(repoRoot, "rev-parse", "--abbrev-ref", "HEAD");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git rev-parse failed.");
        }
        return result.StdOut.Trim();
    }

    public static IList<string> GetStagedFiles(string repoRoot)
    {
        var result = Run(repoRoot, "diff", "--cached", "--name-only", "--diff-filter=ACMRT");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git diff --cached failed.");
        }
        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    public static string GetStagedDiff(string repoRoot, string path)
    {
        var result = Run(repoRoot, "diff", "--cached", "--no-color", "--unified=0", "--", path);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git diff --cached failed.");
        }
        return result.StdOut;
    }

    public static string GetWorkingDiff(string repoRoot, string path)
    {
        var result = Run(repoRoot, "diff", "--no-color", "--unified=0", "--", path);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git diff failed.");
        }
        return result.StdOut;
    }
}
