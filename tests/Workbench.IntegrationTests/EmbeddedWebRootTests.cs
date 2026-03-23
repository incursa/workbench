namespace Workbench.IntegrationTests;

[TestClass]
public class EmbeddedWebRootTests
{
    [TestMethod]
    public void ApplicationAssemblyContainsEmbeddedWebRootManifest()
    {
        var manifestResource = typeof(Program).Assembly.GetManifestResourceInfo(
            "Microsoft.Extensions.FileProviders.Embedded.Manifest.xml");

        Assert.IsNotNull(manifestResource);
    }
}
