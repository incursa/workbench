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
            specs: new[] { "/docs/10-product/legacy-spec.md" });
        var docPath = repo.WriteDoc(
            "docs/10-product/legacy-spec.md",
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
        StringAssert.Contains(content, "path: /docs/10-product/legacy-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(content, "# Legacy spec", StringComparison.Ordinal);
        StringAssert.Contains(content, "Existing body.", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task SyncLinksAsync_PathHistory_RewritesItemLinksToCurrentDocPathAsync()
    {
        using var repo = new TempDocRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0002-renamed-spec.md",
            "TASK-0002",
            "Renamed spec target",
            specs: new[] { "/docs/10-product/old-spec.md" });
        var docPath = repo.WriteDoc(
            "docs/10-product/new-spec.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0002
              codeRefs: []
              path: /docs/10-product/old-spec.md
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
        StringAssert.Contains(docContent, "path: /docs/10-product/new-spec.md", StringComparison.Ordinal);
        StringAssert.Contains(docContent, "pathHistory:", StringComparison.Ordinal);
        StringAssert.Contains(docContent, "- /docs/10-product/old-spec.md", StringComparison.Ordinal);

        var itemContent = await File.ReadAllTextAsync(itemPath);
        StringAssert.Contains(itemContent, "/docs/10-product/new-spec.md", StringComparison.Ordinal);
        Assert.IsFalse(itemContent.Contains("/docs/10-product/old-spec.md", StringComparison.Ordinal), itemContent);
    }

    [TestMethod]
    public async Task SyncLinksAsync_DryRun_ReportsItemUpdatesWithoutPersistingMovedPathChangesAsync()
    {
        using var repo = new TempDocRepoFixture();
        var itemPath = repo.WriteItem(
            "TASK-0003-dry-run.md",
            "TASK-0003",
            "Dry run target",
            specs: new[] { "/docs/10-product/dry-run-old.md" });
        var docPath = repo.WriteDoc(
            "docs/10-product/dry-run-new.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0003
              codeRefs: []
              path: /docs/10-product/dry-run-old.md
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
        StringAssert.Contains(docContent, "path: /docs/10-product/dry-run-old.md", StringComparison.Ordinal);
        Assert.IsFalse(docContent.Contains("- /docs/10-product/dry-run-old.md", StringComparison.Ordinal), docContent);

        var itemContent = await File.ReadAllTextAsync(itemPath);
        StringAssert.Contains(itemContent, "/docs/10-product/dry-run-old.md", StringComparison.Ordinal);
        Assert.IsFalse(itemContent.Contains("/docs/10-product/dry-run-new.md", StringComparison.Ordinal), itemContent);
    }

    [TestMethod]
    public async Task TryUpdateDocWorkItemLink_AddsMetadataAndAvoidsDuplicateLinksAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "docs/10-product/link-target.md",
            """
            # Link target

            Existing body.
            """);

        var added = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/10-product/link-target.md",
            "TASK-0100",
            add: true,
            apply: true);

        var addedAgain = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/10-product/link-target.md",
            "TASK-0100",
            add: true,
            apply: true);

        Assert.IsTrue(added);
        Assert.IsFalse(addedAgain);

        var content = await File.ReadAllTextAsync(docPath);
        StringAssert.Contains(content, "type: spec", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems:", StringComparison.Ordinal);
        Assert.AreEqual(1, CountOccurrences(content, "- TASK-0100"));
        StringAssert.Contains(content, "path: /docs/10-product/link-target.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task TryUpdateDocWorkItemLink_DryRun_DoesNotMutateDocumentAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "docs/10-product/dry-run-link.md",
            """
            # Dry run link
            """);
        var before = await File.ReadAllTextAsync(docPath);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/10-product/dry-run-link.md",
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
            "docs/10-product/remove-link.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0102
                - TASK-0103
              codeRefs: []
              path: /docs/10-product/remove-link.md
              pathHistory: []
            ---

            # Remove link
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/10-product/remove-link.md",
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
            "docs/10-product/remove-missing.md",
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0104
              codeRefs: []
              path: /docs/10-product/remove-missing.md
              pathHistory: []
            ---

            # Remove missing
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/10-product/remove-missing.md",
            "TASK-9999",
            add: false,
            apply: true);

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public void TryUpdateDocWorkItemLink_WorkItemDocumentPath_ReturnsFalse()
    {
        using var repo = new TempDocRepoFixture();
        repo.WriteDoc(
            "docs/70-work/items/TASK-9999-inline-doc.md",
            """
            # Item doc
            """);

        var updated = DocService.TryUpdateDocWorkItemLink(
            repo.Path,
            WorkbenchConfig.Default,
            "/docs/70-work/items/TASK-9999-inline-doc.md",
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
            specs: new[] { "/docs/10-product/referenced-only.md" });
        var referencedDocPath = repo.WriteDoc(
            "docs/10-product/referenced-only.md",
            """
            # Referenced only
            """);
        var unrelatedDocPath = repo.WriteDoc(
            "docs/10-product/unrelated.md",
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
        StringAssert.Contains(referencedContent, "path: /docs/10-product/referenced-only.md", StringComparison.Ordinal);

        var unrelatedContent = await File.ReadAllTextAsync(unrelatedDocPath);
        Assert.IsFalse(unrelatedContent.Contains("workbench:", StringComparison.Ordinal), unrelatedContent);
    }

    [TestMethod]
    public async Task NormalizeDocs_IncludeAllDocs_AddsWorkbenchMetadataToPlainDocsAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "docs/20-architecture/normalization-target.md",
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
        StringAssert.Contains(content, "type: guide", StringComparison.Ordinal);
        StringAssert.Contains(content, "workItems: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "codeRefs: []", StringComparison.Ordinal);
        StringAssert.Contains(content, "path: /docs/20-architecture/normalization-target.md", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task NormalizeDocs_DryRun_DoesNotPersistMetadataAsync()
    {
        using var repo = new TempDocRepoFixture();
        var docPath = repo.WriteDoc(
            "docs/20-architecture/dry-run-normalize.md",
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
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "docs", "10-product"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "docs", "40-decisions"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "docs", "70-work", "items"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "docs", "70-work", "done"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "docs", "70-work", "templates"));
        }

        public string Path { get; }

        public string WriteDoc(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath) ?? Path);
            File.WriteAllText(fullPath, content.ReplaceLineEndings("\n"));
            return fullPath;
        }

        public string WriteItem(string fileName, string id, string title, IList<string>? specs = null)
        {
            var itemPath = System.IO.Path.Combine(Path, "docs", "70-work", "items", fileName);
            var specLines = specs is { Count: > 0 }
                ? string.Join("\n", specs.Select(link => $"    - {link}"))
                : "    []";

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
                  specs:
                {{specLines}}
                  adrs: []
                  files: []
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
