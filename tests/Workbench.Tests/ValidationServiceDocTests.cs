using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ValidationServiceDocTests
{
    [TestMethod]
    public void ValidateRepo_DocBacklinksAndCodeRefs_PassForCoherentRepo()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        var docPath = "/docs/10-product/sample-spec.md";
        WriteCodeFile(repoRoot, "src/Workbench.Core/SampleCode.cs", "// TASK-0001 backlink\npublic static class SampleCode { }\n");
        WriteWorkItem(repoRoot, "TASK-0001", "task", "draft", specs: new[] { docPath }, files: new[] { "/src/Workbench.Core/SampleCode.cs" });
        WriteDoc(repoRoot, "docs/10-product/sample-spec.md", "spec", new[] { "TASK-0001" }, new[] { "src/Workbench.Core/SampleCode.cs#L1" });

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocValidationReportsUnknownWorkItemsMissingBacklinksAndBadCodeRefs()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        WriteCodeFile(repoRoot, "src/Workbench.Core/SampleCode.cs", "public static class SampleCode { }\n");
        WriteWorkItem(repoRoot, "TASK-0001", "task", "draft");
        WriteDoc(
            repoRoot,
            "docs/10-product/sample-spec.md",
            "spec",
            new[] { "TASK-0001", "TASK-9999" },
            new[]
            {
                "src/Workbench.Core/SampleCode.cs#bad-anchor",
                "src/Workbench.Core/MissingCode.cs#L1",
            });

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("unknown work item 'TASK-9999'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing backlink", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("invalid anchor", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("points to missing file", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_RelatedPathsAndFilesRequireExistingTargetsAndBacklinks()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        WriteCodeFile(repoRoot, "src/Workbench.Core/NoBacklink.cs", "public static class NoBacklink { }\n");
        WriteWorkItem(
            repoRoot,
            "TASK-0002",
            "task",
            "draft",
            specs: new[] { "/docs/10-product/missing-spec.md" },
            adrs: new[] { "/docs/40-decisions/missing-adr.md" },
            files: new[] { "/src/Workbench.Core/NoBacklink.cs" });

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.specs missing file", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.adrs missing file", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("related.files target missing backlink 'TASK-0002'", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_AdrDocType_UsesAdrBacklink()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        var docPath = "/docs/40-decisions/sample-adr.md";
        WriteWorkItem(repoRoot, "TASK-0003", "task", "draft", adrs: new[] { docPath });
        WriteDoc(repoRoot, "docs/40-decisions/sample-adr.md", "adr", new[] { "TASK-0003" }, Array.Empty<string>());

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_GenericDocType_UsesFileBacklink()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        var docPath = "/docs/10-product/general-doc.md";
        WriteWorkItem(repoRoot, "TASK-0004", "task", "draft", files: new[] { docPath });
        WriteDoc(repoRoot, "docs/10-product/general-doc.md", "doc", new[] { "TASK-0004" }, Array.Empty<string>());

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_ValidCodeRefs_WithPlainAndAnchoredPaths_Pass()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        WriteCodeFile(repoRoot, "src/Workbench.Core/SampleCode.cs", "public static class SampleCode { }\n");
        WriteDoc(
            repoRoot,
            "docs/10-product/code-refs.md",
            "doc",
            Array.Empty<string>(),
            new[]
            {
                "src/Workbench.Core/SampleCode.cs",
                "src/Workbench.Core/SampleCode.cs#",
                "src/Workbench.Core/SampleCode.cs#L1",
                "src/Workbench.Core/SampleCode.cs#L1-L1",
            });

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocSchemaErrors_AreReported_WhenSchemaExists()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "doc.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": [ "owner", "category" ]
            }
            """);

        WriteDoc(repoRoot, "docs/10-product/schema-miss.md", "doc", Array.Empty<string>(), Array.Empty<string>());

        var result = ValidationService.ValidateRepo(repoRoot, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("schema-miss.md", StringComparison.OrdinalIgnoreCase) && error.Contains("category", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MalformedDocFrontMatter_ReturnsParseError()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "10-product", "bad-doc.md"),
            """
            ---
            workbench:
            	  type: spec
            ---

            # Bad doc
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(result.Errors.Any(error => error.Contains("Tabs are not supported", StringComparison.Ordinal)), string.Join(Environment.NewLine, result.Errors));
    }

    private static string CreateTempRepo()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "10-product"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "40-decisions"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "items"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "70-work", "done"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "src", "Workbench.Core"));
        return repoRoot;
    }

    private static void SeedValidationSchemas(string repoRoot)
    {
        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);
    }

    private static void WriteWorkItem(
        string repoRoot,
        string id,
        string type,
        string status,
        IEnumerable<string>? specs = null,
        IEnumerable<string>? adrs = null,
        IEnumerable<string>? files = null)
    {
        var path = Path.Combine(repoRoot, "docs", "70-work", "items", $"{id}-sample.md");
        File.WriteAllText(
            path,
            $$"""
            ---
            id: {{id}}
            type: {{type}}
            status: {{status}}
            created: 2026-03-08
            related:
              specs:{{FormatListSection(specs)}}
              adrs:{{FormatListSection(adrs)}}
              files:{{FormatListSection(files)}}
              prs: []
              issues: []
              branches: []
            ---

            # {{id}} - Sample

            ## Summary
            Validation sample
            """);
    }

    private static void WriteDoc(
        string repoRoot,
        string relativePath,
        string docType,
        IEnumerable<string> workItems,
        IEnumerable<string> codeRefs)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            $$"""
            ---
            workbench:
              type: {{docType}}
              workItems:{{FormatListSection(workItems)}}
              codeRefs:{{FormatListSection(codeRefs)}}
            owner: platform
            status: active
            updated: 2026-03-08
            ---

            # Sample doc
            """);
    }

    private static void WriteCodeFile(string repoRoot, string relativePath, string content)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static string FormatListSection(IEnumerable<string>? values)
    {
        var items = values?.ToList() ?? new List<string>();
        if (items.Count == 0)
        {
            return " []";
        }

        return Environment.NewLine + string.Join(Environment.NewLine, items.Select(item => $"    - {item}"));
    }
}
