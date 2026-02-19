// CLI command handlers for Codex launches and git worktree setup.
// Keeps agent-oriented workflows available without opening the TUI.
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static void HandleCodexDoctor(string? repo, string format)
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var available = CodexService.TryGetVersion(repoRoot, out var version, out var error);

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new CodexDoctorOutput(
                    available,
                    new CodexDoctorData(available, version, available ? null : error));
                WriteJson(payload, WorkbenchJsonContext.Default.CodexDoctorOutput);
            }
            else
            {
                if (available)
                {
                    Console.WriteLine($"Codex available: {version}");
                }
                else
                {
                    Console.WriteLine("Codex unavailable.");
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine(error);
                    }
                }
            }
            SetExitCode(available ? 0 : 1);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static void HandleCodexRun(string? repo, string format, string prompt, bool terminal)
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            if (terminal)
            {
                CodexService.StartFullAutoInTerminal(repoRoot, prompt);
                if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new CodexRunOutput(
                        true,
                        new CodexRunData(true, true, null, null, null));
                    WriteJson(payload, WorkbenchJsonContext.Default.CodexRunOutput);
                }
                else
                {
                    Console.WriteLine("Launched Codex in a new terminal window.");
                }
                SetExitCode(0);
                return;
            }

            var result = CodexService.Run(repoRoot, "--full-auto", "--search", prompt);
            var ok = result.ExitCode == 0;
            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new CodexRunOutput(
                    ok,
                    new CodexRunData(true, false, result.ExitCode, result.StdOut, result.StdErr));
                WriteJson(payload, WorkbenchJsonContext.Default.CodexRunOutput);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(result.StdOut))
                {
                    Console.WriteLine(result.StdOut);
                }
                if (!string.IsNullOrWhiteSpace(result.StdErr))
                {
                    Console.Error.WriteLine(result.StdErr);
                }
            }

            SetExitCode(ok ? 0 : 2);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static void HandleWorktreeStart(
        string? repo,
        string format,
        string slug,
        int? ticket,
        string? baseBranch,
        string? worktreesRoot,
        string? prompt,
        bool startCodex,
        bool codexTerminal)
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var config = WorkbenchConfig.Load(repoRoot, out _);
            var normalizedSlug = WorkItemService.Slugify(slug);
            if (string.IsNullOrWhiteSpace(normalizedSlug))
            {
                Console.WriteLine("Slug must contain at least one alphanumeric character.");
                SetExitCode(2);
                return;
            }

            if (startCodex && string.IsNullOrWhiteSpace(prompt))
            {
                Console.WriteLine("`--prompt` is required when `--start-codex` is set.");
                SetExitCode(2);
                return;
            }

            var branchLeaf = ticket is null
                ? normalizedSlug
                : $"ticket-{ticket.Value.ToString(CultureInfo.InvariantCulture)}-{normalizedSlug}";
            var branchName = $"feature/{branchLeaf}";
            var resolvedBase = string.IsNullOrWhiteSpace(baseBranch)
                ? config.Git.DefaultBaseBranch
                : baseBranch;
            var root = string.IsNullOrWhiteSpace(worktreesRoot)
                ? $"{repoRoot}.worktrees"
                : Path.GetFullPath(worktreesRoot);
            var worktreePath = Path.GetFullPath(Path.Combine(root, "feature", branchLeaf));

            var reused = Directory.Exists(worktreePath);
            if (!reused)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(worktreePath) ?? root);
                GitService.CommandResult result;
                if (GitService.BranchExists(repoRoot, branchName))
                {
                    result = GitService.Run(repoRoot, "worktree", "add", worktreePath, branchName);
                }
                else
                {
                    result = GitService.Run(repoRoot, "worktree", "add", "-b", branchName, worktreePath, resolvedBase);
                }

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(result.StdErr.Length > 0 ? result.StdErr : "git worktree add failed.");
                }
            }

            var codexLaunched = false;
            if (startCodex && !string.IsNullOrWhiteSpace(prompt))
            {
                if (codexTerminal)
                {
                    CodexService.StartFullAutoInTerminal(worktreePath, prompt);
                }
                else
                {
                    CodexService.StartFullAuto(worktreePath, prompt);
                }
                codexLaunched = true;
            }

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new WorktreeStartOutput(
                    true,
                    new WorktreeStartData(branchName, worktreePath, reused, codexLaunched));
                WriteJson(payload, WorkbenchJsonContext.Default.WorktreeStartOutput);
            }
            else
            {
                Console.WriteLine($"{(reused ? "Reused" : "Created")} worktree: {worktreePath}");
                Console.WriteLine($"Branch: {branchName}");
                if (codexLaunched)
                {
                    Console.WriteLine("Codex launch requested.");
                }
            }

            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }
}
