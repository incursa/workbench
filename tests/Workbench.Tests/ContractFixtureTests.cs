using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ContractFixtureTests
{
    [TestMethod]
    public void ValidWorkItemFixture_ParsesAndValidatesAgainstSchema()
    {
        var repoRoot = FindRepoRoot();
        var fixturePath = Path.Combine(repoRoot, "testdata", "contracts", "work-item.valid.md");
        var content = File.ReadAllText(fixturePath);

        var ok = FrontMatter.TryParse(content, out var frontMatter, out var parseError);
        Assert.IsTrue(ok, parseError);
        Assert.IsNotNull(frontMatter);

        var errors = SchemaValidationService.ValidateFrontMatter(
            repoRoot,
            fixturePath,
            frontMatter!.Data);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void InvalidWorkItemFixture_FailsSchemaValidation()
    {
        var repoRoot = FindRepoRoot();
        var fixturePath = Path.Combine(repoRoot, "testdata", "contracts", "work-item.invalid-missing-id.md");
        var content = File.ReadAllText(fixturePath);

        var ok = FrontMatter.TryParse(content, out var frontMatter, out var parseError);
        Assert.IsTrue(ok, parseError);
        Assert.IsNotNull(frontMatter);

        var errors = SchemaValidationService.ValidateFrontMatter(
            repoRoot,
            fixturePath,
            frontMatter!.Data);

        Assert.IsNotEmpty(errors, "Expected schema validation errors for fixture missing required id.");
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

        throw new DirectoryNotFoundException("Could not locate Workbench.slnx.");
    }
}
