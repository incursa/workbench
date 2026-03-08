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
    public void QualityRootCommand_PrintsGuidance()
    {
        using var repo = CreateFixtureRepo();

        var result = WorkbenchCli.Run(repo.Path, "quality");

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Use `workbench quality sync`", StringComparison.Ordinal);
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

        Directory.CreateDirectory(Path.Combine(repo.Path, "docs", "30-contracts"));
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
                Path.Combine(sourceRepoRoot, "docs", "30-contracts", schema),
                Path.Combine(repo.Path, "docs", "30-contracts", schema));
        }

        File.WriteAllText(Path.Combine(repo.Path, "docs", "30-contracts", "test-gate.contract.yaml"), contractContent ?? """
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
}
