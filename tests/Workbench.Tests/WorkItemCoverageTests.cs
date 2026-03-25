using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class WorkItemCoverageTests
{
    [TestMethod]
    public void CanonicalLifecycle_CoversCreateDraftIssueStatusRenameMoveAndLookup()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var fromIssue = WorkItemService.CreateItemFromGithubIssue(
            repo.Path,
            WorkbenchConfig.Default,
            CreateIssue("Issue-seeded work item", "Body from issue", "https://github.com/octo/demo/issues/11"),
            "work_item",
            "planned",
            null,
            null);
        Assert.AreEqual("Issue-seeded work item", fromIssue.Title);

        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Coverage baseline item",
            "planned",
            null,
            null);

        var draftApplied = WorkItemService.ApplyDraft(
            created.Path,
            new WorkItemDraft(
                "ignored",
                "Updated summary from draft.",
                new List<string> { "first criterion", "- second criterion" },
                "work_item",
                new List<string>()));
        StringAssert.Contains(draftApplied.Body, "Updated summary from draft.", StringComparison.Ordinal);
        StringAssert.Contains(draftApplied.Body, "## Verification Plan", StringComparison.Ordinal);
        StringAssert.Contains(draftApplied.Body, "- first criterion", StringComparison.Ordinal);
        StringAssert.Contains(draftApplied.Body, "- second criterion", StringComparison.Ordinal);

        var beforeIssueUpdate = File.ReadAllText(created.Path);
        var issue = CreateIssue(
            "Updated from GitHub",
            "Issue line 1\\nIssue line 2",
            "https://github.com/octo/demo/issues/42");
        _ = WorkItemService.UpdateItemFromGithubIssue(created.Path, issue, apply: false);
        var afterDryRunUpdate = File.ReadAllText(created.Path);
        Assert.AreEqual(beforeIssueUpdate, afterDryRunUpdate);

        var issueApplied = WorkItemService.UpdateItemFromGithubIssue(created.Path, issue, apply: true);
        Assert.AreEqual("Updated from GitHub", issueApplied.Title);
        StringAssert.Contains(issueApplied.Body, "Imported from GitHub issue: https://github.com/octo/demo/issues/42", StringComparison.Ordinal);
        StringAssert.Contains(issueApplied.Body, "Issue line 1", StringComparison.Ordinal);
        StringAssert.Contains(issueApplied.Body, "Issue line 2", StringComparison.Ordinal);

        var inProgress = WorkItemService.UpdateStatus(created.Path, "in-progress", "Done note.");
        Assert.AreEqual("in_progress", inProgress.Status);
        StringAssert.Contains(inProgress.Body, "## Completion Notes", StringComparison.Ordinal);
        StringAssert.Contains(inProgress.Body, "- Done note.", StringComparison.Ordinal);

        var closed = WorkItemService.Close(created.Path);
        Assert.AreEqual("complete", closed.Status);

        var renamed = WorkItemService.Rename(created.Path, "Renamed canonical item", WorkbenchConfig.Default, repo.Path);
        StringAssert.Contains(Path.GetFileName(renamed.Path), "renamed-canonical-item.md", StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(renamed.Path), renamed.Path);

        var moved = WorkItemService.Move(renamed.Path, "specs/work-items/WB/moved-canonical-item.md", repo.Path);
        StringAssert.Contains(Path.GetFileName(moved.Path), "moved-canonical-item.md", StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(moved.Path), moved.Path);

        var pathById = WorkItemService.GetItemPathById(repo.Path, WorkbenchConfig.Default, moved.Id);
        var pathByArtifactId = WorkItemService.GetItemPathById(repo.Path, WorkbenchConfig.Default, moved.ArtifactId);
        Assert.AreEqual(Path.GetFullPath(moved.Path), Path.GetFullPath(pathById));
        Assert.AreEqual(Path.GetFullPath(moved.Path), Path.GetFullPath(pathByArtifactId));

        var listed = WorkItemService.ListItems(repo.Path, WorkbenchConfig.Default, includeDone: true).Items;
        Assert.IsTrue(listed.Any(item => string.Equals(item.Id, moved.Id, StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(listed.Any(item => string.Equals(item.Id, fromIssue.Id, StringComparison.OrdinalIgnoreCase)));

        Assert.IsFalse(WorkItemService.AddRelatedLink(moved.Path, "specs", "/specs/example.md"));
        Assert.IsFalse(WorkItemService.RemoveRelatedLink(moved.Path, "specs", "/specs/example.md"));
        Assert.IsFalse(WorkItemService.ReplaceRelatedLinks(moved.Path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/specs/example.md"] = "/specs/replaced.md"
        }));
        Assert.IsFalse(WorkItemService.UpdateGithubSynced(moved.Path, DateTime.UtcNow));
    }

    [TestMethod]
    public void CanonicalNormalize_CoversNormalizeItemsAndNormalizeRelatedLinks()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var created = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Normalize me",
            "planned",
            null,
            null);

        RewriteFrontMatter(created.Path, (data, body) =>
        {
            _ = body;
            data["status"] = "in-progress";
            _ = data.Remove("owner");
            data["addresses"] = new List<object?> { " <REQ-WB-0001> ", "REQ-WB-0001", string.Empty };
            data["design_links"] = new List<object?>();
            data["verification_links"] = new List<object?>();
            data["related_artifacts"] = new List<object?>();
        });

        var normalizedItems = WorkItemService.NormalizeItems(repo.Path, WorkbenchConfig.Default, includeDone: true, dryRun: false);
        Assert.AreEqual(1, normalizedItems);

        var afterNormalizeItems = WorkItemService.LoadItem(created.Path);
        Assert.IsNotNull(afterNormalizeItems);
        Assert.AreEqual("in_progress", afterNormalizeItems.Status);
        Assert.AreEqual("platform", afterNormalizeItems.Owner);
        Assert.IsGreaterThanOrEqualTo(afterNormalizeItems.Addresses.Count, 1);
        Assert.IsGreaterThanOrEqualTo(afterNormalizeItems.DesignLinks.Count, 1);
        Assert.IsGreaterThanOrEqualTo(afterNormalizeItems.VerificationLinks.Count, 1);
        Assert.IsGreaterThanOrEqualTo(afterNormalizeItems.RelatedArtifacts.Count, 1);

        RewriteFrontMatter(created.Path, (data, body) =>
        {
            _ = body;
            data["addresses"] = new List<object?> { "REQ-WB-0001", "req-wb-0001", " <REQ-WB-0002> " };
        });

        var normalizedRelated = WorkItemService.NormalizeRelatedLinks(repo.Path, WorkbenchConfig.Default, includeDone: true, dryRun: false);
        Assert.AreEqual(1, normalizedRelated);

        var afterNormalizeRelated = WorkItemService.LoadItem(created.Path);
        Assert.IsNotNull(afterNormalizeRelated);
        Assert.HasCount(2, afterNormalizeRelated.Addresses);
        Assert.AreEqual("REQ-WB-0001", afterNormalizeRelated.Addresses[0]);
        Assert.AreEqual("REQ-WB-0002", afterNormalizeRelated.Addresses[1]);
    }

    [TestMethod]
    public void LegacyLinkMutations_CoverAddRemoveReplaceAndGithubSynced()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var legacyPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-0001-legacy.md");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, new FrontMatter(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "TASK-0001",
                ["type"] = "task",
                ["status"] = "planned",
                ["title"] = "Legacy item",
                ["related"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["specs"] = new List<object?> { "<specs/beta.md>" },
                    ["files"] = new List<object?> { "src/Workbench.Core/WorkItemService.cs" },
                    ["prs"] = new List<object?>(),
                    ["issues"] = new List<object?>(),
                    ["branches"] = new List<object?>()
                }
            },
            """
            # TASK-0001 - Legacy item

            ## Summary
            Legacy path for related-link mutations.
            """).Serialize());

        Assert.IsTrue(WorkItemService.AddRelatedLink(legacyPath, "specs", "<specs/alpha.md>"));
        Assert.IsTrue(WorkItemService.AddRelatedLink(legacyPath, "specs", "specs/beta.md"));
        Assert.IsTrue(WorkItemService.RemoveRelatedLink(legacyPath, "specs", "<specs/alpha.md>"));

        var replaced = WorkItemService.ReplaceRelatedLinks(
            legacyPath,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["specs/beta.md"] = "specs/delta.md"
            });
        Assert.IsTrue(replaced);

        var firstSynced = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(WorkItemService.UpdateGithubSynced(legacyPath, firstSynced, apply: true));
        Assert.IsFalse(WorkItemService.UpdateGithubSynced(legacyPath, firstSynced, apply: true));

        var legacyContent = File.ReadAllText(legacyPath);
        StringAssert.Contains(legacyContent, "specs/delta.md", StringComparison.Ordinal);
        StringAssert.Contains(legacyContent, "githubSynced: \"2026-03-24T12:00:00Z\"", StringComparison.Ordinal);
    }

    private static void RewriteFrontMatter(string path, Action<IDictionary<string, object?>, string> mutate)
    {
        var content = File.ReadAllText(path);
        Assert.IsTrue(FrontMatter.TryParse(content, out var frontMatter, out var error), error ?? "front matter parse failed");
        mutate(frontMatter!.Data, frontMatter.Body);
        File.WriteAllText(path, frontMatter.Serialize());
    }

    private static GithubIssue CreateIssue(string title, string body, string url)
    {
        return new GithubIssue(
            new GithubRepoRef("github.com", "octo", "demo"),
            42,
            title,
            body,
            url,
            "open",
            new List<string> { "quality" },
            new List<string> { "https://github.com/octo/demo/pull/77" });
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

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
