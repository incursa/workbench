using System.Text.Json;

namespace Workbench.IntegrationTests;

[TestClass]
public class QualityCommandTests
{
    [TestMethod]
    public void QualitySync_JsonOutput_WritesArtifactsAndSummary()
    {
        using var repo = CreateFixtureRepo();

        var result = WorkbenchCli.Run(
            repo.Path,
            "quality",
            "sync",
            "--results",
            "artifacts/raw/test-results",
            "--coverage",
            "artifacts/raw/coverage",
            "--format",
            "json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        Assert.IsTrue(json.RootElement.GetProperty("ok").GetBoolean());
        var data = json.RootElement.GetProperty("data");
        Assert.AreEqual(1, data.GetProperty("inventory").GetProperty("projects").GetInt32());
        Assert.AreEqual(2, data.GetProperty("inventory").GetProperty("tests").GetInt32());
        Assert.AreEqual("failed", data.GetProperty("results").GetProperty("status").GetString());
        Assert.AreEqual("fail", data.GetProperty("report").GetProperty("status").GetString());

        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing", "test-inventory.json")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing", "test-run-summary.json")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing", "coverage-summary.json")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing", "quality-report.json")));
        Assert.IsTrue(File.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing", "quality-summary.md")));
    }

    [TestMethod]
    public void QualitySync_JsonOutput_IncludesGeneratedRequirementComments()
    {
        using var repo = CreateFixtureRepo();
        WriteRequirementCommentJsonSpec(repo.Path);
        WriteRequirementCommentSource(repo.Path);

        var result = RunQualitySync(repo.Path, "--sync-requirement-comments", "--format", "json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var data = json.RootElement.GetProperty("data");
        var traceSync = data.GetProperty("traceSync");
        Assert.AreEqual(0, traceSync.GetProperty("specifications").GetProperty("filesUpdated").GetInt32());
        Assert.AreEqual(1, traceSync.GetProperty("testRequirementComments").GetProperty("filesUpdated").GetInt32());

        var updatedTest = File.ReadAllText(Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs"));
        StringAssert.Contains(
            updatedTest,
            "requirementId=\"REQ-SAMPLE-0004\">The widget test class MUST be documented with generated requirement comments.",
            StringComparison.Ordinal);
        StringAssert.Contains(
            updatedTest,
            "requirementId=\"REQ-SAMPLE-0001\">The system MUST verify addition behavior.",
            StringComparison.Ordinal);
        StringAssert.Contains(
            updatedTest,
            "requirementId=\"REQ-SAMPLE-0002\">The system MUST document zero handling.",
            StringComparison.Ordinal);
        StringAssert.Contains(
            updatedTest,
            "requirementId=\"REQ-SAMPLE-0003\">The system MUST document the fallback path.",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualitySync_JsonOutput_IncludesGeneratedRequirementComments_ForEmptyTestFile()
    {
        using var repo = CreateFixtureRepo();
        WriteRequirementCommentJsonSpec(repo.Path);
        WriteRequirementCommentEmptySource(repo.Path);

        var result = RunQualitySync(repo.Path, "--sync-requirement-comments", "--format", "json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var data = json.RootElement.GetProperty("data");
        var traceSync = data.GetProperty("traceSync");
        Assert.AreEqual(1, traceSync.GetProperty("testRequirementComments").GetProperty("filesUpdated").GetInt32());

        var updatedTest = File.ReadAllText(Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs"));
        StringAssert.Contains(
            updatedTest,
            "<workbench-requirements generated=\"true\" source=\"workbench quality sync\">",
            StringComparison.Ordinal);
        StringAssert.Contains(
            updatedTest,
            "requirementId=\"REQ-SAMPLE-0004\">The widget test class MUST be documented with generated requirement comments.",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualityShow_JsonOutput_ReturnsReportAndInventoryArtifacts()
    {
        using var repo = CreateFixtureRepo();

        var sync = RunQualitySync(repo.Path, "--format", "json");
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var report = WorkbenchCli.Run(repo.Path, "quality", "show", "--format", "json");
        Assert.AreEqual(0, report.ExitCode, $"stderr: {report.StdErr}\nstdout: {report.StdOut}");
        using var reportJson = JsonDocument.Parse(report.StdOut);
        var reportData = reportJson.RootElement.GetProperty("data");
        Assert.AreEqual("report", reportData.GetProperty("kind").GetString());
        Assert.AreEqual("fail", reportData.GetProperty("report").GetProperty("assessment").GetProperty("status").GetString());

        var inventory = WorkbenchCli.Run(repo.Path, "quality", "show", "--kind", "inventory", "--format", "json");
        Assert.AreEqual(0, inventory.ExitCode, $"stderr: {inventory.StdErr}\nstdout: {inventory.StdOut}");
        using var inventoryJson = JsonDocument.Parse(inventory.StdOut);
        var inventoryData = inventoryJson.RootElement.GetProperty("data");
        Assert.AreEqual("inventory", inventoryData.GetProperty("kind").GetString());
        Assert.AreEqual(2, inventoryData.GetProperty("inventory").GetProperty("tests").GetArrayLength());
    }

    [TestMethod]
    public void QualitySync_TableOutput_DryRun_PrintsSummaryWithoutWritingArtifacts()
    {
        using var repo = CreateFixtureRepo();

        var result = RunQualitySync(repo.Path, "--dry-run");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Inventory: 1 projects, 2 tests", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Results: failed (1 passed, 1 failed, 0 skipped)", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Coverage: line 75.0 %, branch 50.0 %", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "Dry run: no files were written.", StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing")));
    }

    [TestMethod]
    public void QualityShow_TableOutput_RendersReportInventoryResultsAndCoverageKinds()
    {
        using var repo = CreateFixtureRepo();

        var sync = RunQualitySync(repo.Path, "--format", "json");
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var report = WorkbenchCli.Run(repo.Path, "quality", "show");
        Assert.AreEqual(0, report.ExitCode, $"stderr: {report.StdErr}\nstdout: {report.StdOut}");
        StringAssert.Contains(report.StdOut, "Kind: report", StringComparison.Ordinal);
        StringAssert.Contains(report.StdOut, "Status: fail", StringComparison.Ordinal);
        StringAssert.Contains(report.StdOut, "Confidence: under-target", StringComparison.Ordinal);
        StringAssert.Contains(report.StdOut, "Observed tests: 2", StringComparison.Ordinal);
        StringAssert.Contains(report.StdOut, "Findings:", StringComparison.Ordinal);
        StringAssert.Contains(report.StdOut, "Markdown summary:", StringComparison.Ordinal);

        var inventory = WorkbenchCli.Run(repo.Path, "quality", "show", "--kind", "inventory");
        Assert.AreEqual(0, inventory.ExitCode, $"stderr: {inventory.StdErr}\nstdout: {inventory.StdOut}");
        StringAssert.Contains(inventory.StdOut, "Kind: inventory", StringComparison.Ordinal);
        StringAssert.Contains(inventory.StdOut, "Projects: 1", StringComparison.Ordinal);
        StringAssert.Contains(inventory.StdOut, "Tests: 2", StringComparison.Ordinal);
        StringAssert.Contains(inventory.StdOut, "Frameworks:", StringComparison.Ordinal);

        var results = WorkbenchCli.Run(repo.Path, "quality", "show", "--kind", "results");
        Assert.AreEqual(0, results.ExitCode, $"stderr: {results.StdErr}\nstdout: {results.StdOut}");
        StringAssert.Contains(results.StdOut, "Kind: results", StringComparison.Ordinal);
        StringAssert.Contains(results.StdOut, "Status: failed", StringComparison.Ordinal);
        StringAssert.Contains(results.StdOut, "Passed: 1", StringComparison.Ordinal);
        StringAssert.Contains(results.StdOut, "Failed: 1", StringComparison.Ordinal);
        StringAssert.Contains(results.StdOut, "Skipped: 0", StringComparison.Ordinal);

        var coverage = WorkbenchCli.Run(repo.Path, "quality", "show", "--kind", "coverage");
        Assert.AreEqual(0, coverage.ExitCode, $"stderr: {coverage.StdErr}\nstdout: {coverage.StdOut}");
        StringAssert.Contains(coverage.StdOut, "Kind: coverage", StringComparison.Ordinal);
        StringAssert.Contains(coverage.StdOut, "Line coverage: 75.0 %", StringComparison.Ordinal);
        StringAssert.Contains(coverage.StdOut, "Branch coverage: 50.0 %", StringComparison.Ordinal);
        StringAssert.Contains(coverage.StdOut, "Critical files:", StringComparison.Ordinal);
        StringAssert.Contains(coverage.StdOut, "- src/Sample/Widget.cs: pass", StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualityProofHealth_JsonOutput_ClassifiesRequirementProofHealth()
    {
        using var repo = CreateFixtureRepo();
        WriteProofHealthArtifacts(repo.Path);

        var result = WorkbenchCli.Run(repo.Path, "quality", "proof-health", "--format", "json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var data = json.RootElement.GetProperty("data");
        Assert.AreEqual(4, data.GetProperty("summary").GetProperty("requirements").GetInt32());
        var states = data.GetProperty("summary").GetProperty("states");
        Assert.AreEqual(1, states.GetProperty("trace_clean").GetInt32());
        Assert.AreEqual(1, states.GetProperty("partially_covered").GetInt32());
        Assert.AreEqual(1, states.GetProperty("uncovered_blocked").GetInt32());
        Assert.AreEqual(1, states.GetProperty("missing_coverage_contract").GetInt32());

        var requirements = data.GetProperty("requirements").EnumerateArray().ToDictionary(
            element => element.GetProperty("requirementId").GetString()!,
            element => element.GetProperty("state").GetString()!,
            StringComparer.Ordinal);
        Assert.AreEqual("trace_clean", requirements["REQ-SAMPLE-PROOF-0001"]);
        Assert.AreEqual("partially_covered", requirements["REQ-SAMPLE-PROOF-0002"]);
        Assert.AreEqual("uncovered_blocked", requirements["REQ-SAMPLE-PROOF-0003"]);
        Assert.AreEqual("missing_coverage_contract", requirements["REQ-SAMPLE-PROOF-0004"]);
        Assert.IsFalse(Directory.Exists(Path.Combine(repo.Path, "artifacts", "quality", "testing")));
    }

    [TestMethod]
    public void QualityProofHealth_DefaultRequired_EvaluatesMarkdownRequirementsWithExplicitPolicy()
    {
        using var repo = CreateFixtureRepo();
        WriteProofHealthMarkdownArtifacts(repo.Path);

        var result = WorkbenchCli.Run(
            repo.Path,
            "quality",
            "proof-health",
            "--scope",
            "REQ-SAMPLE-MD-0001",
            "--default-required",
            "positive",
            "negative",
            "--format",
            "json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var requirement = json.RootElement.GetProperty("data").GetProperty("requirements")[0];
        Assert.AreEqual("trace_clean", requirement.GetProperty("state").GetString());
        Assert.AreEqual(
            "default_policy",
            requirement.GetProperty("evidence").GetProperty("coverageContractSource").GetString());
        CollectionAssert.Contains(
            requirement.GetProperty("workQueueTags").EnumerateArray().Select(element => element.GetString()).ToList(),
            "author_coverage_contract");
    }

    [TestMethod]
    public void QualityRootCommand_PrintsGuidance()
    {
        using var repo = CreateFixtureRepo();

        var result = WorkbenchCli.Run(repo.Path, "quality");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Use `workbench quality sync`", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "`workbench quality proof-health`", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "`workbench quality attest`", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "`workbench quality show`", StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualityCommands_MalformedConfig_ReturnConfigError()
    {
        using var repo = CreateFixtureRepo();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
        File.WriteAllText(Path.Combine(repo.Path, ".workbench", "config.json"), "{");

        var sync = RunQualitySync(repo.Path);
        Assert.AreEqual(2, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");
        StringAssert.Contains(sync.StdOut, "Config error:", StringComparison.Ordinal);

        var show = WorkbenchCli.Run(repo.Path, "quality", "show");
        Assert.AreEqual(2, show.ExitCode, $"stderr: {show.StdErr}\nstdout: {show.StdOut}");
        StringAssert.Contains(show.StdOut, "Config error:", StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualityShow_GlobalOptionsWithEqualsSyntax_AreAcceptedOutsideRepo()
    {
        using var repo = CreateFixtureRepo();
        using var outside = TempRepo.Create();

        var sync = RunQualitySync(repo.Path, "--format", "json");
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var result = WorkbenchCli.Run(
            outside.Path,
            "quality",
            "show",
            $"--repo={repo.Path}",
            "--format=json");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var data = json.RootElement.GetProperty("data");
        Assert.AreEqual("report", data.GetProperty("kind").GetString());
    }

    [TestMethod]
    public void QualityShow_EnvironmentRepoAndFormat_AreRespected()
    {
        using var repo = CreateFixtureRepo();
        using var outside = TempRepo.Create();

        var sync = RunQualitySync(repo.Path, "--format", "json");
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var result = WorkbenchCli.Run(
            outside.Path,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["WORKBENCH_REPO"] = repo.Path,
                ["WORKBENCH_FORMAT"] = "json",
            },
            "quality",
            "show");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var data = json.RootElement.GetProperty("data");
        Assert.AreEqual("report", data.GetProperty("kind").GetString());
    }

    [TestMethod]
    public void QualityShow_MissingExplicitPath_ReturnsPathNotFoundErrorEnvelope()
    {
        using var repo = CreateFixtureRepo();

        var result = WorkbenchCli.Run(
            repo.Path,
            "quality",
            "show",
            "--path",
            "artifacts/quality/testing/missing-report.json",
            "--format",
            "json");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var error = json.RootElement.GetProperty("error");
        Assert.AreEqual("path_not_found", error.GetProperty("code").GetString());
        StringAssert.Contains(error.GetProperty("hint").GetString()!, "Verify the referenced file or directory exists.", StringComparison.Ordinal);
        Assert.IsFalse(result.StdErr.Contains("FileNotFoundException", StringComparison.Ordinal), result.StdErr);
    }

    [TestMethod]
    public void QualityShow_EnvironmentDebug_PrintsExceptionDetailsForJsonErrors()
    {
        using var repo = CreateFixtureRepo();

        var result = WorkbenchCli.Run(
            repo.Path,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["WORKBENCH_DEBUG"] = "on",
                ["WORKBENCH_FORMAT"] = "json",
            },
            "quality",
            "show",
            "--path",
            "artifacts/quality/testing/missing-report.json");

        Assert.AreEqual(2, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        using var json = JsonDocument.Parse(result.StdOut);
        var error = json.RootElement.GetProperty("error");
        Assert.AreEqual("path_not_found", error.GetProperty("code").GetString());
        StringAssert.Contains(result.StdErr, "FileNotFoundException", StringComparison.Ordinal);
    }

    [TestMethod]
    public void QualitySync_RespectsAuthoredSolutionPath_AndCsprojGlobIncludes()
    {
        using var repo = CreateFixtureRepo(
            """
            version: 2
            domain: testing

            scope:
              solutionPath: src/Sample.All.slnx
              includes:
                - tests/**/*.csproj

            expectations:
              evidence:
                - inventory
                - results
                - coverage
            """,
            solutionRelativePath: "src/Sample.All.slnx");

        var sync = RunQualitySync(repo.Path, "--format", "json");
        Assert.AreEqual(0, sync.ExitCode, $"stderr: {sync.StdErr}\nstdout: {sync.StdOut}");

        var inventory = WorkbenchCli.Run(repo.Path, "quality", "show", "--kind", "inventory", "--format", "json");
        Assert.AreEqual(0, inventory.ExitCode, $"stderr: {inventory.StdErr}\nstdout: {inventory.StdOut}");
        using var inventoryJson = JsonDocument.Parse(inventory.StdOut);
        var data = inventoryJson.RootElement.GetProperty("data").GetProperty("inventory");
        Assert.AreEqual("src/Sample.All.slnx", data.GetProperty("scope").GetProperty("solutionPath").GetString());
        Assert.AreEqual(1, data.GetProperty("projects").GetArrayLength());
        Assert.AreEqual(2, data.GetProperty("tests").GetArrayLength());
        Assert.AreEqual("tests/Sample.Tests/Sample.Tests.csproj", data.GetProperty("projects")[0].GetProperty("projectPath").GetString());
    }

    private static CommandResult RunQualitySync(string repoPath, params string[] extraArgs)
    {
        var args = new List<string>
        {
            "quality",
            "sync",
            "--results",
            "artifacts/raw/test-results",
            "--coverage",
            "artifacts/raw/coverage"
        };
        args.AddRange(extraArgs);
        return WorkbenchCli.Run(repoPath, args.ToArray());
    }

    private static TempRepo CreateFixtureRepo(string? contractContent = null, string? solutionRelativePath = null)
    {
        var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);

        Directory.CreateDirectory(Path.Combine(repo.Path, "schemas"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "quality"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "test-results"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "artifacts", "raw", "coverage"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "src", "Sample"));
        Directory.CreateDirectory(Path.Combine(repo.Path, "tests", "Sample.Tests"));

        var sourceRepoRoot = FindSourceRepoRoot();
        foreach (var schema in new[]
        {
            "test-inventory.schema.json",
            "test-run-summary.schema.json",
            "coverage-summary.schema.json",
            "quality-report.schema.json"
        })
        {
            File.Copy(
                Path.Combine(sourceRepoRoot, "schemas", schema),
                Path.Combine(repo.Path, "schemas", schema));
        }

        File.WriteAllText(Path.Combine(repo.Path, "quality", "testing-intent.yaml"), contractContent ?? """
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
              confidenceTarget: medium

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

        if (!string.IsNullOrWhiteSpace(solutionRelativePath))
        {
            var solutionPath = Path.Combine(repo.Path, solutionRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath) ?? repo.Path);
            File.WriteAllText(solutionPath, "<Solution />\n");
        }

        File.WriteAllText(Path.Combine(repo.Path, "src", "Sample", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo.Path, "src", "Sample", "Widget.cs"), """
            namespace Sample;

            public class Widget
            {
                public int Add(int left, int right)
                {
                    if (right == 0)
                    {
                        return left;
                    }

                    return left + right;
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo.Path, "tests", "Sample.Tests", "Sample.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsTestProject>true</IsTestProject>
                <AssemblyName>Sample.Tests</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
                <PackageReference Include="xunit" Version="2.9.2" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs"), """
            using Xunit;

            namespace Sample.Tests;

            public class WidgetTests
            {
                [Fact]
                public void Adds_numbers()
                {
                }

                [Theory]
                public void Handles_zero()
                {
                }
            }
            """);

        WriteSampleResultsArtifact(repo.Path);
        WriteSampleCoverageArtifact(repo.Path);

        return repo;
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

    private static void WriteSampleResultsArtifact(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "artifacts", "raw", "test-results", "sample-results.trx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun id="6c574911-54e4-4f7a-bc3a-07dc30e803c7" name="quality-sample" runUser="workbench" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Times creation="2026-03-07T16:00:00.0000000+00:00" start="2026-03-07T16:00:01.0000000+00:00" finish="2026-03-07T16:00:03.0000000+00:00" />
              <TestDefinitions>
                <UnitTest name="Sample.Tests.WidgetTests.Adds_numbers" storage="Sample.Tests.dll" id="11111111-1111-1111-1111-111111111111">
                  <Execution id="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" />
                  <TestMethod codeBase="C:\agent\bin\Debug\net10.0\Sample.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" className="Sample.Tests.WidgetTests" name="Adds_numbers" />
                </UnitTest>
                <UnitTest name="Sample.Tests.WidgetTests.Handles_zero" storage="Sample.Tests.dll" id="22222222-2222-2222-2222-222222222222">
                  <Execution id="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" />
                  <TestMethod codeBase="C:\agent\bin\Debug\net10.0\Sample.Tests.dll" adapterTypeName="executor://xunit/VsTestRunner2/netcoreapp" className="Sample.Tests.WidgetTests" name="Handles_zero" />
                </UnitTest>
              </TestDefinitions>
              <Results>
                <UnitTestResult executionId="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" testId="11111111-1111-1111-1111-111111111111" testName="Sample.Tests.WidgetTests.Adds_numbers" outcome="Passed" duration="00:00:00.0100000" />
                <UnitTestResult executionId="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" testId="22222222-2222-2222-2222-222222222222" testName="Sample.Tests.WidgetTests.Handles_zero" outcome="Failed" duration="00:00:00.0250000">
                  <Output>
                    <ErrorInfo>
                      <Message>Expected zero to be handled.</Message>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
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
                    <class name="Sample.Widget" filename="src/Sample/Widget.cs" line-rate="0.75" branch-rate="0.5">
                      <lines>
                        <line number="1" hits="1" branch="false" />
                        <line number="2" hits="1" branch="true" condition-coverage="50% (1/2)" />
                        <line number="3" hits="1" branch="false" />
                        <line number="4" hits="0" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
    }

    private static void WriteRequirementCommentJsonSpec(string repoRoot)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE"));
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-0001.json"), """
            {
              "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
              "artifact_id": "SPEC-SAMPLE-0001",
              "artifact_type": "specification",
              "title": "Sample requirement comments",
              "domain": "SAMPLE",
              "capability": "quality",
              "status": "draft",
              "owner": "platform",
              "purpose": "Exercise JSON requirement catalog loading for generated comments.",
              "scope": "Keep the sample requirement comments synchronized from canonical JSON.",
              "context": "The sync path should read requirement statements from JSON specification files.",
              "requirements": [
                {
                  "id": "REQ-SAMPLE-0004",
                  "title": "Widget class requirement",
                  "statement": "The widget test class MUST be documented with generated requirement comments."
                },
                {
                  "id": "REQ-SAMPLE-0001",
                  "title": "Adds numbers requirement",
                  "statement": "The system MUST verify addition behavior.",
                  "trace": {
                    "x_test_refs": [
                      "tests/Sample.Tests/WidgetTests.cs::Adds_numbers"
                    ]
                  }
                },
                {
                  "id": "REQ-SAMPLE-0002",
                  "title": "Handles zero requirement",
                  "statement": "The system MUST document zero handling."
                },
                {
                  "id": "REQ-SAMPLE-0003",
                  "title": "Handles zero fallback requirement",
                  "statement": "The system MUST document the fallback path."
                }
              ]
            }
            """);
    }

    private static void WriteRequirementCommentSource(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "WidgetTests.cs"), """
            using Xunit;

            namespace Sample.Tests;

            [Requirement("REQ-SAMPLE-0004")]
            public class WidgetTests
            {
                [Fact]
                [Trait("Category", "Positive")]
                [Requirement("REQ-SAMPLE-0001")]
                public void Adds_numbers()
                {
                }

                [Requirement("REQ-SAMPLE-0002")]
                [Requirement("REQ-SAMPLE-0003")]
                [Theory]
                public void Handles_zero()
                {
                }
            }
            """);
    }

    private static void WriteRequirementCommentEmptySource(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "WidgetTests.cs"), """
            using Xunit;

            namespace Sample.Tests;

            [Requirement("REQ-SAMPLE-0004")]
            public class WidgetTests
            {
            }
            """);
    }

    private static void WriteProofHealthArtifacts(string repoRoot)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "tests", "Sample.Tests", "RequirementHomes", "SAMPLE"));
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-PROOF.json"), """
            {
              "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
              "artifact_id": "SPEC-SAMPLE-PROOF",
              "artifact_type": "specification",
              "title": "Sample proof health",
              "domain": "SAMPLE",
              "capability": "quality",
              "status": "draft",
              "owner": "platform",
              "purpose": "Exercise per-requirement proof health classification.",
              "scope": "Keep the fixture small and deterministic.",
              "context": "Proof health consumes coverage contracts and discovered requirement test traits.",
              "requirements": [
                {
                  "id": "REQ-SAMPLE-PROOF-0001",
                  "title": "Clean proof",
                  "statement": "The system MUST report a clean proof when focused positive and negative tests are linked.",
                  "coverage": {
                    "positive": "required",
                    "negative": "required",
                    "edge": "optional",
                    "fuzz": "not_applicable"
                  },
                  "trace": {
                    "x_test_refs": [
                      "tests/Sample.Tests/RequirementHomes/SAMPLE/REQ-SAMPLE-PROOF-0001.cs::Positive_path_accepts_widget",
                      "tests/Sample.Tests/RequirementHomes/SAMPLE/REQ-SAMPLE-PROOF-0001.cs::Negative_path_rejects_widget"
                    ]
                  }
                },
                {
                  "id": "REQ-SAMPLE-PROOF-0002",
                  "title": "Partial proof",
                  "statement": "The system MUST report partial proof when required evidence kinds are missing.",
                  "coverage": {
                    "positive": "required",
                    "negative": "required",
                    "edge": "optional",
                    "fuzz": "not_applicable"
                  },
                  "trace": {
                    "x_test_refs": [
                      "tests/Sample.Tests/RequirementHomes/SAMPLE/REQ-SAMPLE-PROOF-0002.cs::Positive_path_handles_partial_widget"
                    ]
                  }
                },
                {
                  "id": "REQ-SAMPLE-PROOF-0003",
                  "title": "Blocked proof",
                  "statement": "The system MUST report blocked uncovered proof when the gap ledger references the requirement.",
                  "coverage": {
                    "positive": "required",
                    "negative": "optional",
                    "edge": "optional",
                    "fuzz": "not_applicable"
                  }
                },
                {
                  "id": "REQ-SAMPLE-PROOF-0004",
                  "title": "Missing coverage contract",
                  "statement": "The system MUST report missing coverage contract when no authored coverage metadata exists."
                }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "REQUIREMENT-GAPS.md"), """
            # Requirement Gaps

            - REQ-SAMPLE-PROOF-0003 is intentionally blocked in this fixture.
            """);
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "RequirementHomes", "SAMPLE", "REQ-SAMPLE-PROOF-0001.cs"), """
            using Xunit;

            namespace Sample.Tests.RequirementHomes;

            public class REQ_SAMPLE_PROOF_0001
            {
                [Fact]
                [Requirement("REQ-SAMPLE-PROOF-0001")]
                [Trait("Category", "Positive")]
                public void Positive_path_accepts_widget()
                {
                }

                [Fact]
                [Requirement("REQ-SAMPLE-PROOF-0001")]
                [Trait("Category", "Negative")]
                public void Negative_path_rejects_widget()
                {
                }
            }
            """);
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "RequirementHomes", "SAMPLE", "REQ-SAMPLE-PROOF-0002.cs"), """
            using Xunit;

            namespace Sample.Tests.RequirementHomes;

            public class REQ_SAMPLE_PROOF_0002
            {
                [Fact]
                [Requirement("REQ-SAMPLE-PROOF-0002")]
                [Trait("Category", "Positive")]
                public void Positive_path_handles_partial_widget()
                {
                }
            }
            """);
    }

    private static void WriteProofHealthMarkdownArtifacts(string repoRoot)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "tests", "Sample.Tests", "RequirementHomes", "SAMPLE"));
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-MD.md"), """
            ---
            artifact_id: SPEC-SAMPLE-MD
            artifact_type: specification
            title: Sample markdown proof health
            domain: SAMPLE
            capability: quality
            status: draft
            owner: platform
            ---

            # SPEC-SAMPLE-MD - Sample markdown proof health

            ## Purpose

            Exercise proof-health default policy evaluation for Markdown requirements.

            ## Scope

            - Markdown requirement clauses

            ## Context

            Markdown requirements cannot carry JSON coverage metadata directly.

            ## REQ-SAMPLE-MD-0001 Markdown proof health
            The system MUST evaluate a Markdown requirement against an explicit default policy.

            Trace:
            - Test Refs:
              - tests/Sample.Tests/RequirementHomes/SAMPLE/REQ-SAMPLE-MD-0001.cs::Positive_markdown_requirement
              - tests/Sample.Tests/RequirementHomes/SAMPLE/REQ-SAMPLE-MD-0001.cs::Negative_markdown_requirement
            """);
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "RequirementHomes", "SAMPLE", "REQ-SAMPLE-MD-0001.cs"), """
            using Xunit;

            namespace Sample.Tests.RequirementHomes;

            public class REQ_SAMPLE_MD_0001
            {
                [Fact]
                [Requirement("REQ-SAMPLE-MD-0001")]
                [Trait("Category", "Positive")]
                public void Positive_markdown_requirement()
                {
                }

                [Fact]
                [Requirement("REQ-SAMPLE-MD-0001")]
                [Trait("Category", "Negative")]
                public void Negative_markdown_requirement()
                {
                }
            }
            """);
    }
}
