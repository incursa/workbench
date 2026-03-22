using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public class CliHelpContractTests
{
    [TestMethod]
    public void DocRegenHelp_CheckPassesForCheckedInSnapshot()
    {
        var repoRoot = Repository.FindRepoRoot(Environment.CurrentDirectory)
            ?? throw new InvalidOperationException("Repo root not found.");

        var result = WorkbenchCli.Run(
            repoRoot,
            "--repo",
            repoRoot,
            "doc",
            "regen-help",
            "--check");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "CLI help is up to date", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocRegenHelp_CheckFailsOnDrift_AndRegenRestoresSnapshot()
    {
        using var repo = TempRepo.Create();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git"));

        var cliHelpPath = Path.Combine(repo.Path, "docs", "commands.md");
        Directory.CreateDirectory(Path.GetDirectoryName(cliHelpPath)!);
        File.WriteAllText(cliHelpPath, "# stale snapshot\n");

        var checkResult = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doc",
            "regen-help",
            "--check");

        Assert.AreEqual(2, checkResult.ExitCode, $"stderr: {checkResult.StdErr}\nstdout: {checkResult.StdOut}");
        StringAssert.Contains(checkResult.StdOut, "CLI help drift detected", StringComparison.Ordinal);

        var regenResult = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doc",
            "regen-help");

        Assert.AreEqual(0, regenResult.ExitCode, $"stderr: {regenResult.StdErr}\nstdout: {regenResult.StdOut}");
        Assert.IsTrue(File.Exists(cliHelpPath));

        var content = File.ReadAllText(cliHelpPath);
        StringAssert.Contains(content, "# Workbench CLI Help", StringComparison.Ordinal);
        StringAssert.Contains(content, "Generated from the live `System.CommandLine` tree.", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Sync model", StringComparison.Ordinal);
        StringAssert.Contains(content, "`workbench sync`: umbrella command", StringComparison.Ordinal);
        StringAssert.Contains(content, "`workbench spec`: dedicated requirement-spec workflow", StringComparison.Ordinal);
        StringAssert.Contains(content, "### `workbench spec`", StringComparison.Ordinal);
        StringAssert.Contains(content, "### `workbench spec new`", StringComparison.Ordinal);
        StringAssert.Contains(content, "### `workbench item edit`", StringComparison.Ordinal);
        StringAssert.Contains(content, "### `workbench doc regen-help`", StringComparison.Ordinal);

        var secondCheckResult = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "doc",
            "regen-help",
            "--check");

        Assert.AreEqual(0, secondCheckResult.ExitCode, $"stderr: {secondCheckResult.StdErr}\nstdout: {secondCheckResult.StdOut}");
    }
}
