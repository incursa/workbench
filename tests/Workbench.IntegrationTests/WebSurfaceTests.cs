using System.Net;
using System.Text.RegularExpressions;
using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public sealed class WebSurfaceTests
{
    private static readonly Regex requestVerificationTokenInputRegex = new(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex inputValueRegex = new(
        "value=\"(?<token>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

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

    [TestMethod]
    public async Task LocalWebUi_CreateEditAndValidateWorkItemThroughHttpAsync()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var workspace = new Workbench.WorkbenchWorkspace(repo.Path, config);

        await using var app = Workbench.WorkbenchWebHost.BuildApplication(
            repo.Path,
            config,
            "http://127.0.0.1:0");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            var invalidToken = await GetRequestVerificationTokenAsync(client, "/Create");
            using var invalidResponse = await client.PostAsync(
                "/Create",
                Form(
                    invalidToken,
                    ("Create.Type", "work_item"),
                    ("Create.Title", string.Empty),
                    ("Create.Status", "planned"),
                    ("Create.Priority", "high"),
                    ("Create.Owner", "platform")));
            Assert.AreEqual(HttpStatusCode.OK, invalidResponse.StatusCode);
            Assert.IsEmpty(workspace.ListItems(includeDone: true, statusFilter: null, query: null));

            var createToken = await GetRequestVerificationTokenAsync(client, "/Create");
            using var createResponse = await client.PostAsync(
                "/Create",
                Form(
                    createToken,
                    ("Create.Type", "work_item"),
                    ("Create.Title", "HTTP managed item"),
                    ("Create.Status", "planned"),
                    ("Create.Priority", "high"),
                    ("Create.Owner", "platform")));
            Assert.AreEqual(HttpStatusCode.OK, createResponse.StatusCode);

            var created = workspace.ListItems(includeDone: true, statusFilter: null, query: null).Single();
            Assert.AreEqual("HTTP managed item", created.Title);

            var editPath = $"/?selectedId={Uri.EscapeDataString(created.Id)}";
            var editToken = await GetRequestVerificationTokenAsync(client, editPath);
            using var editResponse = await client.PostAsync(
                "/?handler=Save",
                Form(
                    editToken,
                    ("SelectedId", created.Id),
                    ("StatusFilter", "all"),
                    ("Edit.Path", created.Path),
                    ("Edit.Title", "HTTP managed item updated"),
                    ("Edit.Status", "in_progress"),
                    ("Edit.Priority", "critical"),
                    ("Edit.Owner", "web-team"),
                    ("Edit.Summary", "Edited through the production Razor Page handler."),
                    ("Edit.AcceptanceCriteria", "The item is persisted."),
                    ("Edit.AppendNote", "HTTP edit verified"),
                    ("Edit.RenameFile", "false")));
            Assert.AreEqual(HttpStatusCode.OK, editResponse.StatusCode);

            var updated = workspace.GetItem(created.Id, includeDone: true);
            Assert.IsNotNull(updated);
            Assert.AreEqual("HTTP managed item updated", updated.Title);
            Assert.AreEqual("in_progress", updated.Status);
            Assert.AreEqual("critical", updated.Priority);
            Assert.AreEqual("web-team", updated.Owner);
            StringAssert.Contains(updated.Body, "Edited through the production Razor Page handler.", StringComparison.Ordinal);

            var validateToken = await GetRequestVerificationTokenAsync(client, editPath);
            using var validateResponse = await client.PostAsync(
                "/?handler=Validate",
                Form(
                    validateToken,
                    ("SelectedId", created.Id),
                    ("StatusFilter", "all")));
            var validateBody = await validateResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, validateResponse.StatusCode);
            StringAssert.Contains(validateBody, "Validation complete", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [TestMethod]
    public async Task LocalWebUi_RequirementEditorValidatesAndPersistsTargetedChangesAsync()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var workspace = new Workbench.WorkbenchWorkspace(repo.Path, config);
        var createdSpec = workspace.CreateSpec(new Workbench.SpecEditorInput
        {
            ArtifactId = "SPEC-WEB-INTERACTION",
            Domain = "WEB",
            Capability = "interaction",
            Title = "Web interaction contract",
            Status = "draft",
            Owner = "platform",
            Purpose = "Exercise requirement editor behavior.",
            Scope = "Local HTTP tests.",
            Context = "The production Razor Page handlers are the contract under test.",
            Requirements =
            [
                new Workbench.SpecRequirementEditorInput
                {
                    Id = "REQ-WEB-INTERACTION-0001",
                    Title = "First requirement",
                    Statement = "The editor MUST preserve targeted changes."
                },
                new Workbench.SpecRequirementEditorInput
                {
                    Id = "REQ-WEB-INTERACTION-0002",
                    Title = "Second requirement",
                    Statement = "The editor MUST preserve unrelated requirements."
                }
            ]
        });

        await using var app = Workbench.WorkbenchWebHost.BuildApplication(
            repo.Path,
            config,
            "http://127.0.0.1:0");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            const string SpecId = "SPEC-WEB-INTERACTION";
            const string FirstRequirementId = "REQ-WEB-INTERACTION-0001";
            const string SecondRequirementId = "REQ-WEB-INTERACTION-0002";
            var firstEditorPath = RequirementEditorPath(SpecId, FirstRequirementId);
            var specPath = Path.Combine(repo.Path, createdSpec.Summary.Path);
            var originalContent = await File.ReadAllTextAsync(specPath);

            var invalidToken = await GetRequestVerificationTokenAsync(client, firstEditorPath);
            using var invalidResponse = await client.PostAsync(
                "/Specs?handler=SaveRequirement",
                RequirementForm(
                    invalidToken,
                    SpecId,
                    FirstRequirementId,
                    "0001",
                    "Invalid draft remains visible",
                    "This statement has no normative keyword."));
            var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, invalidResponse.StatusCode);
            StringAssert.Contains(invalidBody, "Invalid draft remains visible", StringComparison.Ordinal);
            Assert.AreEqual(originalContent, await File.ReadAllTextAsync(specPath));

            var saveToken = await GetRequestVerificationTokenAsync(client, firstEditorPath);
            using var saveResponse = await client.PostAsync(
                "/Specs?handler=SaveRequirement",
                RequirementForm(
                    saveToken,
                    SpecId,
                    FirstRequirementId,
                    "0001",
                    "Updated first requirement",
                    "The production handler MUST update only the selected requirement."));
            Assert.AreEqual(HttpStatusCode.OK, saveResponse.StatusCode);

            var afterSave = workspace.CreateSpecEditorInput(SpecId);
            Assert.IsNotNull(afterSave);
            Assert.AreEqual("Updated first requirement", afterSave.Requirements[0].Title);
            Assert.AreEqual("Second requirement", afterSave.Requirements[1].Title);

            var newEditorPath = RequirementEditorPath(SpecId, "new");
            var addToken = await GetRequestVerificationTokenAsync(client, newEditorPath);
            using var addResponse = await client.PostAsync(
                "/Specs?handler=SaveRequirement",
                RequirementForm(
                    addToken,
                    SpecId,
                    "new",
                    "0003",
                    "Third requirement",
                    "The production handler MUST append a valid requirement."));
            Assert.AreEqual(HttpStatusCode.OK, addResponse.StatusCode);

            var afterAdd = workspace.CreateSpecEditorInput(SpecId);
            Assert.IsNotNull(afterAdd);
            Assert.HasCount(3, afterAdd.Requirements);
            Assert.IsTrue(afterAdd.Requirements.Any(requirement => string.Equals(requirement.Id, "REQ-WEB-INTERACTION-0003", StringComparison.Ordinal)));

            var deleteToken = await GetRequestVerificationTokenAsync(
                client,
                RequirementEditorPath(SpecId, SecondRequirementId));
            using var deleteResponse = await client.PostAsync(
                "/Specs?handler=DeleteSelectedRequirement",
                Form(
                    deleteToken,
                    ("SelectedReference", SpecId),
                    ("SelectedRequirementReference", SecondRequirementId),
                    ("RequirementPage", "1"),
                    ("RequirementPageSize", "50")));
            Assert.AreEqual(HttpStatusCode.OK, deleteResponse.StatusCode);

            var afterDelete = workspace.CreateSpecEditorInput(SpecId);
            Assert.IsNotNull(afterDelete);
            Assert.HasCount(2, afterDelete.Requirements);
            Assert.IsFalse(afterDelete.Requirements.Any(requirement => string.Equals(requirement.Id, SecondRequirementId, StringComparison.Ordinal)));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<string> GetRequestVerificationTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"GET {path}\n{body}");
        var inputMatch = requestVerificationTokenInputRegex.Match(body);
        Assert.IsTrue(inputMatch.Success, $"Antiforgery input was not rendered by GET {path}.");
        var valueMatch = inputValueRegex.Match(inputMatch.Value);
        Assert.IsTrue(valueMatch.Success, $"Antiforgery value was not rendered by GET {path}.");
        return WebUtility.HtmlDecode(valueMatch.Groups["token"].Value);
    }

    private static FormUrlEncodedContent RequirementForm(
        string token,
        string specId,
        string selectedRequirementReference,
        string idSuffix,
        string title,
        string statement)
    {
        return Form(
            token,
            ("SelectedReference", specId),
            ("SelectedRequirementReference", selectedRequirementReference),
            ("RequirementPage", "1"),
            ("RequirementPageSize", "50"),
            ("RequirementEdit.IdSuffix", idSuffix),
            ("RequirementEdit.Title", title),
            ("RequirementEdit.Statement", statement),
            ("RequirementEdit.NotesText", string.Empty),
            ("RequirementEdit.Trace.SatisfiedByText", string.Empty),
            ("RequirementEdit.Trace.ImplementedByText", string.Empty),
            ("RequirementEdit.Trace.VerifiedByText", string.Empty),
            ("RequirementEdit.Trace.DerivedFromText", string.Empty),
            ("RequirementEdit.Trace.SupersedesText", string.Empty),
            ("RequirementEdit.Trace.UpstreamRefsText", string.Empty),
            ("RequirementEdit.Trace.RelatedText", string.Empty));
    }

    private static FormUrlEncodedContent Form(string token, params (string Name, string Value)[] fields)
    {
        return new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("__RequestVerificationToken", token) }
                .Concat(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))));
    }

    private static string RequirementEditorPath(string specId, string requirementReference)
    {
        return $"/Specs?selectedReference={Uri.EscapeDataString(specId)}" +
            $"&selectedRequirementReference={Uri.EscapeDataString(requirementReference)}";
    }
}
