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

    [TestMethod]
    public void CreateItemFromGithubIssue_CreatesLinkedItemWithImportedSummary()
    {
        using var repo = new TempRepoFixture();
        repo.WriteTaskTemplate();
        var issue = CreateGithubIssue(
            title: "Imported issue title",
            body: "First line\\nSecond line",
            url: "https://github.com/octo/demo/issues/42",
            labels: new[] { "backend", "urgent" },
            pullRequests: new[] { "https://github.com/octo/demo/pull/9" });

        var item = WorkItemService.CreateItemFromGithubIssue(
            repo.Path,
            WorkbenchConfig.Default,
            issue,
            "task",
            "ready",
            "high",
            "platform");

        Assert.AreEqual("Imported issue title", item.Title);
        Assert.AreEqual("ready", item.Status);
        Assert.AreEqual("high", item.Priority);
        Assert.AreEqual("platform", item.Owner);
        CollectionAssert.AreEqual(new[] { "backend", "urgent" }, item.Tags.ToArray());
        CollectionAssert.Contains(item.Related.Issues.ToArray(), issue.Url);
        CollectionAssert.Contains(item.Related.Prs.ToArray(), "https://github.com/octo/demo/pull/9");

        var content = File.ReadAllText(item.Path);
        StringAssert.Contains(content, "githubSynced:", StringComparison.Ordinal);
        StringAssert.Contains(content, "Imported from GitHub issue: https://github.com/octo/demo/issues/42", StringComparison.Ordinal);
        StringAssert.Contains(content, "First line", StringComparison.Ordinal);
        StringAssert.Contains(content, "Second line", StringComparison.Ordinal);
    }

    [TestMethod]
    public void UpdateItemFromGithubIssue_AddsRelatedLinksAndRewritesSummary()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-outdated.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            title: Outdated title
            tags: []
            ---

            # TASK-0001 - Outdated title

            ## Summary

            Old summary
            """);
        var issue = CreateGithubIssue(
            title: "Updated from GitHub",
            body: "Fresh remote summary",
            url: "https://github.com/octo/demo/issues/77",
            labels: new[] { "api", "sync" },
            pullRequests: new[] { "https://github.com/octo/demo/pull/11", "https://github.com/octo/demo/pull/12" });

        var updated = WorkItemService.UpdateItemFromGithubIssue(itemPath, issue, apply: true);

        Assert.AreEqual("Updated from GitHub", updated.Title);
        CollectionAssert.AreEqual(new[] { "api", "sync" }, updated.Tags.ToArray());
        CollectionAssert.Contains(updated.Related.Issues.ToArray(), issue.Url);
        CollectionAssert.Contains(updated.Related.Prs.ToArray(), "https://github.com/octo/demo/pull/11");
        CollectionAssert.Contains(updated.Related.Prs.ToArray(), "https://github.com/octo/demo/pull/12");

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "# TASK-0001 - Updated from GitHub", StringComparison.Ordinal);
        StringAssert.Contains(content, "Imported from GitHub issue: https://github.com/octo/demo/issues/77", StringComparison.Ordinal);
        StringAssert.Contains(content, "Fresh remote summary", StringComparison.Ordinal);
        StringAssert.Contains(content, "related:", StringComparison.Ordinal);
        StringAssert.Contains(content, "issues:", StringComparison.Ordinal);
        StringAssert.Contains(content, "prs:", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NormalizeItems_RepairsTagsAndRelatedCollections()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-normalize.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            title: Normalize me
            tags: existing
            related:
              specs:
                - </docs/10-product/spec-a.md>
                - /docs/10-product/spec-a.md
              prs:
                - https://github.com/octo/demo/pull/1
                - https://github.com/octo/demo/pull/1
              branches: feature/normalize
            ---

            # TASK-0001 - Normalize me

            ## Summary

            Normalize this item
            """);

        var updated = WorkItemService.NormalizeItems(repo.Path, WorkbenchConfig.Default, includeDone: false, dryRun: false);

        Assert.AreEqual(1, updated);
        var normalized = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        Assert.IsEmpty(normalized.Tags);
        CollectionAssert.AreEqual(new[] { "/docs/10-product/spec-a.md" }, normalized.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "https://github.com/octo/demo/pull/1" }, normalized.Related.Prs.ToArray());
        Assert.IsEmpty(normalized.Related.Branches);
        Assert.IsEmpty(normalized.Related.Adrs);
        Assert.IsEmpty(normalized.Related.Files);
        Assert.IsEmpty(normalized.Related.Issues);

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "tags: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "- /docs/10-product/spec-a.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateItem_WithoutTemplate_ThrowsHelpfulError()
    {
        using var repo = new TempRepoFixture();

        try
        {
            WorkItemService.CreateItem(repo.Path, WorkbenchConfig.Default, "task", "Missing template", null, null, null);
            Assert.Fail("Expected CreateItem to reject a missing template.");
        }
        catch (InvalidOperationException error)
        {
            StringAssert.Contains(error.Message, "Template not found:", StringComparison.Ordinal);
            StringAssert.Contains(error.Message, "work-item.task.md", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void CreateItem_WithInvalidTemplateFrontMatter_ThrowsHelpfulError()
    {
        using var repo = new TempRepoFixture();
        repo.WriteTaskTemplate(
            """
            ---
            id: TASK-0000
            type task
            ---

            # TASK-0000 - <title>
            """);

        try
        {
            WorkItemService.CreateItem(repo.Path, WorkbenchConfig.Default, "task", "Broken template", null, null, null);
            Assert.Fail("Expected CreateItem to reject malformed template front matter.");
        }
        catch (InvalidOperationException error)
        {
            StringAssert.Contains(error.Message, "Template front matter error:", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void AddAndRemoveRelatedLink_NormalizeAndMatchCaseInsensitive()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-related-links.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs:
                - </docs/10-product/spec-a.md>
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Related links

            ## Summary

            Related links
            """);

        var normalized = WorkItemService.AddRelatedLink(itemPath, "specs", "/docs/10-product/spec-a.md");
        var duplicate = WorkItemService.AddRelatedLink(itemPath, "specs", "/docs/10-product/spec-a.md");
        var removed = WorkItemService.RemoveRelatedLink(itemPath, "specs", "/DOCS/10-PRODUCT/SPEC-A.MD");

        Assert.IsTrue(normalized);
        Assert.IsFalse(duplicate);
        Assert.IsTrue(removed);

        var content = File.ReadAllText(itemPath);
        Assert.IsFalse(content.Contains("</docs/10-product/spec-a.md>", StringComparison.Ordinal), content);
        Assert.IsFalse(content.Contains("/docs/10-product/spec-a.md", StringComparison.Ordinal), content);
    }

    [TestMethod]
    public void ReplaceRelatedLinks_DeduplicatesReplacementTarget()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-replace-links.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs:
                - /docs/10-product/spec-a.md
                - /docs/10-product/spec-b.md
              adrs: []
              files:
                - /docs/20-architecture/file-a.md
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Replace links

            ## Summary

            Replace links
            """);

        var changed = WorkItemService.ReplaceRelatedLinks(
            itemPath,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["/docs/10-product/spec-b.md"] = "/docs/10-product/spec-a.md",
                ["/docs/20-architecture/file-a.md"] = "/docs/20-architecture/file-b.md"
            });

        Assert.IsTrue(changed);
        var updated = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.AreEqual(new[] { "/docs/10-product/spec-a.md" }, updated.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "/docs/20-architecture/file-b.md" }, updated.Related.Files.ToArray());
    }

    [TestMethod]
    public void NormalizeRelatedLinks_DryRunLeavesFileUntouched_ButApplyNormalizes()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-normalize-related.md",
            """
            ---
            id: TASK-0001
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs:
                - </docs/10-product/spec-a.md>
                - /docs/10-product/spec-a.md
              adrs:
                - </docs/40-decisions/adr-a.md>
              files: []
              prs:
                - https://github.com/octo/demo/pull/1
                - https://github.com/octo/demo/pull/1
              issues:
                - https://github.com/octo/demo/issues/7
                - https://github.com/octo/demo/issues/7
              branches: []
            ---

            # TASK-0001 - Normalize related links

            ## Summary

            Normalize related links
            """);
        var original = File.ReadAllText(itemPath);

        var dryRunUpdated = WorkItemService.NormalizeRelatedLinks(repo.Path, WorkbenchConfig.Default, includeDone: false, dryRun: true);

        Assert.AreEqual(0, dryRunUpdated);
        Assert.AreEqual(original, File.ReadAllText(itemPath));

        var updated = WorkItemService.NormalizeRelatedLinks(repo.Path, WorkbenchConfig.Default, includeDone: false, dryRun: false);

        Assert.AreEqual(1, updated);
        var normalized = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.AreEqual(new[] { "/docs/10-product/spec-a.md" }, normalized.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "/docs/40-decisions/adr-a.md" }, normalized.Related.Adrs.ToArray());
        CollectionAssert.AreEqual(new[] { "https://github.com/octo/demo/pull/1" }, normalized.Related.Prs.ToArray());
        CollectionAssert.AreEqual(new[] { "https://github.com/octo/demo/issues/7" }, normalized.Related.Issues.ToArray());
    }

    private static GithubIssue CreateGithubIssue(
        string title,
        string body,
        string url,
        IList<string>? labels = null,
        IList<string>? pullRequests = null)
    {
        return new GithubIssue(
            new GithubRepoRef("github.com", "octo", "demo"),
            42,
            title,
            body,
            url,
            "open",
            labels ?? Array.Empty<string>(),
            pullRequests ?? Array.Empty<string>());
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
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "docs", "70-work", "templates"));
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

        public void WriteTaskTemplate(string? content = null)
        {
            var templatePath = System.IO.Path.Combine(this.Path, "docs", "70-work", "templates", "work-item.task.md");
            File.WriteAllText(
                templatePath,
                content?.Replace("\r\n", "\n") ?? """
                ---
                id: TASK-0000
                type: task
                status: draft
                priority: medium
                owner: null
                created: 0000-00-00
                updated: null
                tags: []
                related:
                  specs: []
                  adrs: []
                  files: []
                  prs: []
                  issues: []
                  branches: []
                ---

                # TASK-0000 - <title>

                ## Summary

                ## Acceptance criteria
                -
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
