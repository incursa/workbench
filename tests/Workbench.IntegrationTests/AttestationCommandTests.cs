using System.Linq;
using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class AttestationCommandTests
{
    [TestMethod]
    public void AttestationCommand_WritesHtmlAndJsonSnapshot_WithSeparatedEvidenceAndDirectRefs()
    {
        using var repo = CreateFixtureRepo(includeOutsideScopeRequirement: false, includeExecutionCommand: false);

        var sync = RunQualitySync(repo.Path);
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var result = WorkbenchCli.Run(repo.Path, "quality", "attest", "--format", "json");
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");

        using var envelope = JsonDocument.Parse(result.StdOut);
        Assert.IsTrue(envelope.RootElement.GetProperty("ok").GetBoolean());

        var data = envelope.RootElement.GetProperty("data");
        var snapshot = data.GetProperty("snapshot");

        Assert.AreEqual("attestation", snapshot.GetProperty("domain").GetString());
        Assert.AreEqual(2, snapshot.GetProperty("requirements").GetArrayLength());

        var aggregates = snapshot.GetProperty("aggregates");
        Assert.AreEqual(2, aggregates.GetProperty("requirements").GetInt32());

        var traceCoverage = aggregates.GetProperty("traceCoverage");
        Assert.AreEqual(2, traceCoverage.GetProperty("requirements").GetInt32());
        Assert.AreEqual(1, traceCoverage.GetProperty("withSatisfiedBy").GetInt32());
        Assert.AreEqual(1, traceCoverage.GetProperty("withImplementedBy").GetInt32());
        Assert.AreEqual(1, traceCoverage.GetProperty("withVerifiedBy").GetInt32());
        Assert.AreEqual(1, traceCoverage.GetProperty("withTestRefs").GetInt32());
        Assert.AreEqual(1, traceCoverage.GetProperty("withCodeRefs").GetInt32());

        var evidence = snapshot.GetProperty("evidence");
        Assert.AreEqual("failed", evidence.GetProperty("testResults").GetProperty("status").GetString());
        Assert.IsTrue(evidence.GetProperty("coverage").GetProperty("present").GetBoolean());
        Assert.AreEqual("passing", evidence.GetProperty("benchmarks").GetProperty("status").GetString());
        Assert.AreEqual("passing", evidence.GetProperty("manualQa").GetProperty("status").GetString());

        Assert.IsTrue(snapshot.TryGetProperty("derivedRollups", out var derivedRollups));
        Assert.IsTrue(derivedRollups.GetProperty("implementedEnabled").GetBoolean());
        Assert.IsTrue(derivedRollups.GetProperty("verifiedEnabled").GetBoolean());
        Assert.IsTrue(derivedRollups.GetProperty("releaseReadyEnabled").GetBoolean());

        var requirements = snapshot.GetProperty("requirements");
        var traceRequirement = requirements.EnumerateArray().Single(requirement => string.Equals(requirement.GetProperty("requirementId").GetString(), "REQ-WB-ATTEST-0001", StringComparison.OrdinalIgnoreCase));
        var refsRequirement = requirements.EnumerateArray().Single(requirement => string.Equals(requirement.GetProperty("requirementId").GetString(), "REQ-WB-ATTEST-0002", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(traceRequirement.GetProperty("hasSatisfiedBy").GetBoolean());
        Assert.IsTrue(traceRequirement.GetProperty("hasImplementedBy").GetBoolean());
        Assert.IsTrue(traceRequirement.GetProperty("hasVerifiedBy").GetBoolean());
        Assert.IsFalse(traceRequirement.GetProperty("hasTestRefs").GetBoolean());
        Assert.IsFalse(traceRequirement.GetProperty("hasCodeRefs").GetBoolean());
        Assert.AreEqual(1, traceRequirement.GetProperty("trace").GetProperty("satisfiedBy").GetArrayLength());
        Assert.AreEqual(0, traceRequirement.GetProperty("directRefs").GetProperty("testRefs").GetArrayLength());

        Assert.IsFalse(refsRequirement.GetProperty("hasSatisfiedBy").GetBoolean());
        Assert.IsFalse(refsRequirement.GetProperty("hasImplementedBy").GetBoolean());
        Assert.IsFalse(refsRequirement.GetProperty("hasVerifiedBy").GetBoolean());
        Assert.IsTrue(refsRequirement.GetProperty("hasTestRefs").GetBoolean());
        Assert.IsTrue(refsRequirement.GetProperty("hasCodeRefs").GetBoolean());
        Assert.AreEqual(0, refsRequirement.GetProperty("trace").GetProperty("satisfiedBy").GetArrayLength());
        Assert.AreEqual(1, refsRequirement.GetProperty("directRefs").GetProperty("testRefs").GetArrayLength());

        var summaryPath = Path.Combine(repo.Path, "artifacts", "quality", "attestation", "summary.html");
        var detailsPath = Path.Combine(repo.Path, "artifacts", "quality", "attestation", "details.html");
        var jsonPath = Path.Combine(repo.Path, "artifacts", "quality", "attestation", "attestation.json");

        Assert.IsTrue(File.Exists(summaryPath), summaryPath);
        Assert.IsTrue(File.Exists(detailsPath), detailsPath);
        Assert.IsTrue(File.Exists(jsonPath), jsonPath);

        var summaryHtml = File.ReadAllText(summaryPath);
        var detailsHtml = File.ReadAllText(detailsPath);

        StringAssert.Contains(summaryHtml, "<!doctype html>", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(summaryHtml.Contains("<script", StringComparison.OrdinalIgnoreCase), summaryHtml);

        StringAssert.Contains(detailsHtml, "<details", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(detailsHtml, "<summary>", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(detailsHtml, "REQ-WB-ATTEST-0001", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(detailsHtml, "REQ-WB-ATTEST-0002", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(detailsHtml, "Test refs", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(detailsHtml, "Code refs", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(detailsHtml.Contains("<script", StringComparison.OrdinalIgnoreCase), detailsHtml);
    }

    [TestMethod]
    public void AttestationCommand_ScopedRunIsolatesTheTargetSubtree()
    {
        using var repo = CreateFixtureRepo(includeOutsideScopeRequirement: true, includeExecutionCommand: false);

        var sync = RunQualitySync(repo.Path);
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var unscoped = WorkbenchCli.Run(repo.Path, "quality", "attest", "--format", "json");
        Assert.AreEqual(0, unscoped.ExitCode, $"stderr: {unscoped.StdErr}\nstdout: {unscoped.StdOut}");

        using var unscopedJson = JsonDocument.Parse(unscoped.StdOut);
        var unscopedSnapshot = unscopedJson.RootElement.GetProperty("data").GetProperty("snapshot");
        Assert.AreEqual(3, unscopedSnapshot.GetProperty("requirements").GetArrayLength());

        var scoped = WorkbenchCli.Run(
            repo.Path,
            "quality",
            "attest",
            "--format",
            "json",
            "--scope",
            "specs/requirements/WB");

        Assert.AreEqual(0, scoped.ExitCode, $"stderr: {scoped.StdErr}\nstdout: {scoped.StdOut}");

        using var scopedJson = JsonDocument.Parse(scoped.StdOut);
        var scopedSnapshot = scopedJson.RootElement.GetProperty("data").GetProperty("snapshot");
        Assert.AreEqual(2, scopedSnapshot.GetProperty("requirements").GetArrayLength());

        var requirementIds = scopedSnapshot.GetProperty("requirements").EnumerateArray().Select(requirement => requirement.GetProperty("requirementId").GetString()).ToList();
        CollectionAssert.DoesNotContain(requirementIds, "REQ-UX-LEGACY-0001");

        var detailsPath = Path.Combine(repo.Path, "artifacts", "quality", "attestation", "details.html");
        var detailsHtml = File.ReadAllText(detailsPath);
        Assert.IsFalse(detailsHtml.Contains("REQ-UX-LEGACY-0001", StringComparison.OrdinalIgnoreCase), detailsHtml);
    }

    [TestMethod]
    public void AttestationCommand_ExplicitExecutionIsSafeAndConflictsAreRejected()
    {
        using var repo = CreateFixtureRepo(includeOutsideScopeRequirement: false, includeExecutionCommand: true);

        var dryRun = WorkbenchCli.Run(repo.Path, "quality", "attest", "--format", "json");
        Assert.AreEqual(0, dryRun.ExitCode, $"stderr: {dryRun.StdErr}\nstdout: {dryRun.StdOut}");

        using var dryRunJson = JsonDocument.Parse(dryRun.StdOut);
        var dryRunSnapshot = dryRunJson.RootElement.GetProperty("data").GetProperty("snapshot");
        Assert.IsFalse(dryRunSnapshot.GetProperty("evidence").GetProperty("execution").GetProperty("requested").GetBoolean());
        Assert.IsFalse(File.Exists(Path.Combine(repo.Path, "quality", "attestation-exec-marker.txt")));

        var exec = WorkbenchCli.Run(repo.Path, "quality", "attest", "--format", "json", "--exec");
        Assert.AreEqual(0, exec.ExitCode, $"stderr: {exec.StdErr}\nstdout: {exec.StdOut}");

        using var execJson = JsonDocument.Parse(exec.StdOut);
        var execSnapshot = execJson.RootElement.GetProperty("data").GetProperty("snapshot");
        var execution = execSnapshot.GetProperty("evidence").GetProperty("execution");
        Assert.IsTrue(execution.GetProperty("requested").GetBoolean());
        Assert.IsTrue(execution.GetProperty("performed").GetBoolean());
        Assert.AreEqual(1, execution.GetProperty("commands").GetArrayLength());
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "quality", "attestation-exec-marker.txt")));

        var conflict = WorkbenchCli.Run(repo.Path, "quality", "attest", "--format", "json", "--exec", "--no-exec");
        Assert.AreEqual(2, conflict.ExitCode, $"stderr: {conflict.StdErr}\nstdout: {conflict.StdOut}");

        using var conflictJson = JsonDocument.Parse(conflict.StdOut);
        var error = conflictJson.RootElement.GetProperty("error");
        Assert.AreEqual("unexpected_error", error.GetProperty("code").GetString());
        StringAssert.Contains(error.GetProperty("message").GetString()!, "Attestation execution options conflict", StringComparison.OrdinalIgnoreCase);
    }

    private static CommandResult RunQualitySync(string repoPath)
    {
        return WorkbenchCli.Run(
            repoPath,
            "quality",
            "sync",
            "--results",
            "artifacts/raw/test-results",
            "--coverage",
            "artifacts/raw/coverage",
            "--format",
            "json");
    }

    private static TempRepo CreateFixtureRepo(bool includeOutsideScopeRequirement, bool includeExecutionCommand)
    {
        var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        Directory.CreateDirectory(Path.Combine(repo.Path, "schemas"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality", "benchmarks"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality", "manual-qa"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "test-results"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "coverage"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "src", "Sample"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "tests", "Sample.Tests"));

        CopySchemas(repo.Path);
        WriteQualityIntent(repo.Path);
        WriteAttestationConfig(repo.Path, includeExecutionCommand);
        WriteSampleSource(repo.Path);
        WriteSampleResultsArtifact(repo.Path);
        WriteSampleCoverageArtifact(repo.Path);
        WriteBenchmarkEvidence(repo.Path);
        WriteManualQaEvidence(repo.Path);
        WriteCanonicalArtifacts(repo.Path, includeOutsideScopeRequirement);

        return repo;
    }

    private static void CopySchemas(string repoRoot)
    {
        var sourceRoot = FindSourceRepoRoot();
        foreach (var schema in new[]
        {
            "test-inventory.schema.json",
            "test-run-summary.schema.json",
            "coverage-summary.schema.json",
            "quality-report.schema.json"
        })
        {
            File.Copy(Path.Combine(sourceRoot, "schemas", schema), Path.Combine(repoRoot, "schemas", schema), overwrite: true);
        }
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

            scenarios:
              requiredTests:
                - tests/Sample.Tests/WidgetTests.cs::Adds_numbers
                - tests/Sample.Tests/WidgetTests.cs::Handles_zero
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
                [Fact] public void Adds_numbers() { }
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

    private static void WriteCanonicalArtifacts(string repoRoot, bool includeOutsideScopeRequirement)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "architecture", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "work-items", "WB"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "verification", "WB"));

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

}
