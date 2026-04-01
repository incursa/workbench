using System.Reflection;
using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class DocCoverageTests
{
    [TestMethod]
    public void DocPromptTemplates_BuildTemplate_CoversExpectedTypes_AndFallback()
    {
        var templateType = typeof(DocService).Assembly.GetType("Workbench.Core.DocPromptTemplates");
        Assert.IsNotNull(templateType);

        var buildTemplate = templateType!.GetMethod("BuildTemplate", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(buildTemplate);

        static string InvokeBuildTemplate(MethodInfo method, string docType)
        {
            var value = method.Invoke(null, new object[] { docType }) as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Template for '{docType}' was empty.");
            return value!;
        }

        var specTemplate = InvokeBuildTemplate(buildTemplate!, "spec");
        var specificationTemplate = InvokeBuildTemplate(buildTemplate, "specification");
        var architectureTemplate = InvokeBuildTemplate(buildTemplate, "architecture");
        var verificationTemplate = InvokeBuildTemplate(buildTemplate, "verification");
        var workItemTemplate = InvokeBuildTemplate(buildTemplate, "work_item");
        var workItemDashTemplate = InvokeBuildTemplate(buildTemplate, "work-item");
        var docTemplate = InvokeBuildTemplate(buildTemplate, "doc");
        var runbookTemplate = InvokeBuildTemplate(buildTemplate, "runbook");
        var fallbackTemplate = InvokeBuildTemplate(buildTemplate, "unsupported-type");

        StringAssert.Contains(specTemplate, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(specificationTemplate, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(architectureTemplate, "artifact_type: architecture", StringComparison.Ordinal);
        StringAssert.Contains(verificationTemplate, "artifact_type: verification", StringComparison.Ordinal);
        StringAssert.Contains(workItemTemplate, "artifact_type: work_item", StringComparison.Ordinal);
        StringAssert.Contains(workItemDashTemplate, "artifact_type: work_item", StringComparison.Ordinal);
        StringAssert.Contains(docTemplate, "## Summary", StringComparison.Ordinal);
        StringAssert.Contains(docTemplate, "## Scope", StringComparison.Ordinal);
        StringAssert.Contains(docTemplate, "## Notes", StringComparison.Ordinal);
        StringAssert.Contains(runbookTemplate, "## Notes", StringComparison.Ordinal);
        StringAssert.Contains(fallbackTemplate, "## Notes", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocTitleHelper_FromTranscript_HandlesNormalAndEmptyInput()
    {
        var title = DocTitleHelper.FromTranscript("sync navigation links for canonical docs");
        Assert.AreEqual("sync navigation links for canonical docs", title);

        Assert.AreEqual("Voice note", DocTitleHelper.FromTranscript("   "));
    }

    [TestMethod]
    public void DocService_CreateDoc_CanonicalSpec_AssignsArtifactIdAndWritesCanonicalFrontMatter()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var createdItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Doc coverage canonical item",
            "planned",
            null,
            null);

        var result = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "spec",
            "Doc coverage specification",
            path: null,
            workItems: new List<string> { createdItem.Id },
            codeRefs: new List<string>(),
            force: false);

        Assert.IsTrue(File.Exists(result.Path), result.Path);
        Assert.IsTrue(
            result.Path.Replace('\\', '/').Contains("/specs/", StringComparison.Ordinal),
            result.Path);
        Assert.IsTrue(result.ArtifactId!.StartsWith("SPEC-", StringComparison.Ordinal), result.ArtifactId);

        var content = File.ReadAllText(result.Path);
        StringAssert.Contains(content, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(content, $"artifact_id: {result.ArtifactId}", StringComparison.Ordinal);
        StringAssert.Contains(content, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(content, $"- {createdItem.Id}", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocService_CreateDoc_Runbook_AndShowDataCoverSuccessAndMissingPath()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var createdItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Doc coverage runbook item",
            "planned",
            null,
            null);

        var runbook = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "runbook",
            "Doc coverage runbook",
            path: "runbooks/doc-coverage-runbook.md",
            workItems: new List<string> { createdItem.Id },
            codeRefs: new List<string> { "src/Workbench.Core/DocService.cs#L1-L5" },
            force: false);

        Assert.IsTrue(File.Exists(runbook.Path), runbook.Path);
        var runbookContent = File.ReadAllText(runbook.Path);
        StringAssert.Contains(runbookContent, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(runbookContent, "workItems:", StringComparison.Ordinal);
        StringAssert.Contains(runbookContent, createdItem.Id, StringComparison.Ordinal);

        var show = DocService.GetDocShowData(repo.Path, WorkbenchConfig.Default, runbook.Path);
        Assert.AreEqual("runbook", show.Type);
        Assert.AreEqual("Doc coverage runbook", show.Title);
        Assert.HasCount(1, show.WorkItems);
        Assert.AreEqual(createdItem.Id, show.WorkItems[0]);

        var missing = Assert.Throws<InvalidOperationException>(
            () => DocService.GetDocShowData(repo.Path, WorkbenchConfig.Default, "runbooks/missing.md"));
        StringAssert.Contains(missing.Message, "Doc not found", StringComparison.Ordinal);
    }

    [TestMethod]
    public void DocService_PathResolutionAndArtifactLookup_CoversArtifactAndFallbackStem()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var spec = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "spec",
            "Lookup specification",
            path: null,
            workItems: new List<string>(),
            codeRefs: new List<string>(),
            force: false);

        var resolvedByArtifact = DocService.TryResolveDocPathByArtifactId(
            repo.Path,
            WorkbenchConfig.Default,
            spec.ArtifactId!,
            out var resolvedDocPath);
        Assert.IsTrue(resolvedByArtifact);
        Assert.AreEqual(Path.GetFullPath(spec.Path), Path.GetFullPath(resolvedDocPath));

        var plainDocPath = Path.Combine(repo.Path, "tracking", "plain-note.md");
        Directory.CreateDirectory(Path.GetDirectoryName(plainDocPath)!);
        File.WriteAllText(plainDocPath, "# Plain note");

        var hasArtifact = DocService.TryGetDocumentArtifactId(plainDocPath, out var artifactId);
        Assert.IsTrue(hasArtifact);
        Assert.AreEqual("plain-note", artifactId);
    }

    [TestMethod]
    public void DocService_CreateGeneratedDoc_EditDoc_AndTryUpdateLink_CoverCanonicalAndLegacyBranches()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Doc coverage item",
            "planned",
            null,
            null);

        var canonical = DocService.CreateGeneratedDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "spec",
            "Generated spec",
            """
            ## Purpose

            Original body.
            """,
            path: null,
            workItems: new List<string> { workItem.Id },
            codeRefs: new List<string>(),
            tags: new List<string> { "quality", "docs" },
            related: new List<string> { "VER-WB-0001" },
            status: "draft",
            source: null,
            force: false,
            artifactId: "SPEC-WB-0009",
            domain: "WB",
            capability: "coverage",
            owner: "platform");

        Assert.IsTrue(File.Exists(canonical.Path), canonical.Path);
        var canonicalContent = File.ReadAllText(canonical.Path);
        StringAssert.Contains(canonicalContent, "artifact_type: specification", StringComparison.Ordinal);
        StringAssert.Contains(canonicalContent, "artifact_id: SPEC-WB-0009", StringComparison.Ordinal);
        StringAssert.Contains(canonicalContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(canonicalContent, workItem.Id, StringComparison.Ordinal);
        StringAssert.Contains(canonicalContent, "VER-WB-0001", StringComparison.Ordinal);

        var canonicalEdit = DocService.EditDoc(
            repo.Path,
            WorkbenchConfig.Default,
            canonical.Path,
            "SPEC-WB-0010",
            "Edited spec",
            "approved",
            "release",
            "WB-OPS",
            "coverage-plus",
            """
            ## Edited spec

            Updated body.
            """,
            new List<string> { workItem.Id, "WI-WB-0002" },
            null,
            null,
            null);

        Assert.IsTrue(canonicalEdit.ArtifactIdUpdated);
        Assert.IsTrue(canonicalEdit.TitleUpdated);
        Assert.IsTrue(canonicalEdit.StatusUpdated);
        Assert.IsTrue(canonicalEdit.OwnerUpdated);
        Assert.IsTrue(canonicalEdit.DomainUpdated);
        Assert.IsTrue(canonicalEdit.CapabilityUpdated);
        Assert.IsTrue(canonicalEdit.BodyUpdated);
        Assert.IsTrue(canonicalEdit.RelatedArtifactsUpdated);
        Assert.IsFalse(canonicalEdit.WorkItemsUpdated);
        Assert.IsFalse(canonicalEdit.CodeRefsUpdated);

        var editedCanonical = File.ReadAllText(canonical.Path);
        StringAssert.Contains(editedCanonical, "artifact_id: SPEC-WB-0010", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "title: Edited spec", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "status: approved", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "owner: release", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "domain: WB-OPS", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "capability: coverage-plus", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "Updated body.", StringComparison.Ordinal);
        StringAssert.Contains(editedCanonical, "WI-WB-0002", StringComparison.Ordinal);

        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, canonical.Path, workItem.Id, add: false));
        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, canonical.Path, workItem.Id, add: true));

        var legacy = DocService.CreateGeneratedDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "runbook",
            "Generated runbook",
            """
            ## Steps

            Do the thing.
            """,
            path: "runbooks/generated-runbook.md",
            workItems: new List<string> { workItem.Id },
            codeRefs: new List<string> { "src/Workbench.Core/DocService.cs#L1-L5" },
            tags: new List<string> { "ops" },
            related: new List<string> { "tracking/reference.md" },
            status: "active",
            source: null,
            force: false,
            artifactId: "RUNBOOK-0001",
            owner: "platform");

        Assert.IsTrue(File.Exists(legacy.Path), legacy.Path);
        var legacyContent = File.ReadAllText(legacy.Path);
        StringAssert.Contains(legacyContent, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(legacyContent, "workItems:", StringComparison.Ordinal);
        StringAssert.Contains(legacyContent, "codeRefs:", StringComparison.Ordinal);
        StringAssert.Contains(legacyContent, "artifact_id: RUNBOOK-0001", StringComparison.Ordinal);

        var legacyEdit = DocService.EditDoc(
            repo.Path,
            WorkbenchConfig.Default,
            legacy.Path,
            " ",
            "Edited runbook",
            "active",
            "platform",
            null,
            null,
            """
            ## Edited runbook

            Updated body.
            """,
            new List<string> { "tracking/reference.md", "VER-WB-0002" },
            null,
            new List<string> { workItem.Id, "WI-WB-0002" },
            new List<string> { "src/Workbench.Core/DocService.cs#L1-L10" });

        Assert.IsTrue(legacyEdit.ArtifactIdUpdated);
        Assert.IsTrue(legacyEdit.TitleUpdated);
        Assert.IsTrue(legacyEdit.BodyUpdated);
        Assert.IsTrue(legacyEdit.RelatedArtifactsUpdated);
        Assert.IsTrue(legacyEdit.WorkItemsUpdated);
        Assert.IsTrue(legacyEdit.CodeRefsUpdated);

        var editedLegacy = File.ReadAllText(legacy.Path);
        Assert.IsFalse(editedLegacy.Contains("artifact_id:", StringComparison.Ordinal));
        StringAssert.Contains(editedLegacy, "title: Edited runbook", StringComparison.Ordinal);
        StringAssert.Contains(editedLegacy, "Updated body.", StringComparison.Ordinal);
        StringAssert.Contains(editedLegacy, "WI-WB-0002", StringComparison.Ordinal);

        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, legacy.Path, workItem.Id, add: false));
        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, legacy.Path, workItem.Id, add: true));
    }

    [TestMethod]
    public void DocService_CreateGeneratedDoc_EditDoc_CoversArchitectureAndVerificationBranches()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var architecture = DocService.CreateGeneratedDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "architecture",
            "Generated architecture",
            """
            ## Purpose

            Original architecture body.
            """,
            path: null,
            workItems: new List<string>(),
            codeRefs: new List<string>(),
            tags: new List<string>(),
            related: new List<string> { "SPEC-WB-ARCH-0001" },
            status: "draft",
            source: null,
            force: false,
            artifactId: "ARC-WB-ARCH-0001",
            domain: "WB",
            capability: "architecture",
            owner: "platform",
            satisfies: new List<string> { "REQ-WB-ARCH-0001", "REQ-WB-ARCH-0002" });

        Assert.IsTrue(File.Exists(architecture.Path), architecture.Path);
        var architectureContent = File.ReadAllText(architecture.Path);
        StringAssert.Contains(architectureContent, "artifact_type: architecture", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "artifact_id: ARC-WB-ARCH-0001", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "satisfies:", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "REQ-WB-ARCH-0002", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(architectureContent, "SPEC-WB-ARCH-0001", StringComparison.Ordinal);

        var architectureEdit = DocService.EditDoc(
            repo.Path,
            WorkbenchConfig.Default,
            architecture.Path,
            "ARC-WB-ARCH-0002",
            "Edited architecture",
            "approved",
            "release",
            "WB-OPS",
            "architecture-plus",
            """
            ## Purpose

            Updated architecture body.
            """,
            new List<string> { "REQ-WB-ARCH-0001", "REQ-WB-ARCH-0003" },
            null,
            new List<string> { "SPEC-WB-ARCH-0001", "ARC-WB-ARCH-0009" },
            null,
            null,
            null);

        Assert.IsTrue(architectureEdit.ArtifactIdUpdated);
        Assert.IsTrue(architectureEdit.TitleUpdated);
        Assert.IsTrue(architectureEdit.StatusUpdated);
        Assert.IsTrue(architectureEdit.OwnerUpdated);
        Assert.IsTrue(architectureEdit.DomainUpdated);
        Assert.IsTrue(architectureEdit.CapabilityUpdated);
        Assert.IsTrue(architectureEdit.BodyUpdated);
        Assert.IsTrue(architectureEdit.RelatedArtifactsUpdated);
        Assert.IsFalse(architectureEdit.WorkItemsUpdated);
        Assert.IsFalse(architectureEdit.CodeRefsUpdated);

        var editedArchitecture = File.ReadAllText(architecture.Path);
        StringAssert.Contains(editedArchitecture, "artifact_id: ARC-WB-ARCH-0002", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "title: Edited architecture", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "status: approved", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "owner: release", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "domain: WB-OPS", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "capability: architecture-plus", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "REQ-WB-ARCH-0003", StringComparison.Ordinal);
        StringAssert.Contains(editedArchitecture, "ARC-WB-ARCH-0009", StringComparison.Ordinal);

        var verification = DocService.CreateGeneratedDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "verification",
            "Generated verification",
            """
            ## Purpose

            Original verification body.
            """,
            path: null,
            workItems: new List<string>(),
            codeRefs: new List<string>(),
            tags: new List<string>(),
            related: new List<string> { "ARC-WB-VER-0001" },
            status: "planned",
            source: null,
            force: false,
            artifactId: "VER-WB-VER-0001",
            domain: "WB",
            capability: "verification",
            owner: "platform",
            verifies: new List<string> { "REQ-WB-VER-0001", "REQ-WB-VER-0002" });

        Assert.IsTrue(File.Exists(verification.Path), verification.Path);
        var verificationContent = File.ReadAllText(verification.Path);
        StringAssert.Contains(verificationContent, "artifact_type: verification", StringComparison.Ordinal);
        StringAssert.Contains(verificationContent, "artifact_id: VER-WB-VER-0001", StringComparison.Ordinal);
        StringAssert.Contains(verificationContent, "verifies:", StringComparison.Ordinal);
        StringAssert.Contains(verificationContent, "REQ-WB-VER-0002", StringComparison.Ordinal);
        StringAssert.Contains(verificationContent, "related_artifacts:", StringComparison.Ordinal);
        StringAssert.Contains(verificationContent, "ARC-WB-VER-0001", StringComparison.Ordinal);

        var verificationEdit = DocService.EditDoc(
            repo.Path,
            WorkbenchConfig.Default,
            verification.Path,
            "VER-WB-VER-0002",
            "Edited verification",
            "passed",
            "release",
            "WB-OPS",
            "verification-plus",
            """
            ## Purpose

            Updated verification body.
            """,
            null,
            new List<string> { "REQ-WB-VER-0001", "REQ-WB-VER-0003" },
            new List<string> { "ARC-WB-VER-0001", "VER-WB-VER-0009" },
            null,
            null,
            null);

        Assert.IsTrue(verificationEdit.ArtifactIdUpdated);
        Assert.IsTrue(verificationEdit.TitleUpdated);
        Assert.IsTrue(verificationEdit.StatusUpdated);
        Assert.IsTrue(verificationEdit.OwnerUpdated);
        Assert.IsTrue(verificationEdit.DomainUpdated);
        Assert.IsTrue(verificationEdit.CapabilityUpdated);
        Assert.IsTrue(verificationEdit.BodyUpdated);
        Assert.IsTrue(verificationEdit.RelatedArtifactsUpdated);
        Assert.IsFalse(verificationEdit.WorkItemsUpdated);
        Assert.IsFalse(verificationEdit.CodeRefsUpdated);

        var editedVerification = File.ReadAllText(verification.Path);
        StringAssert.Contains(editedVerification, "artifact_id: VER-WB-VER-0002", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "title: Edited verification", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "status: passed", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "owner: release", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "domain: WB-OPS", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "capability: verification-plus", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "REQ-WB-VER-0003", StringComparison.Ordinal);
        StringAssert.Contains(editedVerification, "VER-WB-VER-0009", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task DocService_NormalizeDocs_AndSyncLinks_CoverBackfillAndPathHistoryAsync()
    {
        using var repo = CreateRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: true);

        var legacyItemPath = Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-0001-legacy.md");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyItemPath)!);
        await File.WriteAllTextAsync(
            legacyItemPath,
            new FrontMatter(
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "TASK-0001",
                    ["type"] = "task",
                    ["status"] = "planned",
                    ["title"] = "Legacy doc sync item",
                    ["related"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["specs"] = new List<object?>(),
                        ["files"] = new List<object?> { "/tracking/referenced.md" },
                        ["prs"] = new List<object?>(),
                        ["issues"] = new List<object?>(),
                        ["branches"] = new List<object?>()
                    }
                },
                """
                # TASK-0001 - Legacy doc sync item

                ## Summary
                Legacy item for doc sync coverage.
                """).Serialize()).ConfigureAwait(false);

        var workItem = WorkItemService.LoadItem(legacyItemPath) ?? throw new InvalidOperationException("Failed to load legacy work item.");
        Assert.IsTrue(
            WorkItemService.ListItems(repo.Path, WorkbenchConfig.Default, includeDone: true).Items.Any(item => item.Id.Equals(workItem.Id, StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(workItem.Related.Files.Contains("/tracking/referenced.md", StringComparer.OrdinalIgnoreCase));

        var unreferencedPath = Path.Combine(repo.Path, "tracking", "unreferenced.md");
        Directory.CreateDirectory(Path.GetDirectoryName(unreferencedPath)!);
        await File.WriteAllTextAsync(
            unreferencedPath,
            """
            # Unreferenced note

            Body only.
            """).ConfigureAwait(false);

        var referencedPath = Path.Combine(repo.Path, "tracking", "referenced.md");
        await File.WriteAllTextAsync(
            referencedPath,
            """
            # Referenced note

            Body only.
            """).ConfigureAwait(false);

        var firstNormalize = DocService.NormalizeDocs(repo.Path, WorkbenchConfig.Default, includeAllDocs: false, dryRun: false);
        Assert.AreEqual(1, firstNormalize);

        var referencedContent = await File.ReadAllTextAsync(referencedPath).ConfigureAwait(false);
        StringAssert.Contains(referencedContent, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(referencedContent, "path: /tracking/referenced.md", StringComparison.Ordinal);

        var secondNormalize = DocService.NormalizeDocs(repo.Path, WorkbenchConfig.Default, includeAllDocs: true, dryRun: false);
        Assert.IsGreaterThan(0, secondNormalize);

        var unreferencedContent = await File.ReadAllTextAsync(unreferencedPath).ConfigureAwait(false);
        StringAssert.Contains(unreferencedContent, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(unreferencedContent, "path: /tracking/unreferenced.md", StringComparison.Ordinal);
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort test cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
