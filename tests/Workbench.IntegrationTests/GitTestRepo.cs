namespace Workbench.IntegrationTests;

internal static class GitTestRepo
{
    public static void InitializeGitRepo(string repoRoot)
    {
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "init"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "checkout", "-b", "main"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "config", "user.email", "workbench@example.com"));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "config", "user.name", "Workbench Tests"));

        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Temp Repo\n");
        CommitAll(repoRoot, "Initial commit");
    }

    public static void CommitAll(string repoRoot, string message)
    {
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "add", "."));
        EnsureSuccess(ProcessRunner.Run(repoRoot, "git", "commit", "-m", message));
    }

    private static void EnsureSuccess(CommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed: {result.StdErr}\n{result.StdOut}");
        }
    }
}
