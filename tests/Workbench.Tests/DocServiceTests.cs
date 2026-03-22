using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class DocServiceTests
{
    [TestMethod]
    public async Task SyncLinksAsync_ReferencedDocWithoutFrontMatter_GainsMetadataAndBacklinkAsync()
    {
        using var repo = new TempDocRepoFixture();
        repo.WriteItem(
            "TASK-0001-spec-link.md",
            "TASK-0001",
            "Spec link target",
            specs: new[] { "/specs/legacy-spec.md" });
        var docPath = repo.WriteDoc(
            "specs/legacy-spec.md",
            """
            # Legacy spec

            Existing body.
            """);

        var result = await DocService.SyncLinksAsync(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: false,
            syncIssues: false,
            includeDone: false,
            dryRun: false);

        Assert.AreEqual(2, result.DocsUpdated);
        Assert.AreEqual(0, result.ItemsUpdated);
        Assert.IsEmpty(result.MissingItems);

        var content = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(content, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(content, "type: spec", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems:", StringComparison.Ordinal);
        StringAssert.Contains(content, "- TASK-0001", StringComparison.Ordinal);
        StringAssert.Contains(content, "path: /specs/legacy-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(content, "# Legacy spec", StringComparison.Ordinal);
        StringAssert.Contains(content, "Existing body.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateDoc_PolicyDrivenSpecId_UsesDomainAndCapabilityMetadata()
    {
        using var repo = new TempDocRepoFixture();
        File.WriteAllText(
            Path.Combine(repo.Path, "artifact-id-policy.json"),
            """
            {
              "sequence": { "minimum_digits": 4 },
              "artifact_id_templates": {
                "specification": "SPEC-{domain}{grouping}"
              }
            }
            """);

        var result = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "spec",
            "ACH duplicate batch handling",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            artifactId: null,
            domain: "PAY",
            capability: "ACH");

        Assert.AreEqual("SPEC-PAY-ACH", result.ArtifactId);

        var content = File.ReadAllText(result.Path);
        StringAssert.Contains(content, "artifact_id: SPEC-PAY-ACH", StringComparison.Ordinal);
        StringAssert.Contains(content, "domain: PAY", StringComparison.Ordinal);
        StringAssert.Contains(content, "capability: ACH", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CreateDoc_DefaultPolicy_UsesCanonicalPathsInBareRepo()
    {
        var repoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);

        try
        {
            var result = DocService.CreateDoc(
                repoRoot,
                WorkbenchConfig.Default,
                "spec",
                "CLI empty-directory smoke test",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                artifactId: null,
                domain: "CLI",
                capability: "SMOKE");

            Assert.AreEqual("SPEC-CLI-SMOKE", result.ArtifactId);
            Assert.AreEqual(
                "specs/SPEC-CLI-SMOKE.md",
                System.IO.Path.GetRelativePath(repoRoot, result.Path).Replace('\\', '/'));

            var content = File.ReadAllText(result.Path);
            StringAssert.Contains(content, "artifact_id: SPEC-CLI-SMOKE", StringComparison.Ordinal);
            StringAssert.Contains(content, "artifact_type: specification", StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CreateDoc_SpecificationStarterBody_IsParseable()
    {
        using var repo = new TempDocRepoFixture();

        var result = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "spec",
            "Smoke spec",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            artifactId: null,
            domain: "CLI",
            capability: "SMOKE");

        var content = File.ReadAllText(result.Path);
        Assert.IsTrue(FrontMatter.TryParse(content, out var frontMatter, out var error), error);
        Assert.IsNotNull(frontMatter);

        var requirementClauses = SpecTraceMarkdown.ParseRequirementClauses(frontMatter.Body, out var parseErrors);
        Assert.IsEmpty(parseErrors, string.Join(Environment.NewLine, parseErrors));
        Assert.HasCount(1, requirementClauses);
        Assert.AreEqual("REQ-EXAMPLE-0001", requirementClauses[0].RequirementId);
    }

    [TestMethod]
    public async Task SyncLinksAsync_PathHistory_RewritesItemLinksToCurrentDocPathAsync()
    {
        using var repo = new TempDocRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0002-renamed-spec.md",
            "TASK-0002",
            "Renamed spec target",
            specs: new[] { "/specs/old-spec.md" });
        var docPath = repo.WriteDoc(
            "specs/new-spec.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0002
              codeRefs: []
              path: /specs/old-spec.md
              pathHistory: []
            ---

            # Renamed spec
            """);

        var result = await DocService.SyncLinksAsync(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: false,
            syncIssues: false,
            includeDone: false,
            dryRun: false);

        Assert.AreEqual(1, result.DocsUpdated);
        Assert.AreEqual(2, result.ItemsUpdated);
        Assert.IsEmpty(result.MissingItems);

        var docContent = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(docContent, "path: /specs/new-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(docContent, "pathHistory:", StringComparison.Ordinal);
        StringAssert.Contains(docContent, "- /specs/old-spec.md", StringComparison.Ordinal);

        var itemContent = await File.ReadAllTextAsync(itemPath);
        StringAssert.Contains(itemContent, "/specs/new-spec.md", StringComparison.Ordinal);
        Assert.IsFalse(itemContent.Contains("/specs/old-spec.md", StringComparison.Ordinal), itemContent);
    }

    [TestMethod]
    public async Task SyncLinksAsync_DryRun_ReportsItemUpdatesWithoutPersistingMovedPathChangesAsync()
    {
        using var repo = new TempDocRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0003-dry-run.md",
            "TASK-0003",
            "Dry run target",
            specs: new[] { "/specs/dry-run-old.md" });
        var docPath = repo.WriteDoc(
            "specs/dry-run-new.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0003
              codeRefs: []
              path: /specs/dry-run-old.md
              pathHistory: []
            ---

            # Dry run spec
            """);

        var result = await DocService.SyncLinksAsync(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: false,
            syncIssues: false,
            includeDone: false,
            dryRun: true);

        Assert.AreEqual(0, result.DocsUpdated);
        Assert.AreEqual(2, result.ItemsUpdated);
        Assert.HasCount(1, result.MissingDocs);
        Assert.IsEmpty(result.MissingItems);

        var docContent = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(docContent, "path: /specs/dry-run-old.md", StringComparison.Ordinal);
        Assert.IsFalse(docContent.Contains("- /specs/dry-run-old.md", StringComparison.Ordinal), docContent);

        var itemContent = await File.ReadAllTextAsync(itemPath);
        StringAssert.Contains(itemContent, "/specs/dry-run-old.md", StringComparison.Ordinal);
        Assert.IsFalse(itemContent.Contains("/specs/dry-run-new.md", StringComparison.Ordinal), itemContent);
    }

    [TestMethod]
    public async Task TryUpdateDocWorkItemLink_AddsMetadataAndAvoidsDuplicateLinksAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "specs/link-target.md",
            """
            # Link target

            Existing body.
            """);

        var added = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/specs/link-target.md",
            "TASK-0100",
            add: true,
            apply: true);

        var addedAgain = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/specs/link-target.md",
            "TASK-0100",
            add: true,
            apply: true);

        Assert.IsTrue(added);
        Assert.IsFalse(addedAgain);

        var content = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(content, "type: spec", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems:", StringComparison.Ordinal);
        Assert.AreEqual(1, CountOccurrences(content, "- TASK-0100"));
        StringAssert.Contains(content, "path: /specs/link-target.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task TryUpdateDocWorkItemLink_DryRun_DoesNotMutateDocumentAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "specs/dry-run-link.md",
            """
            # Dry run link
            """);
        var before = await File.ReadAllTextAsync(docPath);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/specs/dry-run-link.md",
            "TASK-0101",
            add: true,
            apply: false);

        Assert.IsTrue(updated);
        var after = await File.ReadAllTextAsync(docPath);
        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public async Task TryUpdateDocWorkItemLink_RemoveExistingLink_UpdatesDocumentAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "specs/remove-link.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0102
                - TASK-0103
              codeRefs: []
              path: /specs/remove-link.md
              pathHistory: []
            ---

            # Remove link
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/specs/remove-link.md",
            "TASK-0102",
            add: false,
            apply: true);

        Assert.IsTrue(updated);

        var content = await File.ReadAllTextAsync(docPath);
        Assert.IsFalse(content.Contains("- TASK-0102", StringComparison.Ordinal), content);
        StringAssert.Contains(content, "- TASK-0103", StringComparison.Ordinal);
    }

    [TestMethod]
    public void TryUpdateDocWorkItemLink_RemoveMissingLink_WithNormalizedMetadata_ReturnsFalse()
    {
        using var repo = new TempDocRepoFixture();
        repo.WriteDoc(
            "specs/remove-missing.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0104
              codeRefs: []
              path: /specs/remove-missing.md
              pathHistory: []
            ---

            # Remove missing
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/specs/remove-missing.md",
            "TASK-9999",
            add: false,
            apply: true);

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public void CreateDoc_ContractType_UsesContractsFolderAndLinksWorkItem()
    {
        using var repo = new TempDocRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0105-contract-link.md",
            "TASK-0105",
            "Contract link target",
            files: new[] { "/contracts/api-contract.md" });

        var result = DocService.CreateDoc(
            repo.Path,
            WorkbenchConfig.Default,
            "contract",
            "API Contract",
            null,
            new List<string> { "TASK-0105" },
            new List<string>(),
            force: false);

        Assert.AreEqual("contract", result.Type);
        StringAssert.Contains(result.Path, $"{Path.DirectorySeparatorChar}contracts{Path.DirectorySeparatorChar}api-contract.md", StringComparison.OrdinalIgnoreCase);

        var content = File.ReadAllText(result.Path);
        StringAssert.Contains(content, "type: contract", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems:", StringComparison.Ordinal);
        StringAssert.Contains(content, "- TASK-0105", StringComparison.Ordinal);
        StringAssert.Contains(content, "# API Contract", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Overview", StringComparison.Ordinal);
        StringAssert.Contains(content, "## Related specs", StringComparison.Ordinal);

        var updatedItem = WorkItemService.LoadItem(itemPath) ?? throw new InvalidOperationException("Failed to reload work item.");
        CollectionAssert.Contains(updatedItem.Related.Files.ToArray(), "/contracts/api-contract.md");
    }

    [TestMethod]
    public void TryUpdateDocWorkItemLink_WorkItemDocumentPath_ReturnsFalse()
    {
        using var repo = new TempDocRepoFixture();
        repo.WriteDoc(
            "work/items/TASK-9999-inline-doc.md",
            """
            # Item doc
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/work/items/TASK-9999-inline-doc.md",
            "TASK-9999",
            add: true,
            apply: true);

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public async Task NormalizeDocs_ReferencedPlainDoc_WhenIncludeAllDocsFalse_OnlyNormalizesReferencedDocAsync()
    {
        using var repo = new TempDocRepoFixture();
        repo.WriteItem(
            "TASK-0105-referenced-doc.md",
            "TASK-0105",
            "Referenced doc target",
            specs: new[] { "/specs/referenced-only.md" });
        var referencedDocPath = repo.WriteDoc(
            "specs/referenced-only.md",
            """
            # Referenced only
            """);
        var unrelatedDocPath = repo.WriteDoc(
            "specs/unrelated.md",
            """
            # Unrelated
            """);

        var updated = DocService.NormalizeDocs(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: false,
            dryRun: false);

        Assert.AreEqual(1, updated);

        var referencedContent = await File.ReadAllTextAsync(referencedDocPath);
        StringAssert.Contains(referencedContent, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(referencedContent, "path: /specs/referenced-only.md", StringComparison.Ordinal);

        var unrelatedContent = await File.ReadAllTextAsync(unrelatedDocPath);
        Assert.IsFalse(unrelatedContent.Contains("workbench:", StringComparison.Ordinal), unrelatedContent);
    }

    [TestMethod]
    public async Task NormalizeDocs_IncludeAllDocs_AddsWorkbenchMetadataToPlainDocsAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "architecture/normalization-target.md",
            """
            # Normalization target

            Some architecture notes.
            """);

        var updated = DocService.NormalizeDocs(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: true,
            dryRun: false);

        Assert.AreEqual(1, updated);

        var content = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(content, "workbench:", StringComparison.Ordinal);
        StringAssert.Contains(content, "type: architecture", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "codeRefs: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "path: /architecture/normalization-target.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task NormalizeDocs_DryRun_DoesNotPersistMetadataAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "architecture/dry-run-normalize.md",
            """
            # Dry run normalization
            """);
        var before = await File.ReadAllTextAsync(docPath);

        var updated = DocService.NormalizeDocs(
            repo.Path,
            WorkbenchConfig.Default,
            includeAllDocs: true,
            dryRun: true);

        Assert.AreEqual(0, updated);

        var after = await File.ReadAllTextAsync(docPath);
        Assert.AreEqual(before, after);
    }

    private sealed class TempDocRepoFixture : IDisposable
    {
        public TempDocRepoFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "overview"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "requirements"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "contracts"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "schemas"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "decisions"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "runbooks"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "tracking"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "work", "items"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "work", "done"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "work", "templates"));
        }

        public string Path { get; }

        public string WriteDoc(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath) ?? Path);
            File.WriteAllText(fullPath, content.ReplaceLineEndings("\n"));
            return fullPath;
        }

        public string WriteItem(string fileName, string id, string title, IList<string>? specs = null, IList<string>? files = null)
        {
            var itemPath = System.IO.Path.Combine(Path, "work", "items", fileName);
            var specLines = specs is { Count: > 0 }
                ? "\n" + string.Join("\n", specs.Select(link => $"    - {link}"))
                : " []";
            var fileLines = files is { Count: > 0 }
                ? "\n" + string.Join("\n", files.Select(link => $"    - {link}"))
                : " []";

            File.WriteAllText(
                itemPath,
                $$"""
                ---
                id: {{id}}
                type: task
                status: ready
                title: {{title}}
                priority: medium
                owner: null
                created: 2026-03-07
                updated: null
                githubSynced: null
                tags: []
                related:
                  specs:{{specLines}}
                  adrs: []
                  files:{{fileLines}}
                  prs: []
                  issues: []
                  branches: []
                ---

                # {{id}} - {{title}}

                ## Summary

                Example summary.

                ## Acceptance criteria

                - Example criterion
                """.ReplaceLineEndings("\n"));

            return itemPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static int CountOccurrences(string text, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }
}
