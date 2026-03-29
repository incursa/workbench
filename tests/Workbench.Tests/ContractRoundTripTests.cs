using System.Text.Json;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ContractRoundTripTests
{
    [TestMethod]
    public void RoundTrip_CoreContractRecords_PreserveExpectedFields()
    {
        var configSources = new ConfigSources(true, ".workbench/config.json");
        var configData = new ConfigData(WorkbenchConfig.Default, configSources);
        var configOutput = new ConfigOutput(true, configData);
        var codexDoctor = new CodexDoctorOutput(
            true,
            new CodexDoctorData(true, "1.2.3", null));
        var codexRun = new CodexRunOutput(
            true,
            new CodexRunData(true, false, 0, "ok", string.Empty));
        var credentialUpdate = new CredentialUpdateData(
            ".workbench/credentials.env",
            "GITHUB_TOKEN",
            true,
            false,
            false);
        var docDelete = new DocDeleteOutput(
            true,
            new DocDeleteData("specs/requirements/WB/SPEC-WB-0001.md", 2));
        var docEdit = new DocEditData(
            "specs/requirements/WB/SPEC-WB-0001.md",
            "SPEC-WB-0001",
            false,
            true,
            true,
            false,
            false,
            true,
            true,
            true,
            true);
        var docShow = new DocShowData(
            "specs/requirements/WB/SPEC-WB-0001.md",
            "SPEC-WB-0001",
            "WB",
            "docs",
            "specification",
            "Spec",
            "draft",
            "platform",
            new List<string> { "WI-WB-0001" },
            new List<string> { "src/Workbench.Core/ValidationService.cs#L1-L5" },
            "## REQ-WB-0001 Example\nThe tool MUST stay deterministic.");
        var githubIssuePayload = new GithubIssuePayload(
            "incursa/workbench",
            42,
            "https://github.com/incursa/workbench/issues/42",
            "Issue title",
            "open",
            new List<string> { "bug", "priority-high" },
            new List<string> { "https://github.com/incursa/workbench/pull/77" });
        var normalizeData = new NormalizeData(
            3,
            4,
            false,
            true,
            true);
        var validateData = new ValidateData(
            new List<string> { "e1" },
            new List<string> { "w1" },
            new ValidateCounts(1, 1, 2, 9),
            ValidationProfiles.Traceable,
            new List<string> { "specs/requirements/WB" },
            new List<ValidationFinding>
            {
                new(
                    ValidationProfiles.Traceable,
                    "unresolved-reference",
                    "error",
                    "Missing ARC-WB-0001",
                    "specs/requirements/WB/SPEC-WB-0001.md",
                    "REQ-WB-0001",
                    "Satisfied By",
                    "ARC-WB-0001",
                    "architecture",
                    "specs/architecture/WB/ARC-WB-0001.md")
            });
        var itemSummary = new ItemSummary(
            "WI-WB-0001",
            "work_item",
            "planned",
            "Track release readiness",
            "specs/work-items/WB/WI-WB-0001-track-release-readiness.md");
        var itemSyncConflict = new ItemSyncConflictEntry(
            "WI-WB-0009",
            "https://github.com/incursa/workbench/issues/90",
            "Title drift");
        var draft = new WorkItemDraft(
            "Improve validation",
            "Cover critical branches.",
            new List<string> { "Add tests", "Preserve behavior" },
            "work_item",
            new List<string> { "quality", "tests" });
        var navResult = new NavigationService.NavigationSyncResult(
            DocsUpdated: 2,
            ItemsUpdated: 5,
            IndexFilesUpdated: 1,
            MissingDocs: new List<string> { "specs/missing.md" },
            MissingItems: new List<string> { "WI-WB-0099" },
            Warnings: new List<string> { "warn" });
        var workItemResult = new WorkItemService.WorkItemResult(
            Id: "WI-WB-0005",
            Slug: "improve-validation",
            Path: "specs/work-items/WB/WI-WB-0005-improve-validation.md");
        var docCreateResult = new DocService.DocCreateResult(
            Path: "specs/requirements/WB/SPEC-WB-0005.md",
            ArtifactId: "SPEC-WB-0005",
            Type: "spec",
            WorkItems: new List<string> { "WI-WB-0005" });
        var docSyncResult = new DocService.DocSyncResult(
            DocsUpdated: 3,
            ItemsUpdated: 7,
            MissingDocs: new List<string> { "specs/ghost.md" },
            MissingItems: new List<string> { "WI-WB-9999" });

        var roundTrippedConfig = RoundTrip(configOutput);
        var roundTrippedDoctor = RoundTrip(codexDoctor);
        var roundTrippedRun = RoundTrip(codexRun);
        var roundTrippedCredential = RoundTrip(credentialUpdate);
        var roundTrippedDocDelete = RoundTrip(docDelete);
        var roundTrippedDocEdit = RoundTrip(docEdit);
        var roundTrippedDocShow = RoundTrip(docShow);
        var roundTrippedIssue = RoundTrip(githubIssuePayload);
        var roundTrippedNormalize = RoundTrip(normalizeData);
        var roundTrippedValidate = RoundTrip(validateData);
        var roundTrippedItemSummary = RoundTrip(itemSummary);
        var roundTrippedConflict = RoundTrip(itemSyncConflict);
        var roundTrippedDraft = RoundTrip(draft);
        var roundTrippedNav = RoundTrip(navResult);
        var roundTrippedWorkItemResult = RoundTrip(workItemResult);
        var roundTrippedDocCreate = RoundTrip(docCreateResult);
        var roundTrippedDocSync = RoundTrip(docSyncResult);

        Assert.IsTrue(roundTrippedConfig.Ok);
        Assert.IsTrue(roundTrippedConfig.Data.Sources.Defaults);
        Assert.AreEqual(".workbench/config.json", roundTrippedConfig.Data.Sources.RepoConfig);

        Assert.IsTrue(roundTrippedDoctor.Data.Available);
        Assert.AreEqual("1.2.3", roundTrippedDoctor.Data.Version);
        Assert.IsTrue(roundTrippedRun.Data.Started);
        Assert.AreEqual(0, roundTrippedRun.Data.ExitCode);
        Assert.AreEqual("ok", roundTrippedRun.Data.StdOut);

        Assert.AreEqual("GITHUB_TOKEN", roundTrippedCredential.Key);
        Assert.IsTrue(roundTrippedDocDelete.Ok);
        Assert.AreEqual(2, roundTrippedDocDelete.Data.ItemsUpdated);
        Assert.AreEqual("SPEC-WB-0001", roundTrippedDocEdit.ArtifactId);
        Assert.IsTrue(roundTrippedDocEdit.CodeRefsUpdated);
        Assert.AreEqual("specification", roundTrippedDocShow.Type);
        Assert.AreEqual("WI-WB-0001", roundTrippedDocShow.WorkItems[0]);

        Assert.AreEqual(42, roundTrippedIssue.Number);
        Assert.AreEqual("priority-high", roundTrippedIssue.Labels[1]);
        Assert.IsTrue(roundTrippedNormalize.ItemsNormalized);
        Assert.AreEqual(1, roundTrippedValidate.Counts.Errors);
        Assert.AreEqual(ValidationProfiles.Traceable, roundTrippedValidate.Profile);
        Assert.IsNotNull(roundTrippedValidate.Scope);
        Assert.IsNotNull(roundTrippedValidate.Findings);
        Assert.AreEqual("specs/requirements/WB", roundTrippedValidate.Scope[0]);
        Assert.AreEqual(ValidationProfiles.Traceable, roundTrippedValidate.Findings[0].Profile);
        Assert.AreEqual("WI-WB-0001", roundTrippedItemSummary.Id);
        Assert.AreEqual("Title drift", roundTrippedConflict.Reason);
        Assert.AreEqual("Improve validation", roundTrippedDraft.Title);
        Assert.HasCount(2, roundTrippedDraft.AcceptanceCriteria!);

        Assert.AreEqual(2, roundTrippedNav.DocsUpdated);
        Assert.AreEqual("WI-WB-0005", roundTrippedWorkItemResult.Id);
        Assert.AreEqual("SPEC-WB-0005", roundTrippedDocCreate.ArtifactId);
        Assert.AreEqual(3, roundTrippedDocSync.DocsUpdated);
        Assert.AreEqual("WI-WB-9999", roundTrippedDocSync.MissingItems[0]);
    }

    [TestMethod]
    public void ValidationProfileFixture_ListsCoreTraceableAndAuditableCases()
    {
        var fixturePath = Path.Combine(FindRepoRoot(), "testdata", "contracts", "validate-profile-envelopes.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));

        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToList();
        Assert.HasCount(3, cases);

        var profiles = cases
            .Select(entry => entry.GetProperty("data").GetProperty("profile").GetString())
            .ToArray();

        CollectionAssert.AreEquivalent(new[] { "core", "traceable", "auditable" }, profiles);
    }

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var result = JsonSerializer.Deserialize<T>(json);
        Assert.IsNotNull(result, $"Round-trip failed for type {typeof(T).Name}.");
        return result!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Workbench.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Workbench.slnx.");
    }
}
