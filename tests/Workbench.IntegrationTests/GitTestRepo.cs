namespace Workbench.IntegrationTests;

internal static class GitTestRepo
{
    public static void InitializeGitRepo(string repoRoot)
    {
        EnsureHermeticGitLayout(repoRoot);
        EnsureSuccess(repoRoot, RunGit(repoRoot, "init"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "checkout", "-b", "main"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "user.email", "workbench@example.com"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "user.name", "Workbench Tests"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "user.useConfigOnly", "true"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "commit.gpgSign", "false"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "tag.gpgSign", "false"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "config", "--local", "core.hooksPath", GetHooksPath(repoRoot)));

        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Temp Repo\n");
        CommitAll(repoRoot, "Initial commit");
    }

    public static void CommitAll(string repoRoot, string message)
    {
        EnsureSuccess(repoRoot, RunGit(repoRoot, "add", "--all"));
        EnsureSuccess(repoRoot, RunGit(repoRoot, "commit", "--no-gpg-sign", "--no-verify", "-m", message));
    }

    public static CommandResult RunGit(string repoRoot, params string[] args)
    {
        EnsureHermeticGitLayout(repoRoot);
        var gitArgs = new List<string>
        {
            "-c", $"core.hooksPath={GetHooksPath(repoRoot)}",
            "-c", "commit.gpgSign=false",
            "-c", "tag.gpgSign=false",
            "-c", "user.useConfigOnly=true",
            "-c", "advice.detachedHead=false"
        };
        gitArgs.AddRange(args);

        return ProcessRunner.Run(repoRoot, "git", BuildGitEnvironment(repoRoot), gitArgs.ToArray());
    }

    private static void EnsureSuccess(string repoRoot, CommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Hermetic git command failed."
                + Environment.NewLine + $"Repo: {repoRoot}"
                + Environment.NewLine + $"Hooks path: {GetHooksPath(repoRoot)}"
                + Environment.NewLine + "StdErr:"
                + Environment.NewLine + result.StdErr
                + Environment.NewLine + "StdOut:"
                + Environment.NewLine + result.StdOut);
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildGitEnvironment(string repoRoot)
    {
        var home = GetHomePath(repoRoot);
        var xdg = Path.Combine(home, ".config");
        var globalConfig = Path.Combine(home, ".gitconfig");
        var templateDir = GetTemplatePath(repoRoot);
        var gnupg = Path.Combine(home, ".gnupg");

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["USERPROFILE"] = home,
            ["XDG_CONFIG_HOME"] = xdg,
            ["GIT_CONFIG_GLOBAL"] = globalConfig,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_TEMPLATE_DIR"] = templateDir,
            ["GNUPGHOME"] = gnupg
        };
    }

    private static void EnsureHermeticGitLayout(string repoRoot)
    {
        var home = GetHomePath(repoRoot);
        var xdg = Path.Combine(home, ".config");
        var globalConfig = Path.Combine(home, ".gitconfig");
        var templateDir = GetTemplatePath(repoRoot);
        var hooksDir = GetHooksPath(repoRoot);
        var gnupg = Path.Combine(home, ".gnupg");

        Directory.CreateDirectory(home);
        Directory.CreateDirectory(xdg);
        Directory.CreateDirectory(templateDir);
        Directory.CreateDirectory(hooksDir);
        Directory.CreateDirectory(gnupg);

        if (!File.Exists(globalConfig))
        {
            File.WriteAllText(globalConfig, string.Empty);
        }
    }

    private static string GetHermeticRoot(string repoRoot) => Path.Combine(repoRoot, ".workbench-test-git");

    private static string GetHomePath(string repoRoot) => Path.Combine(GetHermeticRoot(repoRoot), "home");

    private static string GetTemplatePath(string repoRoot) => Path.Combine(GetHermeticRoot(repoRoot), "templates");

    private static string GetHooksPath(string repoRoot) => Path.Combine(GetHermeticRoot(repoRoot), "hooks");
}
