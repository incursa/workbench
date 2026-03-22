using Workbench;
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
    public void EditItem_UpdatesMetadataFields()
    {
        using var repo = new TempRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0001-metadata.md",
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

            # TASK-0001 - Metadata target

            ## Summary

            Original summary

            ## Acceptance criteria
            - Original criterion
            """);

        var result = WorkItemService.EditItem(
            itemPath,
            null,
            null,
            null,
            null,
            renameFile: false,
            WorkbenchConfig.Default,
            repo.Path,
            status: "in-progress",
            priority: "high",
            owner: "platform");

        Assert.AreEqual("in-progress", result.Item.Status);
        Assert.AreEqual("high", result.Item.Priority);
        Assert.AreEqual("platform", result.Item.Owner);

        var content = File.ReadAllText(result.Item.Path);
        StringAssert.Contains(content, "status: in-progress", StringComparison.Ordinal);
        StringAssert.Contains(content, "priority: high", StringComparison.Ordinal);
        StringAssert.Contains(content, "owner: platform", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ListDocs_AndGetDoc_BrowseLocalMarkdown()
    {
        using var repo = new TempRepoFixture();
        var docsRoot = Path.Combine(repo.Path, "specs");
        Directory.CreateDirectory(docsRoot);

        var docPath = Path.Combine(docsRoot, "SPEC-WEB-LOCAL-UI.md");
        File.WriteAllText(
            docPath,
            """
            ---
            artifact_id: SPEC-WEB-LOCAL-UI
            artifact_type: specification
            title: Requirement Spec: Local Web UI Mode
            domain: WEB
            capability: local-ui
            status: draft
            owner: platform
            ---

            # Requirement Spec: Local Web UI Mode

            A browser-based local UI for the repo.
            """);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var docs = workspace.ListDocs("spec", null);

        Assert.HasCount(1, docs);
        Assert.AreEqual("Requirement Spec: Local Web UI Mode", docs[0].Title);
        Assert.AreEqual("specification", docs[0].Type);
        StringAssert.Contains(docs[0].Excerpt, "browser-based local UI", StringComparison.Ordinal);

        var detail = workspace.GetDoc("specs/SPEC-WEB-LOCAL-UI.md");
        Assert.IsNotNull(detail);
        Assert.AreEqual("Requirement Spec: Local Web UI Mode", detail!.Summary.Title);
        StringAssert.Contains(detail.Body, "browser-based local UI", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateSpec_UsesPolicyDrivenId_PreservesOwner_AndLinksWorkItems()
    {
        using var repo = new TempRepoFixture();
        File.WriteAllText(
            Path.Combine(repo.Path, "artifact-id-policy.json"),
            """
            {
              "sequence": { "minimum_digits": 4 },
              "artifact_id_templates": {
                "specification": "SPEC-{domain}{grouping}"
              }
            }
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "work", "templates"));
        File.WriteAllText(
            Path.Combine(repo.Path, "work", "templates", "work-item.task.md"),
            """
            ---
            id: TASK-0000
            type: task
            status: draft
            priority: medium
            owner: null
            created: 0000-00-00
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

            # TASK-0000 - <title>

            ## Summary

            ## Context

            ## Trace Links

            Addresses:

            - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

            Uses Design:

            - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

            Verified By:

            - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

            ## Implementation notes

            -

            ## Acceptance criteria

            -

            ## Notes

            -
            """);

        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "task",
            "Spec target",
            null,
            null,
            null);

        Directory.CreateDirectory(Path.Combine(repo.Path, "architecture"));
        File.WriteAllText(
            Path.Combine(repo.Path, "architecture", "spec-editor.md"),
            """
            # Spec Editor Architecture

            Architecture notes for the spec editor.
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "decisions"));
        File.WriteAllText(
            Path.Combine(repo.Path, "decisions", "ADR-2026-03-20-cli-spec-workflow.md"),
            """
            # CLI Spec Workflow Decision

            Decision notes for the spec workflow.
            """);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        StringAssert.Contains(workspace.GetSpecIdPolicySummary(), "Custom spec IDs are enabled", StringComparison.Ordinal);
        var created = workspace.CreateSpec(new SpecEditorInput
        {
            Title = "CLI onboarding spec",
            Domain = "CLI",
            Capability = "ONBOARDING",
            Owner = "platform",
            Summary = "Describe how the CLI spec workflow should behave.",
            Scope = "Spec authoring and management.",
            Context = "The repo uses policy-driven spec identifiers.",
            Requirements = """
                ## REQ-CLI-0001 Example requirement

                The system MUST create repository-native specs with explicit traceability sections.
                """,
            RelatedArchitectureDocs = "- /architecture/spec-editor.md",
            RelatedWorkItems = workItem.Id,
            RelatedAdrs = "- /decisions/ADR-2026-03-20-cli-spec-workflow.md",
            OpenQuestions = "- Should the browser UI support inline path overrides?",
            CodeRefs = "src/Workbench.Cli/Program.cs#L1-L3"
        });

        Assert.AreEqual("CLI onboarding spec", created.Summary.Title);
        Assert.AreEqual("specification", created.Summary.Type);
        Assert.AreEqual("SPEC-CLI-ONBOARDING", created.Summary.ArtifactId);
        Assert.AreEqual("platform", created.FrontMatter["owner"]?.ToString());
        StringAssert.Contains(created.Body, "# CLI onboarding spec", StringComparison.Ordinal);
        StringAssert.Contains(created.Body, "## REQ-CLI-0001 Example requirement", StringComparison.Ordinal);
        StringAssert.Contains(created.Body, "The system MUST create repository-native specs with explicit traceability sections.", StringComparison.Ordinal);

        var savedContent = File.ReadAllText(Path.Combine(repo.Path, created.Summary.Path.Replace('/', Path.DirectorySeparatorChar)));
        StringAssert.Contains(savedContent, "artifact_id: SPEC-CLI-ONBOARDING", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "owner: platform", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "related_artifacts:", StringComparison.Ordinal);

        var updatedItem = WorkItemService.LoadItem(workItem.Path) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.Contains(
            updatedItem.Related.Specs.ToArray(),
            "/" + created.Summary.Path.Replace('\\', '/'));
    }

    [TestMethod]
    public void ListFiles_AndGetFile_BrowseLocalRepoFiles()
    {
        using var repo = new TempRepoFixture();
        var docsRoot = Path.Combine(repo.Path, "specs");
        var srcRoot = Path.Combine(repo.Path, "src", "Workbench");
        Directory.CreateDirectory(docsRoot);
        Directory.CreateDirectory(srcRoot);

        var markdownPath = Path.Combine(docsRoot, "SPEC-WEB-LOCAL-UI.md");
        File.WriteAllText(
            markdownPath,
            """
            ---
            artifact_id: SPEC-WEB-LOCAL-UI
            artifact_type: specification
            title: Requirement Spec: Local Web UI Mode
            domain: WEB
            capability: local-ui
            status: draft
            owner: platform
            ---

            # Requirement Spec: Local Web UI Mode

            A browser-based local UI for the repo.
            """);

        var textPath = Path.Combine(srcRoot, "notes.txt");
        File.WriteAllText(textPath, "First line\nSecond line\nThird line");

        var binaryPath = Path.Combine(repo.Path, "assets.bin");
        File.WriteAllBytes(binaryPath, [0x00, 0x01, 0x02, 0x03]);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var files = workspace.ListFiles("all", null);

        Assert.IsTrue(files.Any(file => file.Path.Equals("specs/SPEC-WEB-LOCAL-UI.md", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(files.Any(file => file.Path.Equals("src/Workbench/notes.txt", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(files.Any(file => file.Path.Equals("assets.bin", StringComparison.OrdinalIgnoreCase)));

        var markdown = files.First(file => file.Path.Equals("specs/SPEC-WEB-LOCAL-UI.md", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("markdown", markdown.FileType);
        StringAssert.Contains(markdown.Excerpt, "SPEC-WEB-LOCAL-UI", StringComparison.Ordinal);

        var text = files.First(file => file.Path.Equals("src/Workbench/notes.txt", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("text", text.FileType);
        StringAssert.Contains(text.Excerpt, "First line", StringComparison.Ordinal);

        var binary = files.First(file => file.Path.Equals("assets.bin", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("binary", binary.FileType);

        var detail = workspace.GetFile("specs/SPEC-WEB-LOCAL-UI.md");
        Assert.IsNotNull(detail);
        Assert.IsTrue(detail!.IsMarkdown);
        StringAssert.Contains(detail.Body, "browser-based local UI", StringComparison.Ordinal);

        var rendered = RepoContentRenderer.RenderMarkdown(detail.Body);
        StringAssert.Contains(rendered, "id=\"requirement-spec-local-web-ui-mode\"", StringComparison.Ordinal);
        StringAssert.Contains(rendered, "Requirement Spec: Local Web UI Mode", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildDocTree_AndBuildFileTree_CreateNestedNavigators()
    {
        using var repo = new TempRepoFixture();
        var docsRoot = Path.Combine(repo.Path, "docs");
        var docsProductRoot = Path.Combine(repo.Path, "specs");
        var srcPagesRoot = Path.Combine(repo.Path, "src", "Workbench", "Pages");
        Directory.CreateDirectory(docsRoot);
        Directory.CreateDirectory(docsProductRoot);
        Directory.CreateDirectory(srcPagesRoot);

        File.WriteAllText(
            Path.Combine(docsRoot, "README.md"),
            """
            # Repository docs
            """);

        File.WriteAllText(
            Path.Combine(docsProductRoot, "SPEC-WEB-LOCAL-UI.md"),
            """
            ---
            artifact_id: SPEC-WEB-LOCAL-UI
            artifact_type: specification
            title: Requirement Spec: Local Web UI Mode
            domain: WEB
            capability: local-ui
            status: draft
            owner: platform
            ---

            # Requirement Spec: Local Web UI Mode
            """);

        File.WriteAllText(
            Path.Combine(srcPagesRoot, "Index.cshtml"),
            """
            <h1>Items</h1>
            """);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);

        var docTree = WorkbenchWorkspace.BuildDocTree(
            workspace.ListDocs("all", null),
            doc => $"/Docs?selectedPath={Uri.EscapeDataString(doc.Path)}",
            "specs/SPEC-WEB-LOCAL-UI.md");

        var specsBranch = docTree.Children.First(child => child.Name.Equals("specs", StringComparison.OrdinalIgnoreCase));
        Assert.HasCount(1, specsBranch.Entries);
        Assert.AreEqual("specs/SPEC-WEB-LOCAL-UI.md", specsBranch.Entries[0].Path);
        Assert.IsTrue(specsBranch.Entries[0].IsSelected);

        var fileTree = WorkbenchWorkspace.BuildFileTree(
            workspace.ListFiles("all", null),
            file => $"/Files?selectedPath={Uri.EscapeDataString(file.Path)}",
            "src/Workbench/Pages/Index.cshtml");

        var srcBranch = fileTree.Children.First(child => child.Name.Equals("src", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(srcBranch.Children.Any(child => child.Name.Equals("Workbench", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void UserProfileStore_SavesAndLoadsLocalIdentity()
    {
        var profilePath = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"), "profile.json");
        var store = new WorkbenchUserProfileStore(profilePath);
        var profile = new WorkbenchUserProfile
        {
            DisplayName = "Sam Example",
            Handle = "sam",
            Email = "sam@example.com",
            DefaultOwner = "platform"
        };

        store.Save(profile);
        var loaded = store.Load(out var error);

        Assert.IsNull(error);
        Assert.AreEqual("Sam Example", loaded.DisplayName);
        Assert.AreEqual("sam", loaded.Handle);
        Assert.AreEqual("sam@example.com", loaded.Email);
        Assert.AreEqual("platform", loaded.DefaultOwner);
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
            "work/done",
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
        var destination = Path.Combine("work", "done", "TASK-0001-move-target.md");

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
                - </specs/spec-a.md>
                - /specs/spec-a.md
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
        CollectionAssert.AreEqual(new[] { "/specs/spec-a.md" }, normalized.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "https://github.com/octo/demo/pull/1" }, normalized.Related.Prs.ToArray());
        Assert.IsEmpty(normalized.Related.Branches);
        Assert.IsEmpty(normalized.Related.Adrs);
        Assert.IsEmpty(normalized.Related.Files);
        Assert.IsEmpty(normalized.Related.Issues);

        var content = File.ReadAllText(itemPath);
        StringAssert.Contains(content, "tags: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "- /specs/spec-a.md", StringComparison.Ordinal);
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
                - </specs/spec-a.md>
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

        var normalized = WorkItemService.AddRelatedLink(itemPath, "specs", "/specs/spec-a.md");
        var duplicate = WorkItemService.AddRelatedLink(itemPath, "specs", "/specs/spec-a.md");
        var removed = WorkItemService.RemoveRelatedLink(itemPath, "specs", "/SPECS/SPEC-A.MD");

        Assert.IsTrue(normalized);
        Assert.IsFalse(duplicate);
        Assert.IsTrue(removed);

        var content = File.ReadAllText(itemPath);
        Assert.IsFalse(content.Contains("</specs/spec-a.md>", StringComparison.Ordinal), content);
        Assert.IsFalse(content.Contains("/specs/spec-a.md", StringComparison.Ordinal), content);
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
                - /specs/spec-a.md
                - /specs/spec-b.md
              adrs: []
              files:
                - /architecture/file-a.md
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
                ["/specs/spec-b.md"] = "/specs/spec-a.md",
                ["/architecture/file-a.md"] = "/architecture/file-b.md"
            });

        Assert.IsTrue(changed);
        var updated = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.AreEqual(new[] { "/specs/spec-a.md" }, updated.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "/architecture/file-b.md" }, updated.Related.Files.ToArray());
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
                - </specs/spec-a.md>
                - /specs/spec-a.md
              adrs:
                - </decisions/adr-a.md>
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
        CollectionAssert.AreEqual(new[] { "/specs/spec-a.md" }, normalized.Related.Specs.ToArray());
        CollectionAssert.AreEqual(new[] { "/decisions/adr-a.md" }, normalized.Related.Adrs.ToArray());
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
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "work", "items"));
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "work", "done"));
            Directory.CreateDirectory(System.IO.Path.Combine(this.Path, "work", "templates"));
        }

        public string Path { get; }

        public string WriteItem(string fileName, string content)
        {
            var path = System.IO.Path.Combine(this.Path, "work", "items", fileName);
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
            var templatePath = System.IO.Path.Combine(this.Path, "work", "templates", "work-item.task.md");
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
