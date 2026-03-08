using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class WorkItemEditTests
{
    [TestMethod]
    public void ApplyDraft_UpdatesManagedSections_AndNormalizesTags()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem(
            "TASK-0001-draft-target.md",
            """
            tags:
              - existing
            """,
            """
            # TASK-0001 - Draft target

            ## Summary

            Old summary

            ## Acceptance criteria

            - Old criterion
            """);

        var updated = WorkItemService.ApplyDraft(
            itemPath,
            new WorkItemDraft(
                "Ignored title",
                "  Fresh summary for the draft.  ",
                new[] { "First new criterion", "Second new criterion" },
                "task",
                new[] { " alpha ", "BETA", "", "beta", "Alpha" }));

        CollectionAssert.AreEqual(new[] { "alpha", "BETA" }, updated.Tags.ToArray());

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "## Summary", StringComparison.Ordinal);
        StringAssert.Contains(content, "Fresh summary for the draft.", StringComparison.Ordinal);
        StringAssert.Contains(content, "- First new criterion", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Second new criterion", StringComparison.Ordinal);
        Assert.IsFalse(content.Contains("Old summary", StringComparison.Ordinal), content);
    }

    [TestMethod]
    public void ApplyDraft_EmptySummary_Throws()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem("TASK-0001-empty-draft.md");

        try
        {
            WorkItemService.ApplyDraft(
                itemPath,
                new WorkItemDraft(
                    "Ignored title",
                    "   ",
                    new[] { "Criterion" },
                    "task",
                    new[] { "tag" }));
            Assert.Fail("Expected ApplyDraft to reject an empty summary.");
        }
        catch (InvalidOperationException error)
        {
            StringAssert.Contains(error.Message, "Draft summary is empty.", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void ApplyEditDraft_UpdatesTitleHeading_AndManagedSections()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem("TASK-0001-edit-draft.md");

        var updated = WorkItemService.ApplyEditDraft(
            itemPath,
            new WorkItemDraft(
                "  Updated item title  ",
                " Updated summary ",
                new[] { "Criterion A", "Criterion B" },
                "task",
                null));

        Assert.AreEqual("Updated item title", updated.Title);
        Assert.AreEqual(itemPath, updated.Path);

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "# TASK-0001 - Updated item title", StringComparison.Ordinal);
        StringAssert.Contains(content, "Updated summary", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Criterion A", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Criterion B", StringComparison.Ordinal);
    }

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

    [TestMethod]
    public void UpdateStatus_AppendsNote_AndSetsUpdatedDate()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem("TASK-0001-status-target.md");

        var updated = WorkItemService.UpdateStatus(itemPath, "in_progress", "Captured progress.");

        Assert.AreEqual("in_progress", updated.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(updated.Updated));

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "status: in_progress", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Notes", StringComparison.Ordinal);
        StringAssert.Contains(content, "- Captured progress.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Close_WithMove_MovesItemIntoDoneDirectory_AndMarksDone()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem("TASK-0001-close-target.md");

        var closed = WorkItemService.Close(itemPath, move: true, WorkbenchConfig.Default, repo.Path);

        Assert.AreEqual("done", closed.Status);
        Assert.IsFalse(File.Exists(itemPath));
        StringAssert.Contains(
            closed.Path.Replace('\\', '/'),
            "docs/70-work/done",
            StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(File.Exists(closed.Path));

        var content = File.ReadAllText(closed.Path);
        StringAssert.Contains(content, "status: done", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Move_WithRelativeDestination_UsesRepoRoot()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteDefaultItem("TASK-0001-move-target.md");
        var destination = Path.Combine("docs", "70-work", "done", "TASK-0001-move-target.md");

        var moved = WorkItemService.Move(itemPath, destination, repo.Path);

        var expectedPath = Path.Combine(repo.Path, destination);
        Assert.AreEqual(expectedPath, moved.Path);
        Assert.IsFalse(File.Exists(itemPath));
        Assert.IsTrue(File.Exists(expectedPath));
    }

    private sealed class TempRepoFixture : IDisposable
    {
        public TempRepoFixture()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, ".git"));
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "docs", "70-work", "items"));
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "docs", "70-work", "done"));
        }

        public string Path { get; }

        public string WriteItem(string fileName, string content)
        {
            var path = System.IO.Path.Combine(this.Path, "docs", "70-work", "items", fileName);
            File.WriteAllText(path, content.Replace("\r\n", "\n"));
            return path;
        }

        public string WriteDefaultItem(string fileName, string? frontMatterOverrides = null, string? body = null)
        {
            var bodyContent = body ?? """
                # TASK-0001 - Original title

                ## Summary

                Original summary

                ## Acceptance criteria

                - Original criterion
                """;
            var overridesBlock = string.IsNullOrWhiteSpace(frontMatterOverrides)
                ? string.Empty
                : $"{frontMatterOverrides.TrimEnd()}\n";

            return this.WriteItem(
                fileName,
                $$"""
                ---
                id: TASK-0001
                type: task
                status: draft
                priority: medium
                owner: null
                created: 2026-03-07
                updated: null
                githubSynced: null
                {{overridesBlock}}
                related:
                  specs: []
                  adrs: []
                  files: []
                  prs: []
                  issues: []
                  branches: []
                ---

                {{bodyContent}}
                """);
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
