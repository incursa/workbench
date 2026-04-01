using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class WorkbenchWorkspaceManagementTests
{
    [TestMethod]
    public void Workspace_CreateSaveAndDeleteDoc_CoversGenericDocArrayFields()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Workspace doc helper item",
            "planned",
            null,
            null);

        var created = workspace.CreateDoc(new DocEditorInput
        {
            Type = "runbook",
            Title = "Workspace runbook",
            Path = "runbooks/workspace-runbook.md",
            Status = "draft",
            Owner = "platform",
            Body = """
            ## Summary

            Seed body.
            """,
            RelatedArtifacts = "tracking/reference.md",
            WorkItems = workItem.Id,
            CodeRefs = "src/Workbench.Core/DocService.cs#L1-L5"
        });

        var createdPath = Path.Combine(repo.Path, created.Summary.Path);
        Assert.IsTrue(File.Exists(createdPath), createdPath);
        Assert.AreEqual("runbook", created.Summary.Type);

        var editor = workspace.CreateDocEditorInput(created.Summary.Path)!;
        Assert.AreEqual("runbook", editor.Type);
        Assert.AreEqual(created.Summary.Path, editor.Path);
        Assert.IsTrue(editor.RelatedArtifacts.Contains("tracking/reference.md", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(editor.WorkItems.Contains(workItem.Id, StringComparison.OrdinalIgnoreCase));

        editor.Title = "Workspace runbook updated";
        editor.Status = "active";
        editor.Body = """
            ## Summary

            Updated body.
            """;
        editor.RelatedArtifacts = string.Join(Environment.NewLine, "tracking/reference.md", "ARC-WB-0001");
        editor.WorkItems = string.Join(Environment.NewLine, workItem.Id, "WI-WB-0002");
        editor.CodeRefs = "src/Workbench.Core/DocService.cs#L1-10";

        var saved = workspace.SaveDoc(editor);
        var savedDoc = workspace.GetDoc(saved.Path);
        Assert.IsNotNull(savedDoc);
        Assert.AreEqual("Workspace runbook updated", savedDoc!.Summary.Title);
        Assert.AreEqual("active", savedDoc.Summary.Status);

        var savedContent = File.ReadAllText(saved.Path);
        StringAssert.Contains(savedContent, "title: Workspace runbook updated", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "ARC-WB-0001", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "codeRefs:", StringComparison.Ordinal);

        var deleted = workspace.DeleteDoc(saved.Path);
        Assert.AreEqual(
            Path.GetFullPath(saved.Path),
            Path.GetFullPath(Path.Combine(repo.Path, deleted.Doc.Path)));
        Assert.IsFalse(File.Exists(saved.Path));
    }

    [TestMethod]
    public void Workspace_CreateSaveAndDeleteSpec_CoversSchemaMetadataArrays()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Workspace spec item",
            "planned",
            null,
            null);

        var created = workspace.CreateSpec(new SpecEditorInput
        {
            ArtifactId = "SPEC-WB-SCHEMA",
            Domain = "wb",
            Capability = "schema",
            Title = "Workspace spec schema",
            Status = "draft",
            Owner = "platform",
            Purpose = "Describe the schema-driven workflow.",
            Scope = "Scope.",
            Context = "Context.",
            TagsText = """
                alpha
                beta
                """,
            RelatedArtifactsText = string.Join(Environment.NewLine, "ARC-WB-0001", workItem.Id),
            Requirements =
            [
                new SpecRequirementEditorInput
                {
                    Id = "REQ-WB-SCHEMA-0001",
                    Title = "Schema requirement",
                    Statement = "The system MUST persist schema metadata.",
                    NotesText = "One note",
                    Trace = new SpecRequirementTraceEditorInput
                    {
                        ImplementedByText = workItem.Id,
                        RelatedText = "ARC-WB-0001"
                    }
                }
            ]
        });

        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, created.Summary.Path)));
        StringAssert.EndsWith(created.Summary.Path.Replace('\\', '/'), ".json", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(File.ReadAllText(Path.Combine(repo.Path, created.Summary.Path)), "\"tags\"", StringComparison.Ordinal);
        StringAssert.Contains(File.ReadAllText(Path.Combine(repo.Path, created.Summary.Path)), "\"requirements\"", StringComparison.Ordinal);

        var editor = workspace.CreateSpecEditorInput(created.Summary.Path)!;
        Assert.AreEqual("json", editor.SourceFormat);
        Assert.IsTrue(editor.TagsText.Contains("alpha", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(editor.TagsText.Contains("beta", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(editor.RelatedArtifactsText.Contains(workItem.Id, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("REQ-WB-SCHEMA-0001", editor.Requirements.Single().Id);

        editor.TagsText = string.Join(Environment.NewLine, "alpha", "release");
        editor.RelatedArtifactsText = string.Join(Environment.NewLine, "ARC-WB-0001", "VER-WB-0001");
        editor.Status = "approved";
        editor.Requirements[0].Trace.VerifiedByText = "VER-WB-0001";

        var saved = workspace.SaveSpec(editor);
        var savedContent = File.ReadAllText(saved.Path);
        StringAssert.Contains(savedContent, "\"release\"", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "\"related_artifacts\"", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "\"VER-WB-0001\"", StringComparison.Ordinal);

        var reloaded = workspace.CreateSpecEditorInput(saved.Path)!;
        Assert.IsTrue(reloaded.TagsText.Contains("release", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(reloaded.RelatedArtifactsText.Contains("VER-WB-0001", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(reloaded.Requirements[0].Trace.VerifiedByText.Contains("VER-WB-0001", StringComparison.OrdinalIgnoreCase));

        var tagFiltered = workspace.ListDocs("specification", "release");
        Assert.IsTrue(tagFiltered.Any(doc => doc.Path.Equals(reloaded.Path, StringComparison.OrdinalIgnoreCase)));

        var deleted = workspace.DeleteDoc(saved.Path);
        Assert.AreEqual(
            Path.GetFullPath(saved.Path),
            Path.GetFullPath(Path.Combine(repo.Path, deleted.Doc.Path)));
        Assert.IsFalse(File.Exists(saved.Path));
    }

    [TestMethod]
    public void Workspace_CreateSaveAndDeleteArchitectureDoc_CoversSchemaRequiredArrays()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);

        var created = workspace.CreateDoc(new DocEditorInput
        {
            Type = "architecture",
            ArtifactId = "ARC-WB-SCHEMA-0001",
            Title = "Workspace architecture",
            Domain = "WB",
            Capability = "schema",
            Status = "draft",
            Owner = "platform",
            Body = """
                ## Summary

                Seed architecture body.
                """,
            Satisfies = """
                REQ-WB-SCHEMA-0001
                REQ-WB-SCHEMA-0002
                """,
            RelatedArtifacts = """
                SPEC-WB-SCHEMA-0001
                tracking/reference.md
                """
        });

        var createdPath = Path.Combine(repo.Path, created.Summary.Path);
        Assert.IsTrue(File.Exists(createdPath), createdPath);
        StringAssert.Contains(created.Summary.Path.Replace('\\', '/'), "specs/architecture/", StringComparison.OrdinalIgnoreCase);

        var editor = workspace.CreateDocEditorInput(created.Summary.Path)!;
        Assert.AreEqual("architecture", editor.Type);
        Assert.IsTrue(editor.Satisfies.Contains("REQ-WB-SCHEMA-0001", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(editor.RelatedArtifacts.Contains("tracking/reference.md", StringComparison.OrdinalIgnoreCase));

        editor.ArtifactId = "ARC-WB-SCHEMA-0002";
        editor.Title = "Workspace architecture updated";
        editor.Status = "approved";
        editor.Owner = "release";
        editor.Body = """
            ## Summary

            Updated architecture body.
            """;
        editor.Satisfies = string.Join(Environment.NewLine, "REQ-WB-SCHEMA-0001", "REQ-WB-SCHEMA-0003");
        editor.RelatedArtifacts = string.Join(Environment.NewLine, "SPEC-WB-SCHEMA-0001", "ARC-WB-SCHEMA-0009");

        var saved = workspace.SaveDoc(editor);
        var savedDoc = workspace.GetDoc(saved.Path);
        Assert.IsNotNull(savedDoc);
        Assert.AreEqual("Workspace architecture updated", savedDoc!.Summary.Title);
        Assert.AreEqual("approved", savedDoc.Summary.Status);
        Assert.AreEqual("ARC-WB-SCHEMA-0002", savedDoc.Summary.ArtifactId);

        var savedContent = File.ReadAllText(saved.Path);
        StringAssert.Contains(savedContent, "artifact_id: ARC-WB-SCHEMA-0002", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "satisfies:", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "REQ-WB-SCHEMA-0003", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "ARC-WB-SCHEMA-0009", StringComparison.Ordinal);

        var filtered = workspace.ListDocs("architecture", "REQ-WB-SCHEMA-0003");
        Assert.IsTrue(
            filtered.Any(doc => NormalizeDocPath(repo.Path, doc.Path).Equals(NormalizeDocPath(repo.Path, saved.Path), StringComparison.OrdinalIgnoreCase)));

        var deleted = workspace.DeleteDoc(saved.Path);
        Assert.AreEqual(
            Path.GetFullPath(saved.Path),
            Path.GetFullPath(Path.Combine(repo.Path, deleted.Doc.Path)));
        Assert.IsFalse(File.Exists(saved.Path));
    }

    [TestMethod]
    public void Workspace_CreateSaveAndDeleteVerificationDoc_CoversSchemaRequiredArrays()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);

        var created = workspace.CreateDoc(new DocEditorInput
        {
            Type = "verification",
            ArtifactId = "VER-WB-SCHEMA-0001",
            Title = "Workspace verification",
            Domain = "WB",
            Capability = "schema",
            Status = "planned",
            Owner = "platform",
            Body = """
                ## Summary

                Seed verification body.
                """,
            Verifies = """
                REQ-WB-SCHEMA-0101
                REQ-WB-SCHEMA-0102
                """,
            RelatedArtifacts = """
                ARC-WB-SCHEMA-0001
                tracking/reference.md
                """
        });

        var createdPath = Path.Combine(repo.Path, created.Summary.Path);
        Assert.IsTrue(File.Exists(createdPath), createdPath);
        StringAssert.Contains(created.Summary.Path.Replace('\\', '/'), "specs/verification/", StringComparison.OrdinalIgnoreCase);

        var editor = workspace.CreateDocEditorInput(created.Summary.Path)!;
        Assert.AreEqual("verification", editor.Type);
        Assert.IsTrue(editor.Verifies.Contains("REQ-WB-SCHEMA-0101", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(editor.RelatedArtifacts.Contains("tracking/reference.md", StringComparison.OrdinalIgnoreCase));

        editor.ArtifactId = "VER-WB-SCHEMA-0002";
        editor.Title = "Workspace verification updated";
        editor.Status = "passed";
        editor.Owner = "release";
        editor.Body = """
            ## Summary

            Updated verification body.
            """;
        editor.Verifies = string.Join(Environment.NewLine, "REQ-WB-SCHEMA-0101", "REQ-WB-SCHEMA-0103");
        editor.RelatedArtifacts = string.Join(Environment.NewLine, "ARC-WB-SCHEMA-0001", "VER-WB-SCHEMA-0009");

        var saved = workspace.SaveDoc(editor);
        var savedDoc = workspace.GetDoc(saved.Path);
        Assert.IsNotNull(savedDoc);
        Assert.AreEqual("Workspace verification updated", savedDoc!.Summary.Title);
        Assert.AreEqual("passed", savedDoc.Summary.Status);
        Assert.AreEqual("VER-WB-SCHEMA-0002", savedDoc.Summary.ArtifactId);

        var savedContent = File.ReadAllText(saved.Path);
        StringAssert.Contains(savedContent, "artifact_id: VER-WB-SCHEMA-0002", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "verifies:", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "REQ-WB-SCHEMA-0103", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(savedContent, "VER-WB-SCHEMA-0009", StringComparison.Ordinal);

        var filtered = workspace.ListDocs("verification", "REQ-WB-SCHEMA-0103");
        Assert.IsTrue(
            filtered.Any(doc => NormalizeDocPath(repo.Path, doc.Path).Equals(NormalizeDocPath(repo.Path, saved.Path), StringComparison.OrdinalIgnoreCase)));

        var deleted = workspace.DeleteDoc(saved.Path);
        Assert.AreEqual(
            Path.GetFullPath(saved.Path),
            Path.GetFullPath(Path.Combine(repo.Path, deleted.Doc.Path)));
        Assert.IsFalse(File.Exists(saved.Path));
    }

    [TestMethod]
    public void Workspace_DeleteItem_RemovesBacklinksFromSpecsAndGenericDocs()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Workspace delete item",
            "planned",
            null,
            null);

        var spec = workspace.CreateDoc(new DocEditorInput
        {
            Type = "spec",
            Title = "Workspace delete spec",
            ArtifactId = "SPEC-WB-DELETE-0001",
            Domain = "WB",
            Capability = "delete",
            RelatedArtifacts = workItem.Id
        });

        var runbook = workspace.CreateDoc(new DocEditorInput
        {
            Type = "runbook",
            Title = "Workspace delete runbook",
            Path = "runbooks/workspace-delete-runbook.md",
            RelatedArtifacts = "tracking/reference.md",
            WorkItems = workItem.Id
        });

        var deleted = workspace.DeleteItem(workItem.Id);
        Assert.AreEqual(workItem.Id, deleted.Item.Id);
        Assert.AreEqual(2, deleted.DocsUpdated);
        Assert.IsFalse(File.Exists(workItem.Path));

        var specContent = File.ReadAllText(Path.Combine(repo.Path, spec.Summary.Path));
        var runbookContent = File.ReadAllText(Path.Combine(repo.Path, runbook.Summary.Path));
        Assert.IsFalse(specContent.Contains(workItem.Id, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(runbookContent.Contains(workItem.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private static string NormalizeDocPath(string repoRoot, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path));
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
