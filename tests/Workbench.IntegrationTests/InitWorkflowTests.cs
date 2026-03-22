namespace Workbench.IntegrationTests;

[TestClass]
public class InitWorkflowTests
{
    [TestMethod]
    public void Init_NonInteractive_ScaffoldsRepoAndSkipsGuide()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "init",
            "--non-interactive",
            "--skip-guide");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Step 1: Scaffold repo layout", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Init summary:", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Scaffolded repo (", StringComparison.Ordinal);

        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, ".workbench", "config.json")));
        Assert.IsTrue(Directory.Exists(Path.Combine(repo.Path, "work", "items")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "work", "templates", "work-item.task.md")));
    }

    [TestMethod]
    public void Init_NonInteractiveFrontMatter_AddsWorkbenchFrontMatterToExistingDoc()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        Directory.CreateDirectory(Path.Combine(repo.Path, "docs", "10-product"));
        var docPath = Path.Combine(repo.Path, "docs", "10-product", "init-front-matter.md");
        File.WriteAllText(
            docPath,
            """
            # Existing doc

            This doc starts without Workbench front matter.
            """);

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "init",
            "--non-interactive",
            "--skip-guide",
            "--front-matter");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Step 2: Front matter guidance", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Updated front matter in", StringComparison.Ordinal);

        var content = File.ReadAllText(docPath);
        StringAssert.Contains(content, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(content, "type: doc", StringComparison.Ordinal);
        StringAssert.Contains(content, "path: /docs/10-product/init-front-matter.md", StringComparison.Ordinal);
        StringAssert.Contains(content, "# Existing doc", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Init_NonInteractiveConfigureOpenAi_LocalStore_WritesCredentialFile_AndGitignore()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        var credentialPath = Path.Combine(repo.Path, ".workbench", "credentials.env");

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "init",
            "--non-interactive",
            "--skip-guide",
            "--configure-openai",
            "--credential-store",
            "local",
            "--credential-path",
            credentialPath,
            "--openai-provider",
            "openai",
            "--openai-key",
            "test-secret",
            "--openai-model",
            "gpt-4.1-mini");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Step 3: OpenAI configuration", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, $"Wrote OpenAI credentials to {credentialPath}.", StringComparison.Ordinal);

        Assert.IsTrue(File.Exists(credentialPath));
        var credentialContent = File.ReadAllText(credentialPath);
        StringAssert.Contains(credentialContent, "WORKBENCH_AI_PROVIDER=openai", StringComparison.Ordinal);
        StringAssert.Contains(credentialContent, "WORKBENCH_AI_OPENAI_KEY=test-secret", StringComparison.Ordinal);
        StringAssert.Contains(credentialContent, "WORKBENCH_AI_MODEL=gpt-4.1-mini", StringComparison.Ordinal);

        var gitignorePath = Path.Combine(repo.Path, ".gitignore");
        Assert.IsTrue(File.Exists(gitignorePath));
        var gitignore = File.ReadAllText(gitignorePath);
        StringAssert.Contains(gitignore, ".workbench/credentials.env", StringComparison.Ordinal);
    }
}
