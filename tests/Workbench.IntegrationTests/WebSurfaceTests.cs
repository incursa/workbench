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

    [TestMethod]
    public async Task LocalWebUi_RequirementEditorMovesAndSavesAllAtomicallyWithConcurrencyProtectionAsync()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var workspace = new Workbench.WorkbenchWorkspace(repo.Path, config);
        var createdSpec = workspace.CreateSpec(new Workbench.SpecEditorInput
        {
            ArtifactId = "SPEC-WEB-BATCH",
            Domain = "WEB",
            Capability = "batch",
            Title = "Batch requirement editing",
            Status = "draft",
            Owner = "platform",
            Purpose = "Exercise ordered atomic requirement edits.",
            Requirements =
            [
                Requirement("REQ-WEB-BATCH-0001", "First", "The editor MUST persist the first requirement."),
                Requirement("REQ-WEB-BATCH-0002", "Second", "The editor MUST persist the second requirement."),
                Requirement("REQ-WEB-BATCH-0003", "Third", "The editor MUST persist the third requirement.")
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
            const string SpecId = "SPEC-WEB-BATCH";
            const string FirstId = "REQ-WEB-BATCH-0001";
            const string SecondId = "REQ-WEB-BATCH-0002";
            const string ThirdId = "REQ-WEB-BATCH-0003";
            var pagePath = RequirementEditorPath(SpecId, SecondId);

            var moveEarlierToken = await GetRequestVerificationTokenAsync(client, pagePath);
            var moveEarlierRevision = await GetInputValueAsync(client, pagePath, "Edit.SourceRevision");
            using var moveEarlierResponse = await client.PostAsync(
                "/Specs?handler=MoveRequirement",
                MoveRequirementForm(moveEarlierToken, SpecId, moveEarlierRevision, SecondId, "earlier"));
            Assert.AreEqual(HttpStatusCode.OK, moveEarlierResponse.StatusCode);
            CollectionAssert.AreEqual(
                new[] { SecondId, FirstId, ThirdId },
                workspace.CreateSpecEditorInput(SpecId)!.Requirements.Select(requirement => requirement.Id).ToArray());

            var moveLaterToken = await GetRequestVerificationTokenAsync(client, pagePath);
            var moveLaterRevision = await GetInputValueAsync(client, pagePath, "Edit.SourceRevision");
            using var moveLaterResponse = await client.PostAsync(
                "/Specs?handler=MoveRequirement",
                MoveRequirementForm(moveLaterToken, SpecId, moveLaterRevision, SecondId, "later"));
            Assert.AreEqual(HttpStatusCode.OK, moveLaterResponse.StatusCode);
            CollectionAssert.AreEqual(
                new[] { FirstId, SecondId, ThirdId },
                workspace.CreateSpecEditorInput(SpecId)!.Requirements.Select(requirement => requirement.Id).ToArray());

            var specPath = Path.Combine(repo.Path, createdSpec.Summary.Path);
            var beforeInvalidBatch = await File.ReadAllTextAsync(specPath);
            var invalidToken = await GetRequestVerificationTokenAsync(client, pagePath);
            var invalidRevision = await GetInputValueAsync(client, pagePath, "Edit.SourceRevision");
            using var invalidResponse = await client.PostAsync(
                "/Specs?handler=SaveAllRequirements",
                BatchRequirementsForm(
                    invalidToken,
                    SpecId,
                    invalidRevision,
                    ("0001", "First batch edit", "The batch editor MUST save valid entries."),
                    ("0002", "Invalid second edit", "This clause is invalid."),
                    ("0003", "Third batch edit", "The batch editor MUST save all entries.")));
            var invalidBody = await invalidResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, invalidResponse.StatusCode);
            StringAssert.Contains(invalidBody, "Invalid second edit", StringComparison.Ordinal);
            Assert.AreEqual(beforeInvalidBatch, await File.ReadAllTextAsync(specPath));

            var validToken = await GetRequestVerificationTokenAsync(client, pagePath);
            var validRevision = await GetInputValueAsync(client, pagePath, "Edit.SourceRevision");
            using var validResponse = await client.PostAsync(
                "/Specs?handler=SaveAllRequirements",
                BatchRequirementsForm(
                    validToken,
                    SpecId,
                    validRevision,
                    ("0001", "First batch edit", "The batch editor MUST save the first entry."),
                    ("0002", "Second batch edit", "The batch editor MUST save the second entry."),
                    ("0003", "Third batch edit", "The batch editor MUST save the third entry.")));
            Assert.AreEqual(HttpStatusCode.OK, validResponse.StatusCode);
            CollectionAssert.AreEqual(
                new[] { "First batch edit", "Second batch edit", "Third batch edit" },
                workspace.CreateSpecEditorInput(SpecId)!.Requirements.Select(requirement => requirement.Title).ToArray());

            var staleToken = await GetRequestVerificationTokenAsync(client, pagePath);
            var staleRevision = await GetInputValueAsync(client, pagePath, "Edit.SourceRevision");
            await File.AppendAllTextAsync(specPath, Environment.NewLine);
            var externallyChangedContent = await File.ReadAllTextAsync(specPath);
            using var staleResponse = await client.PostAsync(
                "/Specs?handler=SaveAllRequirements",
                BatchRequirementsForm(
                    staleToken,
                    SpecId,
                    staleRevision,
                    ("0001", "Stale overwrite", "The batch editor MUST reject stale writes."),
                    ("0002", "Second batch edit", "The batch editor MUST preserve external changes."),
                    ("0003", "Third batch edit", "The batch editor MUST remain atomic.")));
            var staleBody = await staleResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, staleResponse.StatusCode);
            StringAssert.Contains(staleBody, "changed on disk", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(externallyChangedContent, await File.ReadAllTextAsync(specPath));
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

    private static async Task<string> GetInputValueAsync(HttpClient client, string path, string inputName)
    {
        using var response = await client.GetAsync(path).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"GET {path}\n{body}");
        var inputMatch = Regex.Match(
            body,
            $"<input[^>]*name=\"{Regex.Escape(inputName)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        Assert.IsTrue(inputMatch.Success, $"Input '{inputName}' was not rendered by GET {path}.");
        var valueMatch = inputValueRegex.Match(inputMatch.Value);
        Assert.IsTrue(valueMatch.Success, $"Input '{inputName}' did not render a value.");
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

    private static FormUrlEncodedContent MoveRequirementForm(
        string token,
        string specId,
        string sourceRevision,
        string requirementReference,
        string direction)
    {
        return Form(
            token,
            ("SelectedReference", specId),
            ("SelectedRequirementReference", requirementReference),
            ("RequirementPage", "1"),
            ("RequirementPageSize", "50"),
            ("Edit.SourceRevision", sourceRevision),
            ("requirementReference", requirementReference),
            ("direction", direction));
    }

    private static FormUrlEncodedContent BatchRequirementsForm(
        string token,
        string specId,
        string sourceRevision,
        params (string IdSuffix, string Title, string Statement)[] requirements)
    {
        var fields = new List<(string Name, string Value)>
        {
            ("SelectedReference", specId),
            ("RequirementPage", "1"),
            ("RequirementPageSize", "50"),
            ("Edit.SourceRevision", sourceRevision)
        };

        for (var index = 0; index < requirements.Length; index++)
        {
            var requirement = requirements[index];
            var prefix = $"Edit.Requirements[{index}]";
            fields.Add(($"{prefix}.IdSuffix", requirement.IdSuffix));
            fields.Add(($"{prefix}.Title", requirement.Title));
            fields.Add(($"{prefix}.Statement", requirement.Statement));
            fields.Add(($"{prefix}.NotesText", string.Empty));
            fields.Add(($"{prefix}.Trace.SatisfiedByText", string.Empty));
            fields.Add(($"{prefix}.Trace.ImplementedByText", string.Empty));
            fields.Add(($"{prefix}.Trace.VerifiedByText", string.Empty));
            fields.Add(($"{prefix}.Trace.DerivedFromText", string.Empty));
            fields.Add(($"{prefix}.Trace.SupersedesText", string.Empty));
            fields.Add(($"{prefix}.Trace.UpstreamRefsText", string.Empty));
            fields.Add(($"{prefix}.Trace.RelatedText", string.Empty));
        }

        return Form(token, fields.ToArray());
    }

    private static Workbench.SpecRequirementEditorInput Requirement(string id, string title, string statement)
    {
        return new Workbench.SpecRequirementEditorInput
        {
            Id = id,
            Title = title,
            Statement = statement
        };
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
