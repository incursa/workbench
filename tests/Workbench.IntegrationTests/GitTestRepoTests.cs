namespace Workbench.IntegrationTests;

[TestClass]
public class GitTestRepoTests
{
    [TestMethod]
    public void InitializeGitRepo_IgnoresHostGitSigningAndHooks()
    {
        using var hostileHome = TempRepo.Create();
        var hostileHooks = Path.Combine(hostileHome.Path, "hooks");
        Directory.CreateDirectory(hostileHooks);
        File.WriteAllText(Path.Combine(hostileHooks, "pre-commit"), "#!/bin/sh\nexit 99\n");
        File.WriteAllText(
            Path.Combine(hostileHome.Path, ".gitconfig"),
            $$"""
            [commit]
                gpgSign = true
            [tag]
                gpgSign = true
            [core]
                hooksPath = {{hostileHooks.Replace("\\", "/")}}
            [gpg]
                program = not-a-real-gpg
            """);

        using var repo = TempRepo.Create();
        using var homeScope = new ScopedEnvironmentVariable("HOME", hostileHome.Path);
        using var userProfileScope = new ScopedEnvironmentVariable("USERPROFILE", hostileHome.Path);
        using var xdgScope = new ScopedEnvironmentVariable("XDG_CONFIG_HOME", hostileHome.Path);

        GitTestRepo.InitializeGitRepo(repo.Path);

        var logResult = GitTestRepo.RunGit(repo.Path, "log", "-1", "--pretty=%B");
        Assert.AreEqual(0, logResult.ExitCode, $"stderr: {logResult.StdErr}\nstdout: {logResult.StdOut}");
        Assert.AreEqual("Initial commit", logResult.StdOut.Trim(), logResult.StdOut);
    }

    [TestMethod]
    public void CommitAll_UsesHermeticLocalGitConfig()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        File.WriteAllText(Path.Combine(repo.Path, "note.txt"), "hello");
        GitTestRepo.CommitAll(repo.Path, "Add note");

        var messageResult = GitTestRepo.RunGit(repo.Path, "log", "-1", "--pretty=%B");
        Assert.AreEqual(0, messageResult.ExitCode, $"stderr: {messageResult.StdErr}\nstdout: {messageResult.StdOut}");
        Assert.AreEqual("Add note", messageResult.StdOut.Trim());

        var signResult = GitTestRepo.RunGit(repo.Path, "config", "--local", "--get", "commit.gpgSign");
        Assert.AreEqual(0, signResult.ExitCode, $"stderr: {signResult.StdErr}\nstdout: {signResult.StdOut}");
        Assert.AreEqual("false", signResult.StdOut.Trim());

        var hooksResult = GitTestRepo.RunGit(repo.Path, "config", "--local", "--get", "core.hooksPath");
        Assert.AreEqual(0, hooksResult.ExitCode, $"stderr: {hooksResult.StdErr}\nstdout: {hooksResult.StdOut}");
        StringAssert.Contains(hooksResult.StdOut, ".workbench-test-git", StringComparison.Ordinal);
    }

    private sealed class ScopedEnvironmentVariable : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        public ScopedEnvironmentVariable(string name, string? value)
        {
            this.name = name;
            this.originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(this.name, this.originalValue);
        }
    }
}
