using System.Text.Json;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class AttestationServiceTests
{
    [TestMethod]
    public void Generate_BuildsSnapshotWithSeparatedTraceDirectRefsEvidenceAndRollups()
    {
        using var repo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: true);

        var preValidation = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);
        var result = AttestationService.Generate(
            repo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "both", "artifacts/quality/attestation", null, null, null, null, null, false, false));
        var postValidation = ValidationService.ValidateRepo(repo.Path, WorkbenchConfig.Default);
        var snapshot = result.Snapshot;

        Assert.HasCount(2, snapshot.Requirements);
        Assert.AreEqual(2, snapshot.Aggregates.Requirements);
        Assert.AreEqual(1, snapshot.Aggregates.TraceCoverage.WithSatisfiedBy);
        Assert.AreEqual(1, snapshot.Aggregates.TraceCoverage.WithImplementedBy);
        Assert.AreEqual(1, snapshot.Aggregates.TraceCoverage.WithVerifiedBy);
        Assert.AreEqual(2, snapshot.Aggregates.TraceCoverage.WithTestRefs);
        Assert.AreEqual(1, snapshot.Aggregates.TraceCoverage.WithCodeRefs);
        Assert.AreEqual(2, snapshot.SchemaVersion);
        Assert.AreEqual(Path.GetFileName(repo.Path), snapshot.Repository.DisplayName);

        var traceRequirement = snapshot.Requirements.Single(requirement => string.Equals(requirement.RequirementId, "REQ-WB-ATTEST-0001", StringComparison.OrdinalIgnoreCase));
        var refsRequirement = snapshot.Requirements.Single(requirement => string.Equals(requirement.RequirementId, "REQ-WB-ATTEST-0002", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual("specs/requirements/WB/SPEC-WB-ATTEST.md", traceRequirement.SpecificationRepoRelativePath);
        Assert.IsNull(traceRequirement.ValidationFindingIds);
        CollectionAssert.AreEquivalent(new[] { "ARC-WB-ATTEST-0001" }, traceRequirement.Trace.SatisfiedBy.ToArray());
        CollectionAssert.AreEquivalent(new[] { "WI-WB-ATTEST-0001", "WI-WB-ATTEST-0002", "WI-WB-ATTEST-0003", "WI-WB-ATTEST-0004", "WI-WB-ATTEST-0005" }, traceRequirement.Trace.ImplementedBy.ToArray());
        CollectionAssert.AreEquivalent(new[] { "VER-WB-ATTEST-0001", "VER-WB-ATTEST-0002", "VER-WB-ATTEST-0003", "VER-WB-ATTEST-0004", "VER-WB-ATTEST-0005" }, traceRequirement.Trace.VerifiedBy.ToArray());
        CollectionAssert.AreEquivalent(new[] { "tests/Sample.Tests/WidgetTests.cs::Adds_numbers" }, traceRequirement.DirectRefs.TestRefs.ToArray());
        Assert.IsEmpty(traceRequirement.DirectRefs.CodeRefs);
        Assert.AreEqual("failing", traceRequirement.TestEvidenceStatus);
        Assert.AreEqual("passing", traceRequirement.BenchmarkEvidenceStatus);
        Assert.AreEqual("passing", traceRequirement.ManualQaStatus);

        Assert.AreEqual("specs/requirements/WB/SPEC-WB-ATTEST-REFS.md", refsRequirement.SpecificationRepoRelativePath);
        CollectionAssert.AreEquivalent(new[] { "tests/Workbench.Tests/AttestationServiceTests.cs" }, refsRequirement.DirectRefs.TestRefs.ToArray());
        CollectionAssert.AreEquivalent(new[] { "src/Workbench.Core/AttestationService.cs" }, refsRequirement.DirectRefs.CodeRefs.ToArray());
        Assert.IsNotNull(refsRequirement.ValidationFindingIds);
        Assert.IsNotEmpty(refsRequirement.ValidationFindingIds);
        CollectionAssert.Contains(refsRequirement.Gaps.ToArray(), "no downstream trace links");

        Assert.IsNotNull(snapshot.DerivedRollups);
        Assert.IsTrue(snapshot.DerivedRollups!.ImplementedEnabled);
        Assert.IsTrue(snapshot.DerivedRollups.VerifiedEnabled);
        Assert.IsTrue(snapshot.DerivedRollups.ReleaseReadyEnabled);
        Assert.HasCount(1, snapshot.Requirements.Where(requirement => requirement.DerivedRollups?.Implemented == true));
        Assert.HasCount(1, snapshot.Requirements.Where(requirement => requirement.DerivedRollups?.Verified == true));
        Assert.IsEmpty(snapshot.Requirements.Where(requirement => requirement.DerivedRollups?.ReleaseReady == true));

        Assert.HasCount(preValidation.Errors.Count, postValidation.Errors);
        Assert.HasCount(preValidation.Findings.Count, postValidation.Findings);

        var json = JsonSerializer.Serialize(snapshot, AttestationJsonContext.Default.AttestationSnapshot);
        Assert.IsFalse(json.Contains(Environment.NewLine, StringComparison.Ordinal), json);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.IsTrue(document.RootElement.TryGetProperty("repository", out var repository));
        Assert.IsTrue(repository.TryGetProperty("displayName", out var displayName));
        Assert.AreEqual(Path.GetFileName(repo.Path), displayName.GetString());
        Assert.IsFalse(repository.TryGetProperty("root", out _));
        Assert.IsTrue(document.RootElement.TryGetProperty("aggregates", out var aggregates));
        Assert.IsTrue(aggregates.TryGetProperty("traceCoverage", out var traceCoverage));
        Assert.AreEqual(2, traceCoverage.GetProperty("requirements").GetInt32());
        Assert.IsTrue(document.RootElement.TryGetProperty("validation", out var validation));
        Assert.IsTrue(validation.TryGetProperty("findings", out var findings));
        Assert.IsGreaterThan(0, findings.GetArrayLength());
        Assert.IsTrue(document.RootElement.TryGetProperty("requirements", out var requirements));
        Assert.AreEqual(2, requirements.GetArrayLength());
        var requirement0 = requirements[0];
        Assert.IsTrue(requirement0.TryGetProperty("specificationRepoRelativePath", out _));
        Assert.IsTrue(requirement0.TryGetProperty("trace", out _));
        Assert.IsTrue(requirement0.TryGetProperty("directRefs", out _));
        Assert.IsFalse(requirement0.TryGetProperty("specificationPath", out _));
        Assert.IsFalse(requirement0.TryGetProperty("validationFindingIds", out _));
        Assert.IsFalse(requirement0.TryGetProperty("hasSatisfiedBy", out _));
        Assert.IsFalse(requirement0.TryGetProperty("hasImplementedBy", out _));
        Assert.IsFalse(requirement0.TryGetProperty("hasVerifiedBy", out _));
        Assert.IsFalse(requirement0.TryGetProperty("hasTestRefs", out _));
        Assert.IsFalse(requirement0.TryGetProperty("hasCodeRefs", out _));
        Assert.IsFalse(requirement0.TryGetProperty("linkedWorkItems", out _));
        Assert.IsFalse(requirement0.TryGetProperty("linkedVerifications", out _));
        var refsJson = requirements.EnumerateArray().Single(requirement => string.Equals(requirement.GetProperty("requirementId").GetString(), "REQ-WB-ATTEST-0002", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(refsJson.TryGetProperty("validationFindingIds", out var validationFindingIds));
        Assert.IsGreaterThan(0, validationFindingIds.GetArrayLength());
    }

    [TestMethod]
    public void Generate_DerivesRequirementEvidenceFromLinkedVerificationEvidence()
    {
        using var repo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: false, includeVerificationEvidenceRequirement: true);

        var result = AttestationService.Generate(
            repo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, false, false));

        var evidenceRequirement = result.Snapshot.Requirements.Single(requirement => string.Equals(requirement.RequirementId, "REQ-WB-ATTEST-0003", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(evidenceRequirement.Trace.VerifiedBy.Contains("VER-WB-ATTEST-0006", StringComparer.OrdinalIgnoreCase));
        Assert.IsTrue(evidenceRequirement.DirectRefs.TestRefs.Contains("tests/Sample.Tests/WidgetTests.cs", StringComparer.OrdinalIgnoreCase));
        Assert.IsTrue(evidenceRequirement.DirectRefs.CodeRefs.Contains("src/Sample/Widget.cs", StringComparer.OrdinalIgnoreCase));
        Assert.AreEqual("not-applicable", evidenceRequirement.BenchmarkEvidenceStatus);
        CollectionAssert.DoesNotContain(evidenceRequirement.Gaps.ToArray(), "benchmark evidence unknown");
        CollectionAssert.DoesNotContain(evidenceRequirement.Gaps.ToArray(), "no implementation evidence or direct refs");
    }

    [TestMethod]
    public void Generate_ScopeFiltersTheTargetSubtree()
    {
        using var repo = CreateAttestationRepo(includeOutsideScopeRequirement: true, legacyOnly: false, includeExecutionCommand: false);

        var unscoped = AttestationService.Generate(
            repo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, false, false));

        var scoped = AttestationService.Generate(
            repo.Path,
            new AttestationRunOptions(new List<string> { "specs/requirements/WB" }, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation-scope", null, null, null, null, null, false, false));

        Assert.HasCount(3, unscoped.Snapshot.Requirements);
        Assert.HasCount(2, scoped.Snapshot.Requirements);
        CollectionAssert.DoesNotContain(scoped.Snapshot.Requirements.Select(requirement => requirement.RequirementId).ToList(), "REQ-UX-LEGACY-0001");
    }

    [TestMethod]
    public void Generate_GroupsRepeatedValidationFindingsAcrossRequirementIds()
    {
        using var repo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: false);
        WriteGroupedValidationArtifacts(repo.Path);

        var result = AttestationService.Generate(
            repo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, false, false));

        var snapshot = result.Snapshot;
        var groupedRequirementIds = new[] { "REQ-WB-GROUP-0001", "REQ-WB-GROUP-0002" };

        var groupedRequirements = snapshot.Requirements
            .Where(requirement => groupedRequirementIds.Contains(requirement.RequirementId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.HasCount(2, groupedRequirements);
        var groupedFindings = snapshot.Validation.Findings
            .Where(finding => finding.RequirementIds is not null &&
                finding.RequirementIds.Count == 2 &&
                groupedRequirementIds.All(requirementId => finding.RequirementIds.Contains(requirementId, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        Assert.IsNotEmpty(groupedFindings);
        var groupedFindingIds = groupedFindings.Select(finding => finding.FindingId).ToArray();

        foreach (var requirement in groupedRequirements)
        {
            Assert.IsNotNull(requirement.ValidationFindingIds);
            Assert.IsGreaterThanOrEqualTo(groupedFindingIds.Length, requirement.ValidationFindingIds!.Count);
            foreach (var findingId in groupedFindingIds)
            {
                CollectionAssert.Contains(requirement.ValidationFindingIds.ToArray(), findingId);
            }
        }

        foreach (var finding in groupedFindings)
        {
            Assert.AreEqual("specs/requirements/WB/SPEC-WB-GROUPED.md", finding.RepoRelativePath);
            Assert.IsNull(finding.ArtifactId);
            Assert.IsNull(finding.TargetId);
            Assert.IsNull(finding.TargetFile);
        }
    }

    [TestMethod]
    public void Generate_ExecutionIsExplicit_ConflictFails_AndLegacyReposStillProduceSnapshots()
    {
        using var noExecRepo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: true);
        var noExec = AttestationService.Generate(
            noExecRepo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, false, false));

        Assert.IsFalse(noExec.Snapshot.Evidence.Execution.Requested);
        Assert.IsFalse(File.Exists(Path.Combine(noExecRepo.Path, "quality", "attestation-exec-marker.txt")));

        using var execRepo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: true);
        var exec = AttestationService.Generate(
            execRepo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, true, false));

        Assert.IsTrue(exec.Snapshot.Evidence.Execution.Requested);
        Assert.IsTrue(exec.Snapshot.Evidence.Execution.Performed);
        Assert.HasCount(1, exec.Snapshot.Evidence.Execution.Commands);
        Assert.IsTrue(File.Exists(Path.Combine(execRepo.Path, "quality", "attestation-exec-marker.txt")));

        using var conflictRepo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: false, includeExecutionCommand: true);
        try
        {
            _ = AttestationService.Generate(
                conflictRepo.Path,
                new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, true, true));
            Assert.Fail("Expected the attestation execution options conflict to throw.");
        }
        catch (InvalidOperationException ex)
        {
            StringAssert.Contains(ex.Message, "Attestation execution options conflict", StringComparison.OrdinalIgnoreCase);
        }

        using var legacyRepo = CreateAttestationRepo(includeOutsideScopeRequirement: false, legacyOnly: true, includeExecutionCommand: false);
        var legacy = AttestationService.Generate(
            legacyRepo.Path,
            new AttestationRunOptions(null, ValidationProfiles.Auditable, "json", "artifacts/quality/attestation", null, null, null, null, null, false, false));

        Assert.HasCount(1, legacy.Snapshot.Requirements);
        Assert.AreEqual("REQ-WB-LEGACY-0001", legacy.Snapshot.Requirements[0].RequirementId);
        Assert.IsEmpty(legacy.Snapshot.Requirements[0].Trace.SatisfiedBy);
        Assert.IsEmpty(legacy.Snapshot.Requirements[0].Trace.ImplementedBy);
        Assert.IsEmpty(legacy.Snapshot.Requirements[0].Trace.VerifiedBy);
    }

    private static TempRepoRoot CreateAttestationRepo(bool includeOutsideScopeRequirement, bool legacyOnly, bool includeExecutionCommand, bool includeVerificationEvidenceRequirement = false)
    {
        var repo = new TempRepoRoot();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git"));
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "schemas"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "specs", "schemas"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality", "benchmarks"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality", "manual-qa"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "test-results"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "coverage"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "src", "Sample"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "tests", "Sample.Tests"));

        CopySchemas(repo.Path);
        WriteWorkbenchConfig(repo.Path);
        WriteQualityIntent(repo.Path);
        WriteAttestationConfig(repo.Path, includeExecutionCommand);
        WriteSampleSource(repo.Path);
        WriteSampleResultsArtifact(repo.Path);
        WriteSampleCoverageArtifact(repo.Path);
        WriteBenchmarkEvidence(repo.Path);
        WriteManualQaEvidence(repo.Path);
        WriteCanonicalArtifacts(repo.Path, includeOutsideScopeRequirement, legacyOnly, includeVerificationEvidenceRequirement);
        _ = QualityService.Sync(repo.Path, new QualitySyncOptions(null, "artifacts/raw/test-results", "artifacts/raw/coverage", null, false));
        return repo;
    }

    private static void CopySchemas(string repoRoot)
    {
        var sourceRoot = FindSourceRepoRoot();
        foreach (var relative in new[]
        {
            Path.Combine("schemas", "workbench-config.schema.json"),
            Path.Combine("specs", "schemas", "artifact-frontmatter.schema.json"),
            Path.Combine("specs", "schemas", "work-item-trace-fields.schema.json"),
            Path.Combine("schemas", "test-inventory.schema.json"),
            Path.Combine("schemas", "test-run-summary.schema.json"),
            Path.Combine("schemas", "coverage-summary.schema.json"),
            Path.Combine("schemas", "quality-report.schema.json")
        })
        {
            var source = Path.Combine(sourceRoot, relative);
            var destination = Path.Combine(repoRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void WriteWorkbenchConfig(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, ".workbench", "config.json"), JsonSerializer.Serialize(WorkbenchConfig.Default, WorkbenchJsonContext.Default.WorkbenchConfig));
    }

    private static void WriteQualityIntent(string repoRoot)
    {
        File.WriteAllText(
            Path.Combine(repoRoot, "quality", "testing-intent.yaml"),
            """
            version: 2
            domain: testing
            scope:
              includes:
                - src/Sample
                - tests/Sample.Tests
            expectations:
              evidence:
                - inventory
                - results
                - coverage
            coverage:
              lineMin: 0.50
              branchMin: 0.50
              criticalFiles:
                - src/Sample/Widget.cs
            """);
    }

    private static void WriteAttestationConfig(string repoRoot, bool includeExecutionCommand)
    {
        var lines = new List<string>
        {
            "rollups:",
            "  implemented: true",
            "  verified: true",
            "  releaseReady:",
            "    enabled: true",
            "    requireNoOpenWorkItems: true",
            "    requireNoValidationErrors: true",
            "    requireNoFailingVerifications: true",
            "    requireNoStaleEvidence: false"
        };

        if (includeExecutionCommand)
        {
            lines.Add(string.Empty);
            lines.Add("execution:");
            lines.Add("  tests:");
            lines.Add("    command: pwsh");
            lines.Add("    args:");
            lines.Add("      - \"-NoLogo\"");
            lines.Add("      - \"-NoProfile\"");
            lines.Add("      - \"-Command\"");
            lines.Add("      - \"Set-Content -Path 'quality/attestation-exec-marker.txt' -Value 'executed'\"");
        }

        File.WriteAllText(Path.Combine(repoRoot, "quality", "attestation.yaml"), string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void WriteSampleSource(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "src", "Sample", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repoRoot, "src", "Sample", "Widget.cs"), """
            namespace Sample;

            public class Widget
            {
                public int Add(int left, int right) => left + right;
            }
            """);
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "Sample.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
                <PackageReference Include="xunit" Version="2.9.2" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "WidgetTests.cs"), """
            using Xunit;

            namespace Sample.Tests;

            public class WidgetTests
            {
                [Fact]
                [Trait("Requirement", "REQ-WB-ATTEST-0001")]
                public void Adds_numbers() { }
                [Fact] public void Handles_zero() { }
            }
            """);
    }

    private static void WriteSampleResultsArtifact(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "artifacts", "raw", "test-results", "sample-results.trx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun id="6c574911-54e4-4f7a-bc3a-07dc30e803c7" name="quality-sample" runUser="workbench" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Times creation="2026-03-07T16:00:00.0000000+00:00" start="2026-03-07T16:00:01.0000000+00:00" finish="2026-03-07T16:00:03.0000000+00:00" />
              <TestDefinitions>
                <UnitTest name="Sample.Tests.WidgetTests.Adds_numbers" storage="Sample.Tests.dll" id="11111111-1111-1111-1111-111111111111">
                  <Execution id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" />
                  <TestMethod codeBase="Sample.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" className="Sample.Tests.WidgetTests" name="Adds_numbers" />
                </UnitTest>
                <UnitTest name="Sample.Tests.WidgetTests.Handles_zero" storage="Sample.Tests.dll" id="22222222-2222-2222-2222-222222222222">
                  <Execution id="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" />
                  <TestMethod codeBase="Sample.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" className="Sample.Tests.WidgetTests" name="Handles_zero" />
                </UnitTest>
              </TestDefinitions>
              <Results>
                <UnitTestResult executionId="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" testId="11111111-1111-1111-1111-111111111111" testName="Sample.Tests.WidgetTests.Adds_numbers" outcome="Passed" />
                <UnitTestResult executionId="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" testId="22222222-2222-2222-2222-222222222222" testName="Sample.Tests.WidgetTests.Handles_zero" outcome="Failed" />
              </Results>
              <ResultSummary outcome="Failed">
                <Counters total="2" executed="2" passed="1" failed="1" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="2" inProgress="0" pending="0" />
              </ResultSummary>
            </TestRun>
            """);
    }

    private static void WriteSampleCoverageArtifact(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "artifacts", "raw", "coverage", "sample-coverage.cobertura.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.75" branch-rate="0.5" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="2" version="1.9" timestamp="1772899200">
              <packages>
                <package name="Sample" line-rate="0.75" branch-rate="0.5">
                  <classes>
                    <class name="Sample.Widget" filename="src/Sample/Widget.cs" line-rate="0.75" branch-rate="0.5" />
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
    }

    private static void WriteBenchmarkEvidence(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "quality", "benchmarks", "benchmark.md"), """
            ---
            status: passed
            ---

            Benchmark completed successfully.
            """);
    }

    private static void WriteManualQaEvidence(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "quality", "manual-qa", "manual-qa.md"), """
            ---
            status: passed
            ---

            Manual QA completed successfully.
            """);
    }

    private static void WriteCanonicalArtifacts(string repoRoot, bool includeOutsideScopeRequirement, bool legacyOnly, bool includeVerificationEvidenceRequirement)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "architecture", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "work-items", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "verification", "WB"));

        if (legacyOnly)
        {
            File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-LEGACY.md"), """
                ---
                artifact_id: SPEC-WB-LEGACY
                artifact_type: specification
                title: Legacy attestation coverage
                domain: WB
                capability: validation
                status: draft
                owner: platform
                ---

                # SPEC-WB-LEGACY - Legacy attestation coverage

                ## REQ-WB-LEGACY-0001 Requirement title
                The tool MUST build a snapshot even when historical work items are absent.

                Trace:
                - Test Refs:
                  - tests/Workbench.Tests/AttestationServiceTests.cs
                - Code Refs:
                  - src/Workbench.Core/AttestationService.cs
                """);
            return;
        }

        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-ATTEST.md"), """
            ---
            artifact_id: SPEC-WB-ATTEST
            artifact_type: specification
            title: Attestation snapshot coverage
            domain: WB
            capability: validation
            status: draft
            owner: platform
            related_artifacts:
              - SPEC-WB-ATTEST-REFS
            ---

            # SPEC-WB-ATTEST - Attestation snapshot coverage

            ## REQ-WB-ATTEST-0001 Requirement title
            The tool MUST expose attestation snapshots without mutating canonical trace structure.

            Trace:
            - Satisfied By:
              - ARC-WB-ATTEST-0001
            - Implemented By:
              - WI-WB-ATTEST-0001
              - WI-WB-ATTEST-0002
              - WI-WB-ATTEST-0003
              - WI-WB-ATTEST-0004
              - WI-WB-ATTEST-0005
            - Verified By:
              - VER-WB-ATTEST-0001
              - VER-WB-ATTEST-0002
              - VER-WB-ATTEST-0003
              - VER-WB-ATTEST-0004
              - VER-WB-ATTEST-0005
            """);

        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-ATTEST-REFS.md"), """
            ---
            artifact_id: SPEC-WB-ATTEST-REFS
            artifact_type: specification
            title: Direct refs stay separate
            domain: WB
            capability: validation
            status: draft
            owner: platform
            ---

            # SPEC-WB-ATTEST-REFS - Direct refs stay separate

            ## REQ-WB-ATTEST-0002 Requirement title
            The tool MUST report Test Refs and Code Refs without treating them as canonical downstream edges.

            Trace:
            - Test Refs:
              - tests/Workbench.Tests/AttestationServiceTests.cs
            - Code Refs:
              - src/Workbench.Core/AttestationService.cs
            """);

        if (includeVerificationEvidenceRequirement)
        {
            File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-ATTEST-EVIDENCE.md"), """
                ---
                artifact_id: SPEC-WB-ATTEST-EVIDENCE
                artifact_type: specification
                title: Verification evidence rolls up into reports
                domain: WB
                capability: validation
                status: draft
                owner: platform
                ---

                # SPEC-WB-ATTEST-EVIDENCE - Verification evidence rolls up into reports

                ## REQ-WB-ATTEST-0003 Requirement title
                The tool MUST derive report-visible test and code refs from linked verification evidence.

                Trace:
                - Verified By:
                  - VER-WB-ATTEST-0006
                """);

            File.WriteAllText(Path.Combine(repoRoot, "specs", "verification", "WB", "VER-WB-ATTEST-0006.md"), """
                ---
                artifact_id: VER-WB-ATTEST-0006
                artifact_type: verification
                title: VER-WB-ATTEST-0006
                domain: WB
                status: passed
                owner: platform
                verifies:
                  - REQ-WB-ATTEST-0003
                ---

                # VER-WB-ATTEST-0006 - VER-WB-ATTEST-0006

                ## Scope

                Verification evidence propagation through derived attestation reports.

                ## Requirements Verified

                - REQ-WB-ATTEST-0003

                ## Verification Method

                Documentation review.

                ## Preconditions

                - The sample repository fixtures exist.

                ## Procedure or Approach

                - Review the linked verification evidence entries.

                ## Expected Result

                - The attestation report shows the linked test and code refs and suppresses benchmark-unknown noise.

                ## Evidence

                - [`tests/Sample.Tests/WidgetTests.cs`](../../../tests/Sample.Tests/WidgetTests.cs)
                - [`src/Sample/Widget.cs`](../../../src/Sample/Widget.cs)
                - benchmark: not-applicable

                ## Status

                passed

                ## Related Artifacts

                - [`SPEC-WB-ATTEST-EVIDENCE`](../../requirements/WB/SPEC-WB-ATTEST-EVIDENCE.md)
                """);
        }

        File.WriteAllText(Path.Combine(repoRoot, "specs", "architecture", "WB", "ARC-WB-ATTEST-0001.md"), """
            ---
            artifact_id: ARC-WB-ATTEST-0001
            artifact_type: architecture
            title: Attestation architecture
            domain: WB
            status: approved
            owner: platform
            satisfies:
              - REQ-WB-ATTEST-0001
            ---

            # ARC-WB-ATTEST-0001 - Attestation architecture
            """);

        foreach (var (artifactId, status) in new[]
        {
            ("WI-WB-ATTEST-0001", "complete"),
            ("WI-WB-ATTEST-0002", "in_progress"),
            ("WI-WB-ATTEST-0003", "planned"),
            ("WI-WB-ATTEST-0004", "blocked"),
            ("WI-WB-ATTEST-0005", "mystery")
        })
        {
            File.WriteAllText(Path.Combine(repoRoot, "specs", "work-items", "WB", $"{artifactId}.md"), $"""
                ---
                artifact_id: {artifactId}
                artifact_type: work_item
                title: {artifactId}
                domain: WB
                status: {status}
                owner: platform
                addresses:
                  - REQ-WB-ATTEST-0001
                design_links:
                  - ARC-WB-ATTEST-0001
                verification_links:
                  - VER-WB-ATTEST-0001
                ---

                # {artifactId} - {artifactId}
                """);
        }

        foreach (var (artifactId, status) in new[]
        {
            ("VER-WB-ATTEST-0001", "passed"),
            ("VER-WB-ATTEST-0002", "failed"),
            ("VER-WB-ATTEST-0003", "planned"),
            ("VER-WB-ATTEST-0004", "obsolete"),
            ("VER-WB-ATTEST-0005", "mystery")
        })
        {
            File.WriteAllText(Path.Combine(repoRoot, "specs", "verification", "WB", $"{artifactId}.md"), $"""
                ---
                artifact_id: {artifactId}
                artifact_type: verification
                title: {artifactId}
                domain: WB
                status: {status}
                owner: platform
                verifies:
                  - REQ-WB-ATTEST-0001
                ---

                # {artifactId} - {artifactId}
                """);
        }

        if (includeOutsideScopeRequirement)
        {
            Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "UX"));
            File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "UX", "SPEC-UX-LEGACY.md"), """
                ---
                artifact_id: SPEC-UX-LEGACY
                artifact_type: specification
                title: Outside scope requirement
                domain: UX
                capability: validation
                status: draft
                owner: platform
                ---

                # SPEC-UX-LEGACY - Outside scope requirement

                ## REQ-UX-LEGACY-0001 Requirement title
                The tool MUST ignore unrelated requirement subtrees when a scope is selected.
                """);
        }
    }

    private static void WriteGroupedValidationArtifacts(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-GROUPED.md"), """
            ---
            artifact_id: SPEC-WB-GROUPED
            artifact_type: specification
            title: Grouped validation findings
            domain: WB
            capability: validation
            status: draft
            owner: platform
            ---

            # SPEC-WB-GROUPED - Grouped validation findings

            ## REQ-WB-GROUP-0001 First grouped requirement
            The tool MUST report repeated validation findings once.

            ## REQ-WB-GROUP-0002 Second grouped requirement
            The tool MUST report repeated validation findings once.
            """);
    }

    private static string FindSourceRepoRoot()
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

        throw new DirectoryNotFoundException("Could not locate the Workbench repo root.");
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
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
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
