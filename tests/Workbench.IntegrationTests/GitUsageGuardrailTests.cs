using System.Text.RegularExpressions;

namespace Workbench.IntegrationTests;

[TestClass]
public class GitUsageGuardrailTests
{
    [TestMethod]
    public void IntegrationTests_DoNotInvokeGitThroughProcessRunner()
    {
        var repoRoot = FindRepoRoot();
        var integrationTestsDir = Path.Combine(repoRoot, "tests", "Workbench.IntegrationTests");
        var files = Directory.EnumerateFiles(integrationTestsDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith("GitTestRepo.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var violations = new List<string>();
        var pattern = new Regex(
            @"ProcessRunner\.Run\s*\([\s\S]*?""git""",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (!pattern.IsMatch(content))
            {
                continue;
            }

            violations.Add(Path.GetFileName(file));
        }

        Assert.IsEmpty(
            violations,
            "Use GitTestRepo.RunGit for git commands in integration tests. Violations: "
            + string.Join(", ", violations));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Workbench.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Workbench.slnx to locate repo root.");
    }
}
