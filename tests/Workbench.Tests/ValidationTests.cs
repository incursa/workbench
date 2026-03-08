using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ValidationTests
{
    [TestMethod]
    public void ValidateRepo_FindsBrokenMarkdownLinks()
    {
        var repoRoot = CreateTempRepo();

        var docPath = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(docPath);
        File.WriteAllText(Path.Combine(docPath, "README.md"), "See [missing](missing.md).");

        var result = ValidationService.ValidateRepo(repoRoot, WorkbenchConfig.Default);
        Assert.IsNotEmpty(result.Errors);
    }

    [TestMethod]
    public void ValidateRepo_FailsWhenDoneItemLivesInActiveDirectory()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0001-test.md"),
            """
            ---
            id: TASK-0001
            type: task
            status: done
            created: 2026-02-19
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Test
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("terminal status 'done' must live under", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_FailsWhenActiveItemLivesInDoneDirectory()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "done", "TASK-0002-test.md"),
            """
            ---
            id: TASK-0002
            type: task
            status: ready
            created: 2026-02-19
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0002 - Test
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("non-terminal status 'ready' must live under", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocReferencingUnknownWorkItem_ReturnsError()
    {
        var repoRoot = CreateValidationRepo();
        var docsDir = Path.Combine(repoRoot, "docs", "10-product");
        Directory.CreateDirectory(docsDir);

        File.WriteAllText(
            Path.Combine(docsDir, "missing-item.md"),
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-9999
              codeRefs: []
            owner: platform
            status: active
            updated: 2026-03-07
            ---

            # Missing item
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("unknown work item 'TASK-9999'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocWithoutBacklinkOnLinkedWorkItem_ReturnsError()
    {
        var repoRoot = CreateValidationRepo();
        var docsDir = Path.Combine(repoRoot, "docs", "10-product");
        Directory.CreateDirectory(docsDir);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0003-test.md"),
            """
            ---
            id: TASK-0003
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0003 - Test
            """);

        File.WriteAllText(
            Path.Combine(docsDir, "orphaned-backlink.md"),
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-0003
              codeRefs: []
            owner: platform
            status: active
            updated: 2026-03-07
            ---

            # Orphaned backlink
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("missing backlink", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_InvalidCodeRefAnchor_ReturnsError()
    {
        var repoRoot = CreateValidationRepo();
        var docsDir = Path.Combine(repoRoot, "docs", "10-product");
        Directory.CreateDirectory(docsDir);
        var sourceDir = Path.Combine(repoRoot, "src", "Workbench.Core");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "ValidationService.cs"), "public static class ValidationService {}");

        File.WriteAllText(
            Path.Combine(docsDir, "invalid-code-ref.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs:
                - src/Workbench.Core/ValidationService.cs#not-a-line-anchor
            owner: platform
            status: active
            updated: 2026-03-07
            ---

            # Invalid code ref
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("invalid anchor", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_RelatedFileWithoutBacklink_ReturnsError()
    {
        var repoRoot = CreateValidationRepo();
        var notesPath = Path.Combine(repoRoot, "docs", "notes.md");
        File.WriteAllText(notesPath, "# Notes without task backlink");

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0004-test.md"),
            """
            ---
            id: TASK-0004
            type: task
            status: draft
            created: 2026-03-07
            related:
              specs: []
              adrs: []
              files:
                - /docs/notes.md
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0004 - Test
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("related.files target missing backlink 'TASK-0004'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_InvalidItemMetadata_ReturnsExpectedErrors()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "bad-name.md"),
            """
            ---
            id: bad-id
            type: incident
            status: someday
            created:
            priority: urgent
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # Bad item
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing required front matter fields", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid id format", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid type 'incident'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid status 'someday'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid priority 'urgent'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("filename does not match ID prefix", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DuplicateIdsAndMissingRelatedSection_ReturnErrors()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0005-first.md"),
            """
            ---
            id: TASK-0005
            type: task
            status: draft
            created: 2026-03-08
            related:
              specs: []
              adrs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0005 - First
            """);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0005-second.md"),
            """
            ---
            id: TASK-0005
            type: task
            status: draft
            created: 2026-03-08
            ---

            # TASK-0005 - Second
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("duplicate ID 'TASK-0005'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing related section", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MarkdownLinkValidation_RespectsIncludeExcludeAndIgnoresExternalLinks()
    {
        var repoRoot = CreateTempRepo();
        var docsRoot = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(Path.Combine(docsRoot, "include"));
        Directory.CreateDirectory(Path.Combine(docsRoot, "skip"));

        File.WriteAllText(Path.Combine(docsRoot, "include", "existing.md"), "# Existing");
        File.WriteAllText(
            Path.Combine(docsRoot, "include", "check.md"),
            """
            [ok](existing.md)
            [anchor](#heading)
            [external](https://example.com)
            [mail](mailto:test@example.com)
            [templated]({{ link }})
            [bad](missing.md)
            """);
        File.WriteAllText(Path.Combine(docsRoot, "skip", "ignored.md"), "[bad](missing.md)");

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(
                LinkInclude: new[] { "docs/include" },
                LinkExclude: new[] { "docs/skip" },
                SkipDocSchema: true));

        Assert.AreEqual(2, result.MarkdownFileCount);
        Assert.AreEqual(1, result.Errors.Count(error => error.Contains("broken local link", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("check.md: broken local link", StringComparison.OrdinalIgnoreCase)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_ConfiguredLinkExclude_SkipsBrokenLinksUnderExcludedPrefix()
    {
        var repoRoot = CreateTempRepo();
        var docsRoot = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(Path.Combine(docsRoot, "included"));
        Directory.CreateDirectory(Path.Combine(docsRoot, "generated"));
        File.WriteAllText(Path.Combine(docsRoot, "included", "bad.md"), "[bad](missing.md)");
        File.WriteAllText(Path.Combine(docsRoot, "generated", "bad.md"), "[bad](missing.md)");

        var config = WorkbenchConfig.Default with
        {
            Validation = new ValidationConfig(new[] { "docs/generated" }, Array.Empty<string>())
        };

        var result = ValidationService.ValidateRepo(
            repoRoot,
            config,
            new ValidationOptions(SkipDocSchema: true));

        Assert.AreEqual(1, result.MarkdownFileCount);
        Assert.AreEqual(1, result.Errors.Count(error => error.Contains("broken local link", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("included", StringComparison.OrdinalIgnoreCase) && error.Contains("bad.md: broken local link", StringComparison.OrdinalIgnoreCase)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MissingDocSchema_IsReported_WhenDocValidationRuns()
    {
        var repoRoot = CreateTempRepo();
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "10-product"));

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "10-product", "sample.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs: []
            owner: platform
            status: active
            updated: 2026-03-08
            ---

            # Sample
            """);

        var result = ValidationService.ValidateRepo(repoRoot, WorkbenchConfig.Default);

        Assert.IsTrue(result.Errors.Any(error => error.Contains("doc schema not found", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_ConfigSchemaErrors_AreSurfaced()
    {
        var repoRoot = CreateValidationRepo();
        Directory.CreateDirectory(Path.Combine(repoRoot, ".workbench"));

        File.WriteAllText(
            Path.Combine(repoRoot, ".workbench", "config.json"),
            """
            {}
            """);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "workbench-config.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": [ "paths" ]
            }
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("config:", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_ItemWithMalformedFrontMatter_ReturnsParseError()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0007-bad.md"),
            """
            ---
            id: TASK-0007
            related:
            	  specs: []
            ---

            # TASK-0007 - Bad
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("Tabs are not supported", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MissingRelatedSubsections_ReturnErrors()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0008-missing-related.md"),
            """
            ---
            id: TASK-0008
            type: task
            status: draft
            created: 2026-03-08
            related:
              specs: []
            ---

            # TASK-0008 - Missing related
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.adrs missing or invalid", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.files missing or invalid", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_InvalidRelatedEntries_ReturnErrors()
    {
        var repoRoot = CreateValidationRepo();

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0009-invalid-related.md"),
            """
            ---
            id: TASK-0009
            type: task
            status: draft
            created: 2026-03-08
            related:
              specs:
                - {}
              adrs:
                - ""
              files:
                - {}
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0009 - Invalid related entries
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.specs entry is invalid", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.adrs entry is invalid", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.files entry is invalid", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_CodeRefValidation_CoversMissingPathAndInvalidLineRanges()
    {
        var repoRoot = CreateValidationRepo();
        var docsDir = Path.Combine(repoRoot, "docs", "10-product");
        Directory.CreateDirectory(docsDir);
        var sourceDir = Path.Combine(repoRoot, "src", "Workbench.Core");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "ValidationService.cs"), "public static class ValidationService {}");

        File.WriteAllText(
            Path.Combine(docsDir, "bad-code-refs.md"),
            """
            ---
            workbench:
              type: doc
              workItems: []
              codeRefs:
                - ""
                - "#L1"
                - src/Workbench.Core/ValidationService.cs#L0
                - src/Workbench.Core/ValidationService.cs#L4-L2
                - src/Workbench.Core/ValidationService.cs#
            owner: platform
            status: active
            updated: 2026-03-08
            ---

            # Bad code refs
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing a path", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid line numbers", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocExclude_SkipsDocValidationButStillChecksMarkdownLinks()
    {
        var repoRoot = CreateValidationRepo();
        var docsRoot = Path.Combine(repoRoot, "docs");
        Directory.CreateDirectory(Path.Combine(docsRoot, "skip"));
        Directory.CreateDirectory(Path.Combine(docsRoot, "check"));

        File.WriteAllText(
            Path.Combine(docsRoot, "skip", "ignored-doc.md"),
            """
            ---
            workbench:
              type: spec
              workItems:
                - TASK-9999
              codeRefs:
                - src/Workbench.Core/Missing.cs#L1
            owner: platform
            status: active
            updated: 2026-03-08
            ---

            [bad](missing.md)
            """);

        File.WriteAllText(Path.Combine(docsRoot, "check", "tracked.md"), "[bad](missing.md)");

        var config = WorkbenchConfig.Default with
        {
            Validation = new ValidationConfig(Array.Empty<string>(), new[] { "docs/skip" })
        };

        var result = ValidationService.ValidateRepo(
            repoRoot,
            config,
            new ValidationOptions(SkipDocSchema: true));

        Assert.AreEqual(2, result.MarkdownFileCount);
        Assert.AreEqual(2, result.Errors.Count(error => error.Contains("broken local link", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsFalse(result.Errors.Any(error => error.Contains("unknown work item 'TASK-9999'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsFalse(result.Errors.Any(error => error.Contains("points to missing file 'src/Workbench.Core/Missing.cs'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_RelatedPathsAndMarkdownLinks_HandleRelativeAndRootRelativeTargets()
    {
        var repoRoot = CreateValidationRepo();
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "10-product"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "40-decisions"));

        File.WriteAllText(Path.Combine(repoRoot, "docs", "10-product", "spec.md"), "# TASK-0006\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "40-decisions", "adr.md"), "# TASK-0006\n");
        File.WriteAllText(Path.Combine(repoRoot, "docs", "linked.md"), "# linked");
        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "README.md"),
            """
            [root](/docs/linked.md)
            [relative](linked.md#section)
            """);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "70-work", "items", "TASK-0006-linked.md"),
            """
            ---
            id: TASK-0006
            type: task
            status: draft
            created: 2026-03-08
            related:
              specs:
                - /docs/10-product/spec.md
              adrs:
                - /docs/40-decisions/adr.md
              files:
                - /docs/10-product/spec.md
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0006 - Linked
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsFalse(result.Errors.Any(error => error.Contains("TASK-0006-linked.md: related.specs missing file", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsFalse(result.Errors.Any(error => error.Contains("TASK-0006-linked.md: related.adrs missing file", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsFalse(result.Errors.Any(error => error.Contains("docs\\README.md: broken local link", StringComparison.OrdinalIgnoreCase) || error.Contains("docs/README.md: broken local link", StringComparison.OrdinalIgnoreCase)), string.Join(Environment.NewLine, result.Errors));
    }

    private static string CreateTempRepo()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return repoRoot;
    }

    private static string CreateValidationRepo()
    {
        var repoRoot = CreateTempRepo();
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "items"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "done"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);

        return repoRoot;
    }
}
