namespace Workbench.Providers.ClickUp.Tests;

[TestClass]
public sealed class ClickUpProviderOptionsTests
{
    [TestMethod]
    public void ParseRequiresDurableStateForMutationModes()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ClickUpProviderOptions.Parse(
            ["--list-id", "42", "--authority-epoch", "epoch-1", "--mode", "apply"]));

        StringAssert.Contains(exception.Message, "--state-directory", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ParseRejectsTokenValuesAndNonLoopbackApiOverrides()
    {
        var tokenException = Assert.Throws<InvalidOperationException>(() => ClickUpProviderOptions.Parse(
            ["--list-id", "42", "--authority-epoch", "epoch-1", "--token", "pk_secret"]));
        var endpointException = Assert.Throws<InvalidOperationException>(() => ClickUpProviderOptions.Parse(
            [
                "--list-id", "42",
                "--authority-epoch", "epoch-1",
                "--api-base-uri", "https://example.invalid/api/v2/"
            ]));

        Assert.DoesNotContain("pk_secret", tokenException.Message, StringComparison.Ordinal);
        StringAssert.Contains(endpointException.Message, "official ClickUp endpoint", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ParseAllowsLoopbackMockEndpoint()
    {
        var options = ClickUpProviderOptions.Parse(
            [
                "--list-id", "42",
                "--authority-epoch", "epoch-1",
                "--api-base-uri", "http://127.0.0.1:5123/api/v2"
            ]);

        Assert.AreEqual("http://127.0.0.1:5123/api/v2/", options.ApiBaseUri.AbsoluteUri);
    }
}
