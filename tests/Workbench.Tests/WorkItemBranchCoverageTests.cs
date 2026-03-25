using System.Linq;
using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class WorkItemBranchCoverageTests
{
    [TestMethod]
    public void LegacyEditAndIssueImport_CoversStatusEditRenameAndImportBranches()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var legacyPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-0001-legacy-branch.md");
        WriteFrontMatter(
            legacyPath,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "TASK-0001",
                ["type"] = "task",
                ["status"] = "planned",
                ["title"] = "Legacy branch item",
                ["priority"] = "low",
                ["owner"] = "legacy-owner",
                ["related"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["specs"] = new List<object?> { " <specs/alpha.md> ", "specs/alpha.md" },
                    ["files"] = new List<object?> { "src/Workbench.Core/WorkItemService.cs", "src/Workbench.Core/WorkItemService.cs" },
                    ["prs"] = new List<object?> { "https://github.com/incursa/workbench/pull/1" },
                    ["issues"] = new List<object?>(),
                    ["branches"] = new List<object?>()
                }
            },
            """
            # TASK-0001 - Legacy branch item

            ## Summary
            Legacy summary.
            """);

        var statusUpdated = WorkItemService.UpdateStatus(legacyPath, "in-progress", "Legacy completion note.");
        StringAssert.Contains(statusUpdated.Body, "## Notes", StringComparison.Ordinal);
        StringAssert.Contains(statusUpdated.Body, "- Legacy completion note.", StringComparison.Ordinal);

        var edited = WorkItemService.EditItem(
            statusUpdated.Path,
            "Updated Legacy Branch Item",
            "Updated legacy summary",
            new List<string> { "first criterion", "second criterion" },
            "Added edit note",
            true,
            WorkbenchConfig.Default,
            repo.Path,
            status: "blocked",
            priority: string.Empty,
            owner: string.Empty);

        Assert.IsTrue(edited.PathChanged);
        Assert.IsTrue(edited.TitleUpdated);
        Assert.IsTrue(edited.SummaryUpdated);
        Assert.IsTrue(edited.AcceptanceCriteriaUpdated);
        Assert.IsTrue(edited.NotesAppended);
        Assert.AreEqual("Updated Legacy Branch Item", edited.Item.Title);
        Assert.IsNull(edited.Item.Priority);
        Assert.IsNull(edited.Item.Owner);
        Assert.AreEqual("blocked", edited.Item.Status);
        StringAssert.Contains(edited.Item.Body, "## Acceptance criteria", StringComparison.Ordinal);
        StringAssert.Contains(edited.Item.Body, "- first criterion", StringComparison.Ordinal);
        StringAssert.Contains(edited.Item.Body, "- Added edit note", StringComparison.Ordinal);
        StringAssert.Contains(Path.GetFileName(edited.Item.Path), "updated-legacy-branch-item.md", StringComparison.Ordinal);

        var issue = CreateIssue(
            "Imported from GitHub",
            "Imported line 1\\nImported line 2",
            "https://github.com/octo/demo/issues/42",
            new List<string> { "quality", "quality" },
            new List<string>
            {
                "https://github.com/octo/demo/pull/77",
                "https://github.com/octo/demo/pull/77"
            });

        var beforePreview = File.ReadAllText(edited.Item.Path);
        var preview = WorkItemService.UpdateItemFromGithubIssue(edited.Item.Path, issue, apply: false);
        Assert.AreEqual(beforePreview, File.ReadAllText(edited.Item.Path));
        Assert.AreEqual("Updated Legacy Branch Item", preview.Title);

        var applied = WorkItemService.UpdateItemFromGithubIssue(edited.Item.Path, issue, apply: true);
        StringAssert.Contains(applied.Body, "Imported from GitHub issue:", StringComparison.Ordinal);
        StringAssert.Contains(applied.Body, "Imported line 1", StringComparison.Ordinal);
        StringAssert.Contains(applied.Body, "Imported line 2", StringComparison.Ordinal);
        Assert.AreEqual("Imported from GitHub", applied.Title);
        Assert.AreEqual(1, applied.Related.Issues.Count(link => link.Equals(issue.Url, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(1, applied.Related.Prs.Count(link => link.Equals(issue.PullRequests[0], StringComparison.OrdinalIgnoreCase)));
        Assert.HasCount(2, applied.Tags);
        Assert.IsNotNull(applied.GithubSynced);

        var syncedTimestamp = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc);
        var beforeSync = File.ReadAllText(edited.Item.Path);
        Assert.IsTrue(WorkItemService.UpdateGithubSynced(edited.Item.Path, syncedTimestamp, apply: false));
        Assert.AreEqual(beforeSync, File.ReadAllText(edited.Item.Path));

        ExpectException<InvalidOperationException>(() => WorkItemService.EditItem(
            edited.Item.Path,
            null,
            null,
            null,
            null,
            false,
            WorkbenchConfig.Default,
            repo.Path));
    }

    [TestMethod]
    public void CanonicalDraftRenameAndLoadFailures_CoversCanonicalBranches()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var canonical = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Canonical branch item",
            "planned",
            null,
            null);

        var draftApplied = WorkItemService.ApplyEditDraft(
            canonical.Path,
            new WorkItemDraft(
                "Canonical branch item revised",
                "Canonical summary",
                new List<string> { "first canonical criterion", "- second canonical criterion" },
                null,
                null));

        Assert.AreEqual("Canonical branch item revised", draftApplied.Title);
        StringAssert.Contains(draftApplied.Body, "## Summary", StringComparison.Ordinal);
        StringAssert.Contains(draftApplied.Body, "## Verification Plan", StringComparison.Ordinal);
        StringAssert.Contains(draftApplied.Body, "- first canonical criterion", StringComparison.Ordinal);

        ExpectException<InvalidOperationException>(() => WorkItemService.ApplyEditDraft(
            draftApplied.Path,
            new WorkItemDraft("Broken title", " ", null, null, null)));

        var canonicalBefore = File.ReadAllText(draftApplied.Path);
        WorkItemService.AddPrLink(draftApplied.Path, "https://github.com/incursa/workbench/pull/99");
        Assert.AreEqual(canonicalBefore, File.ReadAllText(draftApplied.Path));

        var renamed = WorkItemService.Rename(draftApplied.Path, draftApplied.Title, WorkbenchConfig.Default, repo.Path);
        Assert.AreNotEqual(Path.GetFullPath(draftApplied.Path), Path.GetFullPath(renamed.Path));
        Assert.AreEqual("canonical-branch-item-revised.md", Path.GetFileName(renamed.Path));
        Assert.IsTrue(File.Exists(renamed.Path));

        var superseded = WorkItemService.UpdateStatus(renamed.Path, "superseded", "Superseded note.");
        Assert.AreEqual("superseded", superseded.Status);
        StringAssert.Contains(superseded.Body, "## Completion Notes", StringComparison.Ordinal);
        StringAssert.Contains(superseded.Body, "- Superseded note.", StringComparison.Ordinal);

        var readmePath = Path.Combine(repo.Path, "specs", "work-items", "WB", "README.md");
        File.WriteAllText(readmePath, "# README");
        Assert.IsNull(WorkItemService.LoadItem(readmePath));

        var malformedPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "broken.md");
        File.WriteAllText(malformedPath, "not front matter");
        Assert.IsNull(WorkItemService.LoadItem(malformedPath));

        var longSlug = WorkItemService.Slugify(new string('a', 120) + " !!!");
        Assert.AreEqual(80, longSlug.Length);
        Assert.IsFalse(longSlug.EndsWith("-", StringComparison.Ordinal));

        ExpectException<FileNotFoundException>(() => WorkItemService.GetItemPathById(repo.Path, WorkbenchConfig.Default, "WI-WB-9999"));
    }

    [TestMethod]
    public void NormalizationAliasesAndListHelpers_CoverLegacyAndCanonicalNormalization()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var canonicalPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "WI-WB-9000-branch-coverage.md");
        WriteFrontMatter(
            canonicalPath,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["artifact_id"] = "WI-WB-9000",
                ["artifact_type"] = "work_item",
                ["title"] = "Canonical branch item",
                ["domain"] = "WB",
                ["status"] = "planned",
                ["owner"] = "platform",
                ["addresses"] = new List<object?>(),
                ["design_links"] = new List<object?>(),
                ["verification_links"] = new List<object?>(),
                ["related_artifacts"] = new List<object?>()
            },
            """
            # WI-WB-9000 - Canonical branch item

            ## Summary
            Canonical summary.

            ## Requirements Addressed
            -

            ## Design Inputs
            -

            ## Planned Changes
            -

            ## Out of Scope
            -

            ## Verification Plan
            -

            ## Completion Notes
            -

            ## Trace Links
            Addresses:
            - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
            Uses Design:
            - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
            """);

        var legacyPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-9001-legacy-branch.md");
        WriteFrontMatter(
            legacyPath,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "TASK-9001",
                ["type"] = "task",
                ["status"] = "planned",
                ["priority"] = "medium",
                ["owner"] = "legacy-owner",
                ["tags"] = "legacy-tag",
                ["related"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["specs"] = new List<object?> { " <specs/alpha.md> ", "specs/alpha.md" },
                    ["files"] = new List<object?> { "src/Workbench.Core/WorkItemService.cs", "src/Workbench.Core/WorkItemService.cs" },
                    ["prs"] = new List<object?> { "https://github.com/incursa/workbench/pull/2", "https://github.com/incursa/workbench/pull/2" },
                    ["issues"] = new List<object?>(),
                    ["branches"] = new List<object?> { "work/task-9001", "work/task-9001" }
                }
            },
            """
            # TASK-9001 - Legacy branch item

            Intro paragraph.

            ## Context
            Legacy design input.

            ## Traceability
            - REQ-WB-9001

            ## Implementation notes
            Legacy implementation notes.

            ## Acceptance Criteria
            - legacy criterion

            ## Notes
            Legacy note

            ## Appendix
            - extra legacy content
            """);

        var updatedCount = WorkItemService.NormalizeItems(repo.Path, WorkbenchConfig.Default, includeDone: true, dryRun: false);
        Assert.IsGreaterThanOrEqualTo(1, updatedCount);

        var canonical = WorkItemService.LoadItem(canonicalPath) ?? throw new InvalidOperationException("Canonical item did not load.");
        Assert.HasCount(1, canonical.Addresses);
        Assert.AreEqual("REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", canonical.Addresses[0]);
        Assert.AreEqual("ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", canonical.DesignLinks[0]);
        Assert.AreEqual("VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>", canonical.VerificationLinks[0]);
        Assert.AreEqual("SPEC-<DOMAIN>[-<GROUPING>...]", canonical.RelatedArtifacts[0]);
        StringAssert.Contains(canonical.Body, "## Summary", StringComparison.Ordinal);
        StringAssert.Contains(canonical.Body, "## Trace Links", StringComparison.Ordinal);

        var legacy = WorkItemService.LoadItem(legacyPath) ?? throw new InvalidOperationException("Legacy item did not load.");
        Assert.AreEqual("legacy branch", legacy.Title);
        Assert.AreEqual("legacy-branch", legacy.Slug);
        Assert.IsEmpty(legacy.Tags);
        Assert.HasCount(1, legacy.Related.Specs);
        Assert.AreEqual("specs/alpha.md", legacy.Related.Specs[0]);
        Assert.HasCount(1, legacy.Related.Files);
        Assert.AreEqual("src/Workbench.Core/WorkItemService.cs", legacy.Related.Files[0]);
        Assert.HasCount(1, legacy.Related.Prs);
        Assert.AreEqual("https://github.com/incursa/workbench/pull/2", legacy.Related.Prs[0]);
        Assert.HasCount(1, legacy.Related.Branches);
        Assert.AreEqual("work/task-9001", legacy.Related.Branches[0]);
        Assert.IsNull(legacy.GithubSynced);
        StringAssert.Contains(legacy.Body, "## Design Inputs", StringComparison.Ordinal);
        StringAssert.Contains(legacy.Body, "## Trace Links", StringComparison.Ordinal);
        StringAssert.Contains(legacy.Body, "## Planned Changes", StringComparison.Ordinal);
        StringAssert.Contains(legacy.Body, "## Verification Plan", StringComparison.Ordinal);
        StringAssert.Contains(legacy.Body, "## Completion Notes", StringComparison.Ordinal);
        StringAssert.Contains(legacy.Body, "## Appendix", StringComparison.Ordinal);
    }

    private static void WriteFrontMatter(string path, IDictionary<string, object?> data, string body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Missing directory."));
        File.WriteAllText(path, new FrontMatter(data, body).Serialize());
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private static GithubIssue CreateIssue(
        string title,
        string body,
        string url,
        IList<string> labels,
        IList<string> pullRequests)
    {
        return new GithubIssue(
            new GithubRepoRef("github.com", "octo", "demo"),
            42,
            title,
            body,
            url,
            "open",
            labels,
            pullRequests);
    }

    private static void ExpectException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
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
