using Workbench;
using Workbench.Core;

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

    [TestMethod]
    public void ValidateRepo_FailsWhenDoneItemLivesInActiveDirectory()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "items"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0001-test.md"),
            """
            ---
            id: TASK-0001
            type: task
            status: done
            created: 2026-02-19
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Test
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("terminal status 'done' must live under", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_FailsWhenActiveItemLivesInDoneDirectory()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "done"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "done", "TASK-0002-test.md"),
            """
            ---
            id: TASK-0002
            type: task
            status: ready
            created: 2026-02-19
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0002 - Test
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("non-terminal status 'ready' must live under", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }
}
