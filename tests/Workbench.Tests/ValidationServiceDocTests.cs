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

        var docPath = "/overview/sample-spec.md";
        WriteCodeFile(repoRoot, "src/Workbench.Core/SampleCode.cs", "// TASK-0001 backlink\npublic static class SampleCode { }\n");
        WriteWorkItem(repoRoot, "TASK-0001", "task", "draft", specs: new[] { docPath }, files: new[] { "/src/Workbench.Core/SampleCode.cs" });
        WriteDoc(
            repoRoot,
            "overview/sample-spec.md",
            "spec",
            new[] { "TASK-0001" },
            new[] { "src/Workbench.Core/SampleCode.cs#L1" },
            artifactId: "SPEC-TEST-0001");

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_CanonicalSpecificationDocWithRequirementTrace_Passes()
    {
        var repoRoot = CreateTempRepo();
        SeedCanonicalSchemas(repoRoot);

        File.WriteAllText(
            Path.Combine(repoRoot, "specs", "SPEC-CLI-ONBOARDING.md"),
            """
            ---
            artifact_id: SPEC-CLI-ONBOARDING
            artifact_type: specification
            title: Canonical specification
            domain: CLI
            capability: onboarding
            status: draft
            owner: platform
            related_artifacts:
              - ARC-CLI-0001
            ---

            # SPEC-CLI-ONBOARDING - Canonical specification

            ## Purpose

            Canonical spec for validation.

            ## REQ-CLI-0001 Canonical trace labels
            The system MUST preserve canonical trace labels.

            Trace:
            - Satisfied By:
              - ARC-CLI-0001
            - Implemented By:
              - WI-CLI-0001
            - Verified By:
              - VER-CLI-0001
            - Test Refs:
              - tests/Workbench.Tests/ValidationServiceDocTests.cs
            - Code Refs:
              - src/Workbench.Core/ValidationService.cs
            - Related:
              - REQ-CLI-0002

            Notes:
            - Example note.
            """);

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_PolicyDrivenSpecIdMismatch_IsReported()
    {
        var repoRoot = CreateTempRepo();
        SeedValidationSchemas(repoRoot);

        File.WriteAllText(
            Path.Combine(repoRoot, "artifact-id-policy.json"),
            """
            {
              "sequence": { "minimum_digits": 4 },
              "artifact_id_templates": {
                "specification": "SPEC-{domain}{grouping}"
              }
            }
            """);

        WriteDoc(
            repoRoot,
            "specs/SPEC-OPS-0001.md",
            "spec",
            new[] { "TASK-0001" },
            new[] { "src/Workbench.Core/SampleCode.cs#L1" },
            artifactId: "SPEC-OPS-0001");
        WriteCodeFile(repoRoot, "src/Workbench.Core/SampleCode.cs", "// TASK-0001 backlink\npublic static class SampleCode { }\n");
        WriteWorkItem(repoRoot, "TASK-0001", "task", "draft", specs: new[] { "/specs/SPEC-OPS-0001.md" }, files: new[] { "/src/Workbench.Core/SampleCode.cs" });

        var result = ValidationService.ValidateRepo(
            repoRoot,
            WorkbenchConfig.Default,
            new ValidationOptions(SkipDocSchema: true));

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("does not match the configured artifact ID policy", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
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
            "overview/sample-spec.md",
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
            specs: new[] { "/specs/missing-spec.md" },
            adrs: new[] { "/decisions/missing-adr.md" },
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

        var docPath = "/decisions/sample-adr.md";
        WriteWorkItem(repoRoot, "TASK-0003", "task", "draft", adrs: new[] { docPath });
        WriteDoc(repoRoot, "decisions/sample-adr.md", "adr", new[] { "TASK-0003" }, Array.Empty<string>());

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

        var docPath = "/overview/general-doc.md";
        WriteWorkItem(repoRoot, "TASK-0004", "task", "draft", files: new[] { docPath });
        WriteDoc(repoRoot, "overview/general-doc.md", "doc", new[] { "TASK-0004" }, Array.Empty<string>());

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
            "overview/code-refs.md",
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
            Path.Combine(repoRoot, "schemas", "doc.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": [ "owner", "category" ]
            }
            """);

        WriteDoc(repoRoot, "overview/schema-miss.md", "doc", Array.Empty<string>(), Array.Empty<string>());

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
            Path.Combine(repoRoot, "overview", "bad-doc.md"),
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
        Directory.CreateDirectory(Path.Combine(repoRoot, "overview"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "schemas"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "decisions"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "work", "items"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "work", "done"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "src", "Workbench.Core"));
        return repoRoot;
    }

    private static void SeedValidationSchemas(string repoRoot)
    {
        File.WriteAllText(
            Path.Combine(repoRoot, "schemas", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);
    }

    private static void SeedCanonicalSchemas(string repoRoot)
    {
        var sourceRepoRoot = Repository.FindRepoRoot(Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Repo root not found for schema fixture.");

        CopySchemaFile(sourceRepoRoot, repoRoot, "artifact-frontmatter.schema.json");
        CopySchemaFile(sourceRepoRoot, repoRoot, "requirement-clause.schema.json");
        CopySchemaFile(sourceRepoRoot, repoRoot, "requirement-trace-fields.schema.json");
    }

    private static void CopySchemaFile(string sourceRepoRoot, string targetRepoRoot, string schemaName)
    {
        File.Copy(
            Path.Combine(sourceRepoRoot, "schemas", schemaName),
            Path.Combine(targetRepoRoot, "schemas", schemaName));
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
        var path = Path.Combine(repoRoot, "work", "items", $"{id}-sample.md");
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
        IEnumerable<string> codeRefs,
        string? artifactId = null)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var normalizedRelativePath = relativePath.Replace('\\', '/');
        var isCanonicalSpec = string.Equals(docType, "spec", StringComparison.OrdinalIgnoreCase) &&
                               SpecTraceLayout.IsSpecificationRootFile(normalizedRelativePath);
        var resolvedArtifactId = artifactId;
        if (string.Equals(docType, "spec", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(resolvedArtifactId))
        {
            var slug = ArtifactIdPolicy.NormalizeToken(Path.GetFileNameWithoutExtension(relativePath));
            resolvedArtifactId = string.IsNullOrWhiteSpace(slug) ? "SPEC-TEST" : $"SPEC-{slug}";
        }

        if (isCanonicalSpec)
        {
            File.WriteAllText(
                fullPath,
                $$"""
                ---
                artifact_id: {{resolvedArtifactId}}
                artifact_type: specification
                title: Sample specification
                domain: TEST
                capability: sample
                status: draft
                owner: platform
                ---

                # Sample specification

                ## REQ-TEST-0001 Sample requirement
                The system MUST preserve sample behavior.
                """);
            return;
        }

        var artifactLine = string.IsNullOrWhiteSpace(resolvedArtifactId) ? string.Empty : $"artifact_id: {resolvedArtifactId}\n";
        File.WriteAllText(
            fullPath,
            $$"""
            ---
            {{artifactLine}}workbench:
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
