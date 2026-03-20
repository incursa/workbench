using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class GithubIssueLinkerTests
{
    [TestMethod]
    public void BuildBody_WithGithubContext_AppendsWorkbenchItemMarker()
    {
        var item = CreateItem(id: "TASK-0001", title: "Track sync marker");

        var body = PullRequestBuilder.BuildBody(
            item,
            new GithubRepoRef("github.com", "octo", "demo"),
            "main");

        StringAssert.Contains(body, "<!-- workbench:item TASK-0001 -->", StringComparison.Ordinal);
        Assert.IsTrue(GithubIssueLinker.TryExtractWorkbenchItemId(body, out var itemId));
        Assert.AreEqual("TASK-0001", itemId);
    }

    [TestMethod]
    public void TryMatchIssueToItem_MatchesExistingLocalItemByBodyMarker()
    {
        var item = CreateItem(id: "TASK-0042", title: "Existing local item");
        var issue = new GithubIssue(
            new GithubRepoRef("github.com", "octo", "demo"),
            42,
            "Existing local item",
            "## Summary\n\nAlready created from Workbench.\n\n<!-- workbench:item TASK-0042 -->",
            "open",
            "https://github.com/octo/demo/issues/42",
            Array.Empty<string>(),
            Array.Empty<string>());

        var itemsById = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase)
        {
            [item.Id] = item
        };

        var matched = GithubIssueLinker.TryMatchIssueToItem(issue, itemsById, out var resolvedItem);

        Assert.IsTrue(matched);
        Assert.IsNotNull(resolvedItem);
        Assert.AreEqual(item.Id, resolvedItem.Id);
    }

    [TestMethod]
    public void TryBuildMarkerBackfillBody_AppendsMarkerWithoutRewritingLegacyBody()
    {
        var legacyBody = "## Summary\n\nLegacy issue body from before marker support.";

        var changed = GithubIssueLinker.TryBuildMarkerBackfillBody(legacyBody, "TASK-0042", out var updatedBody);

        Assert.IsTrue(changed);
        Assert.AreEqual($"{legacyBody}\n\n<!-- workbench:item TASK-0042 -->", updatedBody);
    }

    private static WorkItem CreateItem(string id, string title)
    {
        return new WorkItem(
            id,
            "task",
            "ready",
            title,
            null,
            null,
            "2026-03-19",
            null,
            new List<string>(),
            new RelatedLinks(
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>()),
            "sample-item",
            Path.Combine("docs", "70-work", "items", $"{id}-sample-item.md"),
            "## Summary\n\nLocal summary.");
    }
}
