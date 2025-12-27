using Workbench;

namespace Workbench.Tests;

[TestClass]
public class IdAllocationTests
{
    [TestMethod]
    public void CreateItem_AllocatesNextIdPerType()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

        var config = WorkbenchConfig.Default;
        Directory.CreateDirectory(Path.Combine(repoRoot, config.Paths.ItemsDir));
        Directory.CreateDirectory(Path.Combine(repoRoot, config.Paths.DoneDir));
        Directory.CreateDirectory(Path.Combine(repoRoot, config.Paths.TemplatesDir));

        File.WriteAllText(Path.Combine(repoRoot, config.Paths.ItemsDir, "TASK-0001-first.md"), "#");
        File.WriteAllText(Path.Combine(repoRoot, config.Paths.DoneDir, "TASK-0003-third.md"), "#");

        var template = """
            ---
            id: TASK-0000
            type: task
            status: draft
            created: 0000-00-00
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
            ---

            # TASK-0000 - <title>
            """;
        File.WriteAllText(Path.Combine(repoRoot, config.Paths.TemplatesDir, "work-item.task.md"), template);

        var result = WorkItemService.CreateItem(repoRoot, config, "task", "Next task", null, null, null);
        Assert.AreEqual("TASK-0004", result.Id);
    }
}
