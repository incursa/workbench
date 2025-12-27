using Workbench;

namespace Workbench.Tests;

[TestClass]
public class ValidationTests
{
    [TestMethod]
    public void ValidateRepo_FindsBrokenMarkdownLinks()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

        var docPath = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(docPath);
        File.WriteAllText(Path.Combine(docPath, "README.md"), "See [missing](missing.md).");

        var result = ValidationService.ValidateRepo(repoRoot, WorkbenchConfig.Default);
        Assert.IsNotEmpty(result.Errors);
    }
}
