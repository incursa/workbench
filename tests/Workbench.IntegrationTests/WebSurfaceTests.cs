using System.Net;
using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public sealed class WebSurfaceTests
{
    [TestMethod]
    public async Task LocalWebUi_ServesPrimaryPagesAndEmbeddedAssetsAsync()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);

        await using var app = Workbench.WorkbenchWebHost.BuildApplication(
            repo.Path,
            config,
            "http://127.0.0.1:0");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var expectation in new[]
            {
                (Path: "/", Marker: "Manage repo-native work items"),
                (Path: "/Dashboard", Marker: "Manage repo-native work items"),
                (Path: "/Docs", Marker: "Manage architecture"),
                (Path: "/Specs", Marker: "Specifications"),
                (Path: "/Files", Marker: "Browse the repo"),
                (Path: "/Settings", Marker: "Store a machine-local"),
                (Path: "/Create", Marker: "Create a repo-native item")
            })
            {
                using var response = await client.GetAsync(expectation.Path);
                var body = await response.Content.ReadAsStringAsync();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"GET {expectation.Path}\n{body}");
                StringAssert.Contains(body, expectation.Marker, StringComparison.OrdinalIgnoreCase);
            }

            using var assetResponse = await client.GetAsync("/css/site.css");
            Assert.AreEqual(HttpStatusCode.OK, assetResponse.StatusCode);
            Assert.AreEqual("text/css", assetResponse.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
