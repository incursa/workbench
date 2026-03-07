using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class WorkItemEditTests
{
    [TestMethod]
    public void EditItem_UpdatesManagedSections_AndRenamesFile()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-old-title.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            priority: medium
            owner: null
            created: 2026-03-07
            updated: null
            githubSynced: null
            tags: []
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Old title

            ## Summary

            Old summary

            ## Acceptance criteria
            - Old criterion
            """);

        var result = WorkItemService.EditItem(
            itemPath,
            "New title",
            "New summary",
            new[] { "First criterion", "Second criterion" },
            "Captured a structured note.",
            renameFile: true,
            WorkbenchConfig.Default,
            repo.Path);

        Assert.IsTrue(result.PathChanged);
        Assert.IsTrue(result.TitleUpdated);
        Assert.IsTrue(result.SummaryUpdated);
        Assert.IsTrue(result.AcceptanceCriteriaUpdated);
        Assert.IsTrue(result.NotesAppended);
        Assert.IsFalse(File.Exists(itemPath));
        Assert.IsTrue(File.Exists(result.Item.Path));

        var content = File.ReadAllText(result.Item.Path);
        StringAssert.Contains(content, "# TASK-0001 - New title", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Summary", StringComparison.Ordinal);
        StringAssert.Contains(content, "New summary", StringComparison.Ordinal);
        StringAssert.Contains(content, "- First criterion", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Second criterion", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Notes", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Captured a structured note.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void EditItem_AddsMissingHeading_AndSections()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-broken.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            Existing freeform body.
            """);

        var result = WorkItemService.EditItem(
            itemPath,
            "Recovered title",
            "Recovered summary",
            new[] { "Recovered criterion" },
            null,
            renameFile: false,
            WorkbenchConfig.Default,
            repo.Path);

        Assert.IsFalse(result.PathChanged);
        var content = File.ReadAllText(result.Item.Path);
        Assert.IsTrue(content.Contains("# TASK-0001 - Recovered title", StringComparison.Ordinal), content);
        Assert.IsTrue(content.Contains("## Summary", StringComparison.Ordinal), content);
        Assert.IsTrue(content.Contains("Recovered summary", StringComparison.Ordinal), content);
        Assert.IsTrue(content.Contains("## Acceptance criteria", StringComparison.Ordinal), content);
        Assert.IsTrue(content.Contains("- Recovered criterion", StringComparison.Ordinal), content);
    }

    [TestMethod]
    public void Rename_UpdatesTitleHeading()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-old-title.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Old title
            """);

        var renamed = WorkItemService.Rename(itemPath, "Fresh title", WorkbenchConfig.Default, repo.Path);

        Assert.IsTrue(File.Exists(renamed.Path));
        var content = File.ReadAllText(renamed.Path);
        StringAssert.Contains(content, "# TASK-0001 - Fresh title", StringComparison.Ordinal);
    }

    private sealed class TempRepoFixture : IDisposable
    {
        public TempRepoFixture()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, ".git"));
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "docs", "70-work", "items"));
        }

        public string Path { get; }

        public string WriteItem(string fileName, string content)
        {
            var path = System.IO.Path.Combine(this.Path, "docs", "70-work", "items", fileName);
            File.WriteAllText(path, content.Replace("\r\n", "\n"));
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
