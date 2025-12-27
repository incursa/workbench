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
}
