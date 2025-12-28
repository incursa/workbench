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
    public void Slugify_TruncatesLongTitles()
    {
        var slug = WorkItemService.Slugify(new string('A', 120) + " trailing");
        Assert.IsTrue(slug.Length <= 80);
        Assert.IsFalse(slug.EndsWith("-", StringComparison.Ordinal));
    }
}
