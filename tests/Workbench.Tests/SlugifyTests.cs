using Workbench;

namespace Workbench.Tests;

[TestClass]
public class SlugifyTests
{
    [TestMethod]
    public void Slugify_NormalizesTitle()
    {
        var slug = WorkItemService.Slugify("Add promotion workflow!");
        Assert.AreEqual("add-promotion-workflow", slug);
    }

    [TestMethod]
    public void Slugify_RemovesSpecialCharacters()
    {
        var slug = WorkItemService.Slugify("Fix: API @ v2.0 (critical)");
        Assert.AreEqual("fix-api-v20-critical", slug);
    }

    [TestMethod]
    public void Slugify_CollapsesMultipleSpaces()
    {
        var slug = WorkItemService.Slugify("Update    config     file");
        Assert.AreEqual("update-config-file", slug);
    }

    [TestMethod]
    public void Slugify_TrimsLeadingAndTrailingPunctuation()
    {
        var slug = WorkItemService.Slugify("!!! Important fix !!!");
        Assert.AreEqual("important-fix", slug);
    }

    [TestMethod]
    public void Slugify_HandlesNumbersCorrectly()
    {
        var slug = WorkItemService.Slugify("Update to v2.1.0");
        Assert.AreEqual("update-to-v210", slug);
    }

    [TestMethod]
    public void Slugify_CollapsesMultipleHyphens()
    {
        var slug = WorkItemService.Slugify("Fix---this--problem");
        Assert.AreEqual("fix-this-problem", slug);
    }

    [TestMethod]
    public void Slugify_HandlesEmptyString()
    {
        var slug = WorkItemService.Slugify("");
        Assert.AreEqual("", slug);
    }

    [TestMethod]
    public void Slugify_HandlesOnlySpecialCharacters()
    {
        var slug = WorkItemService.Slugify("@#$%^&*()");
        Assert.AreEqual("", slug);
    }

    [TestMethod]
    public void Slugify_PreservesExistingHyphens()
    {
        var slug = WorkItemService.Slugify("Add user-profile feature");
        Assert.AreEqual("add-user-profile-feature", slug);
    }

    [TestMethod]
    public void Slugify_HandlesAlreadySlugifiedText()
    {
        var slug = WorkItemService.Slugify("add-promotion-workflow");
        Assert.AreEqual("add-promotion-workflow", slug);
    }

    [TestMethod]
    public void Slugify_HandlesUnicodeCharacters()
    {
        var slug = WorkItemService.Slugify("Add café menu");
        Assert.AreEqual("add-caf-menu", slug);
    }

    [TestMethod]
    public void Slugify_HandlesLeadingAndTrailingSpaces()
    {
        var slug = WorkItemService.Slugify("   Fix bug   ");
        Assert.AreEqual("fix-bug", slug);
    }

    [TestMethod]
    public void Slugify_HandlesMixedCaseWithNumbers()
    {
        var slug = WorkItemService.Slugify("Upgrade .NET 8 to .NET 10");
        Assert.AreEqual("upgrade-net-8-to-net-10", slug);
    }

    [TestMethod]
    public void Slugify_HandlesWhitespaceOnly()
    {
        var slug = WorkItemService.Slugify("    ");
        Assert.AreEqual("", slug);
    }
}
