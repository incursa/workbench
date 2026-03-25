using System.Text.Json;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ValidationCoverageTests
{
    [TestMethod]
    public void ValidateRepo_ValidCanonicalRepo_ReturnsNoErrors()
    {
        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        File.WriteAllText(
            Path.Combine(repo.Path, ".workbench", "config.json"),
            JsonSerializer.Serialize(WorkbenchConfig.Default, WorkbenchJsonContext.Default.WorkbenchConfig));

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WB", "WI-WB-0001-valid.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0001", "Valid item", "planned"));

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "requirements", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "README.md"),
            """
            Just a helper note.
            """);
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "IGNORED.md"),
            """
            ---
            title: Ignored doc
            domain: WB
            status: draft
            owner: platform
            ---

            # Ignored doc
            """);
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-VALIDATION.md"),
            """
            ---
            artifact_id: SPEC-WB-VALIDATION
            artifact_type: specification
            title: Valid specification
            domain: WB
            capability: validation
            status: draft
            owner: platform
            related_artifacts:
              - WI-WB-0001
            ---

            # SPEC-WB-VALIDATION - Valid specification

            ## Purpose

            Exercise canonical validation coverage.

            ## REQ-WB-VALIDATION-0001 Keep the repo valid
            The system MUST remain valid.

            Trace:
            - Implemented By:
              - WI-WB-0001
            - Verified By:
              - VER-WB-9000
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "architecture", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "architecture", "WB", "ARC-WB-9000.md"),
            """
            ---
            artifact_id: ARC-WB-9000
            artifact_type: architecture
            title: Valid architecture
            domain: WB
            status: approved
            owner: platform
            satisfies:
              - REQ-WB-VALIDATION-0001
            related_artifacts:
              - SPEC-WB-VALIDATION
              - WI-WB-0001
              - VER-WB-9000
            ---

            # ARC-WB-9000 - Valid architecture

            ## Purpose

            Exercise canonical architecture validation coverage.
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "verification", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "verification", "WB", "VER-WB-9000.md"),
            """
            ---
            artifact_id: VER-WB-9000
            artifact_type: verification
            title: Valid verification
            domain: WB
            status: passed
            owner: platform
            verifies:
              - REQ-WB-VALIDATION-0001
            related_artifacts:
              - SPEC-WB-VALIDATION
              - ARC-WB-9000
            ---

            # VER-WB-9000 - Valid verification

            ## Scope

            Exercise canonical verification validation coverage.
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MissingConfigSchema_ReportsConfigSchemaError()
    {
        using var repo = new TempRepoRoot();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        File.WriteAllText(Path.Combine(repo.Path, ".workbench", "config.json"), "{}");

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("config: workbench config schema not found", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_SpecWithoutRequirementClauses_ReportsClauseError()
    {
        using var repo = CreateRepoWithSchemas();
        WriteConfig(repo.Path);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WB", "WI-WB-0001-valid.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0001", "Valid item", "planned"));

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "requirements", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-NO-CLAUSES.md"),
            """
            ---
            artifact_id: SPEC-WB-NO-CLAUSES
            artifact_type: specification
            title: Missing clauses
            domain: WB
            capability: validation
            status: draft
            owner: platform
            related_artifacts:
              - WI-WB-0001
            ---

            # SPEC-WB-NO-CLAUSES - Missing clauses

            ## Purpose

            Exercise the empty requirement-clause branch.
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("no requirement clauses found in specification body.", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DuplicateWorkItemArtifactIds_ReportsDuplicateId()
    {
        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WI-WB-0001-alpha.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0001", "Alpha", "planned"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WI-WB-0001-beta.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0001", "Beta", "in_progress"));

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.AreEqual(2, result.WorkItemCount);
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("duplicate artifact_id 'WI-WB-0001'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_InvalidWorkItemStatus_ReportsCanonicalStatusError()
    {
        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WI-WB-0002-bad-status.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0002", "Bad status", "unknown_state"));

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("invalid canonical status 'unknown_state'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_MalformedWorkItemFrontMatter_ReportsParseError()
    {
        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WI-WB-0003-malformed.md"),
            """
            ---
             title:
              child: value
            ---
            # Bad
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("Invalid indentation", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_SpecDocOutsideRequirementsRoot_ReportsLocationError()
    {
        using var repo = CreateRepoWithSchemas();
        var docPath = Path.Combine(repo.Path, "specs", "architecture", "WB", "SPEC-WB-9999-misplaced.md");
        Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
        File.WriteAllText(
            docPath,
            """
            ---
            artifact_id: SPEC-WB-9999
            artifact_type: specification
            title: Misplaced specification
            domain: WB
            status: draft
            owner: platform
            related_artifacts: []
            ---

            ## REQ-WB-9999 Example requirement
            The system MUST stay deterministic.
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("specifications must live under 'specs/requirements/<domain>/'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_BrokenLocalMarkdownLink_ReportsBrokenLink()
    {
        using var repo = CreateRepoWithSchemas();
        WriteConfig(repo.Path);
        Directory.CreateDirectory(Path.Combine(repo.Path, "docs"));
        File.WriteAllText(
            Path.Combine(repo.Path, "docs", "shared.md"),
            """
            # Shared

            ## section

            Shared content.
            """);
        File.WriteAllText(
            Path.Combine(repo.Path, "README.md"),
            """
            # Temp repo

            See [missing note](docs/missing-note.md), [absolute](/docs/shared.md?foo=1#section), [external](https://example.com), [mail](mailto:test@example.com), [anchor](#section), and [template](docs/{{token}}.md).
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("broken local link", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_LegacyWorkItemAndMarkdownLinkFilters_ReportExpectedErrors()
    {
        using var repo = CreateRepoWithSchemas();
        WriteConfig(repo.Path);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WB", "TASK-0001-legacy.md"),
            """
            ---
            id: TASK-0001
            type: task
            status: planned
            title: Legacy item
            related:
              specs: []
              files: []
              prs: []
              issues: []
              branches: []
            ---

            # TASK-0001 - Legacy item
            """);

        File.WriteAllText(
            Path.Combine(repo.Path, "README.md"),
            """
            # Repo

            See [missing](docs/missing.md), [external](https://example.com), [mail](mailto:test@example.com), [anchor](#section), and [template](docs/{{token}}.md).
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("missing required canonical work item fields.", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
        Assert.IsGreaterThan(
            0,
            result.Errors.Count(error => error.Contains("broken local link", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_ArtifactIdPolicyMismatch_ReportsCanonicalPolicyErrors()
    {
        using var repo = CreateRepoWithSchemas();
        WriteConfig(repo.Path);
        File.WriteAllText(
            Path.Combine(repo.Path, "artifact-id-policy.json"),
            """
            {
              "sequence": {
                "minimum_digits": 4
              },
              "artifact_id_templates": {
                "specification": "SPEX-{domain}-{sequence}",
                "architecture": "ARCX-{domain}-{sequence}",
                "work_item": "WIX-{domain}-{sequence}",
                "verification": "VERX-{domain}-{sequence}"
              }
            }
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "requirements", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-VALIDATION.md"),
            """
            ---
            artifact_id: SPEC-WB-VALIDATION
            artifact_type: specification
            title: Policy mismatch spec
            domain: WB
            capability: validation
            status: draft
            owner: platform
            related_artifacts:
              - WI-WB-0001
            ---

            # SPEC-WB-VALIDATION - Policy mismatch spec

            ## REQ-WB-VALIDATION-0001 Keep the repo valid
            The system MUST remain valid.
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "work-items", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "work-items", "WB", "WI-WB-0001-policy-mismatch.md"),
            CreateCanonicalWorkItemFrontMatter("WI-WB-0001", "Policy mismatch item", "planned"));

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("artifact_id 'SPEC-WB-VALIDATION' does not match the configured artifact ID policy.", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("artifact_id 'WI-WB-0001' does not match the configured artifact ID policy.", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocValidation_ReportsArchitectureAndVerificationRootErrors()
    {
        using var repo = CreateRepoWithSchemas();
        WriteConfig(repo.Path);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "requirements", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "ARC-WB-0001-wrong-root.md"),
            """
            ---
            artifact_id: ARC-WB-0001
            artifact_type: architecture
            title: Misplaced architecture
            domain: WB
            status: draft
            owner: platform
            satisfies:
              - REQ-WB-0001
            ---

            # ARC-WB-0001 - Misplaced architecture
            """);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "architecture", "WB"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "architecture", "WB", "VER-WB-0001-wrong-root.md"),
            """
            ---
            artifact_id: VER-WB-0001
            artifact_type: verification
            title: Misplaced verification
            domain: WB
            status: draft
            owner: platform
            verifies:
              - REQ-WB-0001
            ---

            # VER-WB-0001 - Misplaced verification
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);

        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("architecture artifacts must live under 'specs/architecture/<domain>/'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("verification artifacts must live under 'specs/verification/<domain>/'", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateRepo_DocExclude_SkipsExcludedInvalidDocs()
    {
        using var repo = CreateRepoWithSchemas();
        var validation = new ValidationConfig(
            new List<string>(),
            new List<string> { "specs/requirements/WB/excluded" });
        WriteConfig(repo.Path, validation);

        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "requirements", "WB", "excluded"));
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "excluded", "SPEC-WB-SKIP.md"),
            """
            ---
            artifact_id: SPEC-WB-SKIP
            artifact_type: specification
            title: Excluded invalid spec
            domain: WB
            capability: validation
            status: draft
            owner: platform
            related_artifacts: []
            ---

            # SPEC-WB-SKIP - Excluded invalid spec
            """);

        var result = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default with { Validation = validation });

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateMarkdownLinks_IncludeAndExcludeFilters_AreApplied()
    {
        using var repo = CreateRepoWithSchemas();
        var validation = new ValidationConfig(
            new List<string> { "docs/skip" },
            new List<string>());
        WriteConfig(repo.Path, validation);

        Directory.CreateDirectory(Path.Combine(repo.Path, "docs"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "docs", "skip"));
        File.WriteAllText(
            Path.Combine(repo.Path, "docs", "keep.md"),
            """
            # Keep

            See [missing](docs/missing.md).
            """);
        File.WriteAllText(
            Path.Combine(repo.Path, "docs", "skip", "skip.md"),
            """
            # Skip

            See [missing](docs/missing.md).
            """);

        var result = ValidationService.ValidateRepo(
            repo.Path,
            WorkbenchConfig.Default with { Validation = validation },
            new ValidationOptions(
                LinkInclude: new List<string> { "docs/" },
                LinkExclude: new List<string> { "docs/skip/" }));

        Assert.HasCount(1, result.Errors, string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(
            result.Errors.Any(error => error.Contains("broken local link", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, result.Errors));
    }

    [TestMethod]
    public void ValidateConfigAndSchemas_MissingAndMalformedInputs_ReturnExpectedErrors()
    {
        using var emptyRepo = new TempRepoRoot();
        Assert.IsEmpty(SchemaValidationService.ValidateConfig(emptyRepo.Path));

        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        File.WriteAllText(Path.Combine(repo.Path, ".workbench", "config.json"), "{");

        var configErrors = SchemaValidationService.ValidateConfig(repo.Path);
        Assert.IsTrue(configErrors.Any(error => error.Contains("schema validation error", StringComparison.Ordinal)), string.Join(Environment.NewLine, configErrors));

        var invalidJsonErrors = SchemaValidationService.ValidateJsonContent(
            repoRoot: repo.Path,
            schemaRelativePath: "specs/schemas/work-item-trace-fields.schema.json",
            context: "quality/testing",
            json: "{");
        Assert.IsTrue(invalidJsonErrors.Any(error => error.Contains("schema validation error", StringComparison.Ordinal)), string.Join(Environment.NewLine, invalidJsonErrors));

        using var missingSchemaRepo = new TempRepoRoot();
        var artifactErrors = SchemaValidationService.ValidateArtifactFrontMatter(
            missingSchemaRepo.Path,
            "specs/requirements/WB/SPEC-WB-0001.md",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["artifact_id"] = "SPEC-WB-0001",
                ["artifact_type"] = "specification",
                ["title"] = "Missing schema",
                ["domain"] = "WB",
                ["capability"] = "validation",
                ["status"] = "draft",
                ["owner"] = "platform"
            });
        Assert.IsTrue(artifactErrors.Any(error => error.Contains("artifact front matter schema not found", StringComparison.Ordinal)), string.Join(Environment.NewLine, artifactErrors));

        var workItemTraceErrors = SchemaValidationService.ValidateWorkItemTraceFields(
            missingSchemaRepo.Path,
            "trace-context",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Addresses"] = new[] { "REQ-WB-0001" }
            });
        Assert.IsEmpty(workItemTraceErrors);
    }

    [TestMethod]
    public void ValidateFrontMatterWrappers_AndAbsoluteSchemaPaths_AreAccepted()
    {
        using var repo = CreateRepoWithSchemas();
        var artifactPath = "specs/requirements/WB/SPEC-WB-STD.md";
        var frontMatter = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["artifact_id"] = "SPEC-WB-STD",
            ["artifact_type"] = "specification",
            ["title"] = "Workbench Standards Integration",
            ["domain"] = "WB",
            ["capability"] = "standards-integration",
            ["status"] = "draft",
            ["owner"] = "platform",
            ["related_artifacts"] = new List<string>
            {
                "SPEC-STD",
                "SPEC-SCH",
                "SPEC-LAY",
                "SPEC-TPL"
            }
        };

        Assert.IsEmpty(SchemaValidationService.ValidateDocFrontMatter(repo.Path, artifactPath, frontMatter));
        Assert.IsEmpty(SchemaValidationService.ValidateFrontMatter(repo.Path, artifactPath, frontMatter));

        var absoluteSchemaPath = Path.GetFullPath(Path.Combine(repo.Path, "specs", "schemas", "work-item-trace-fields.schema.json"));
        var errors = SchemaValidationService.ValidateJsonContent(
            repo.Path,
            absoluteSchemaPath,
            "quality/testing",
            """
            {
              "Addresses": ["REQ-WB-0001"],
              "Uses Design": ["ARC-WB-0001"],
              "Verified By": ["VER-WB-0001"]
            }
            """);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ValidateSchemaServices_InvalidArtifactAndTraceData_ReturnCollectedSchemaErrors()
    {
        using var repo = CreateRepoWithSchemas();

        var artifactErrors = SchemaValidationService.ValidateArtifactFrontMatter(
            repo.Path,
            "specs/requirements/WB/SPEC-WB-0001.md",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["artifact_id"] = 123,
                ["artifact_type"] = "specification",
                ["title"] = "Invalid artifact",
                ["domain"] = "WB",
                ["capability"] = "validation",
                ["status"] = "draft",
                ["owner"] = "platform"
            });

        Assert.IsGreaterThan(0, artifactErrors.Count, string.Join(Environment.NewLine, artifactErrors));
        Assert.IsTrue(
            artifactErrors.Any(error => error.Contains("Required properties", StringComparison.Ordinal) ||
                                        error.Contains("schema validation error", StringComparison.OrdinalIgnoreCase) ||
                                        error.Contains("artifact_id", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, artifactErrors));

        var traceErrors = SchemaValidationService.ValidateWorkItemTraceFields(
            repo.Path,
            "trace-context",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Addresses"] = new object?[] { 123 },
                ["Uses Design"] = "ARC-WB-0001",
                ["Verified By"] = Array.Empty<object?>()
            });

        Assert.IsGreaterThan(0, traceErrors.Count, string.Join(Environment.NewLine, traceErrors));
        Assert.IsTrue(
            traceErrors.Any(error => error.Contains("Value is", StringComparison.Ordinal) ||
                                     error.Contains("Value should", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, traceErrors));
    }

    [TestMethod]
    public void ValidateRequirementTraceFields_ReportsNullEmptyInvalidAndDuplicateValues()
    {
        var trace = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Verified By"] = null,
            ["Implemented By"] = Array.Empty<string>(),
            ["Derived From"] = new object?[] { "" },
            ["Related"] = new object?[] { "REQ-WB-0001", "REQ-WB-0001" }
        };

        var errors = SchemaValidationService.ValidateRequirementTraceFields(".", "trace-context", trace);

        Assert.IsTrue(errors.Any(error => error.Contains("is empty", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
        Assert.IsTrue(errors.Any(error => error.Contains("must contain at least one value", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
        Assert.IsTrue(errors.Any(error => error.Contains("contains an invalid value", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
        Assert.IsTrue(errors.Any(error => error.Contains("contains duplicate values", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ValidateJsonContent_MalformedJson_ReturnsSchemaValidationError()
    {
        using var repo = CreateRepoWithSchemas();
        var errors = SchemaValidationService.ValidateJsonContent(
            repoRoot: repo.Path,
            schemaRelativePath: "specs/schemas/work-item-trace-fields.schema.json",
            context: "quality/testing",
            json: "{");

        Assert.IsTrue(errors.Any(error => error.Contains("schema validation error", StringComparison.Ordinal)), string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ValidateRequirementTraceFields_ReportsInvalidTraceValues()
    {
        var trace = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Implemented By"] = new object?[] { "WI-WB-0001", "WI-WB-0001" },
            ["Unknown Label"] = new object?[] { "abc" },
            ["Verified By"] = "VER-WB-0001"
        };

        var errors = SchemaValidationService.ValidateRequirementTraceFields(".", "trace-context", trace);

        Assert.IsTrue(errors.Any(error => error.Contains("trace label 'Unknown Label' is not canonical.", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("must be an array of strings", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("contains duplicate values", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRequirementClause_ReportsUnsupportedKeywordAndMissingFields()
    {
        var clause = new SpecTraceMarkdown.RequirementClause(
            RequirementId: string.Empty,
            Title: string.Empty,
            Clause: string.Empty,
            NormativeKeyword: "WILL",
            Trace: null,
            Notes: null);

        var errors = SchemaValidationService.ValidateRequirementClause(".", "spec.md", clause);

        Assert.IsTrue(errors.Any(error => error.Contains("requirement_id is missing", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("requirement title is missing", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("requirement clause is missing", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("unsupported normative keyword 'WILL'", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateJsonContent_MissingSchema_ReturnsSchemaNotFound()
    {
        var errors = SchemaValidationService.ValidateJsonContent(
            repoRoot: ".",
            schemaRelativePath: "does/not/exist.schema.json",
            context: "quality/testing",
            json: "{}");

        Assert.IsTrue(errors.Any(error => error.Contains("schema not found at", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateConfig_ValidConfig_ReturnsNoErrors()
    {
        using var repo = CreateRepoWithSchemas();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        File.WriteAllText(
            Path.Combine(repo.Path, ".workbench", "config.json"),
            JsonSerializer.Serialize(WorkbenchConfig.Default, WorkbenchJsonContext.Default.WorkbenchConfig));

        var errors = SchemaValidationService.ValidateConfig(repo.Path);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ValidateWorkItemTraceFields_ValidTrace_ReturnsNoErrors()
    {
        var trace = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Addresses"] = new[] { "REQ-WB-0001" },
            ["Uses Design"] = new[] { "ARC-WB-0001" },
            ["Verified By"] = new[] { "VER-WB-0001" }
        };

        var errors = SchemaValidationService.ValidateWorkItemTraceFields(".", "trace-context", trace);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void ValidateJsonContent_ValidJson_ReturnsNoErrors()
    {
        using var repo = CreateRepoWithSchemas();
        var errors = SchemaValidationService.ValidateJsonContent(
            repoRoot: repo.Path,
            schemaRelativePath: "specs/schemas/work-item-trace-fields.schema.json",
            context: "quality/testing",
            json: """
            {
              "Addresses": ["REQ-WB-0001"],
              "Uses Design": ["ARC-WB-0001"],
              "Verified By": ["VER-WB-0001"]
            }
            """);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    private static TempRepoRoot CreateRepoWithSchemas()
    {
        var repo = new TempRepoRoot();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git"));

        var sourceRoot = FindRepoRoot();
        Directory.CreateDirectory(Path.Combine(repo.Path, "schemas"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "schemas"));
        File.Copy(
            Path.Combine(sourceRoot, "schemas", "workbench-config.schema.json"),
            Path.Combine(repo.Path, "schemas", "workbench-config.schema.json"));
        File.Copy(
            Path.Combine(sourceRoot, "specs", "schemas", "artifact-frontmatter.schema.json"),
            Path.Combine(repo.Path, "specs", "schemas", "artifact-frontmatter.schema.json"));
        File.Copy(
            Path.Combine(sourceRoot, "specs", "schemas", "work-item-trace-fields.schema.json"),
            Path.Combine(repo.Path, "specs", "schemas", "work-item-trace-fields.schema.json"));

        return repo;
    }

    private static void WriteConfig(string repoPath, ValidationConfig? validation = null)
    {
        var config = WorkbenchConfig.Default with
        {
            Validation = validation ?? new ValidationConfig()
        };

        Directory.CreateDirectory(Path.Combine(repoPath, ".workbench"));
        File.WriteAllText(
            Path.Combine(repoPath, ".workbench", "config.json"),
            JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig));
    }

    private static string CreateCanonicalWorkItemFrontMatter(string artifactId, string title, string status)
    {
        return $"""
                ---
                artifact_id: {artifactId}
                artifact_type: work_item
                title: {title}
                domain: WB
                status: {status}
                owner: platform
                addresses:
                  - REQ-WB-0001
                design_links:
                  - ARC-WB-0001
                verification_links:
                  - VER-WB-0001
                related_artifacts:
                  - SPEC-WB-STD
                ---

                # {artifactId} - {title}

                ## Summary
                Item summary.
                """;
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

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
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
