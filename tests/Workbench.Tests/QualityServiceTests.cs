using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class QualityServiceTests
{
    [TestMethod]
    public void Sync_GeneratesSchemaAlignedArtifactsAndMarkdownSummary()
    {
        using var repo = CreateFixtureRepo();

        var result = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                false));

        Assert.HasCount(1, result.Inventory.Projects);
        Assert.HasCount(2, result.Inventory.Tests);
        Assert.AreEqual("failed", result.Results.Summary.Status);
        Assert.AreEqual(1, result.Results.Summary.Passed);
        Assert.AreEqual(1, result.Results.Summary.Failed);
        Assert.AreEqual(0.75, result.Coverage.Summary.LineRate, 0.0001);
        Assert.AreEqual(0.5, result.Coverage.Summary.BranchRate, 0.0001);
        Assert.AreEqual("fail", result.Report.Assessment.Status);

        AssertSchema(repo.Path, "docs/30-contracts/test-inventory.schema.json", "artifacts/quality/testing/test-inventory.json");
        AssertSchema(repo.Path, "docs/30-contracts/test-run-summary.schema.json", "artifacts/quality/testing/test-run-summary.json");
        AssertSchema(repo.Path, "docs/30-contracts/coverage-summary.schema.json", "artifacts/quality/testing/coverage-summary.json");
        AssertSchema(repo.Path, "docs/30-contracts/quality-report.schema.json", "artifacts/quality/testing/quality-report.json");

        var summaryPath = Path.Combine(repo.Path, "artifacts", "quality", "testing", "quality-summary.md");
        Assert.IsTrue(File.Exists(summaryPath));
        var summary = File.ReadAllText(summaryPath);
        StringAssert.Contains(summary, "## Authored Truth", StringComparison.Ordinal);
        StringAssert.Contains(summary, "## Observed Truth", StringComparison.Ordinal);
        StringAssert.Contains(summary, "## Findings", StringComparison.Ordinal);
    }

    [TestMethod]
    public void IngestTestRunSummary_ParsesTrxIntoNormalizedResults()
    {
        using var repo = CreateFixtureRepo();
        var authored = QualityService.LoadAuthoredIntent(repo.Path, Path.Combine(repo.Path, QualityService.DefaultContractPath));
        var inventory = QualityService.DiscoverTestInventory(repo.Path, authored, "workbench quality sync");

        var summary = QualityService.IngestTestRunSummary(
            repo.Path,
            "artifacts/raw/test-results",
            inventory.Projects,
            inventory.Tests,
            "workbench quality sync");

        Assert.HasCount(2, summary.Tests);
        Assert.AreEqual("failed", summary.Summary.Status);
        Assert.AreEqual(1, summary.Summary.Passed);
        Assert.AreEqual(1, summary.Summary.Failed);
        StringAssert.Contains(
            summary.Tests.Single(test => string.Equals(test.Outcome, "failed", StringComparison.Ordinal)).ErrorMessage!,
            "Expected zero",
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void IngestCoverageSummary_ParsesCoberturaAndCriticalFiles()
    {
        using var repo = CreateFixtureRepo();
        var authored = QualityService.LoadAuthoredIntent(repo.Path, Path.Combine(repo.Path, QualityService.DefaultContractPath));

        var coverage = QualityService.IngestCoverageSummary(
            repo.Path,
            "artifacts/raw/coverage",
            authored,
            Array.Empty<TestInventoryProject>(),
            "workbench quality sync");

        Assert.HasCount(1, coverage.Files);
        Assert.AreEqual("src/Sample/Widget.cs", coverage.Files[0].RepoPath);
        Assert.AreEqual(0.75, coverage.Summary.LineRate, 0.0001);
        Assert.AreEqual(0.5, coverage.Summary.BranchRate, 0.0001);
        Assert.HasCount(1, coverage.CriticalFiles);
        Assert.AreEqual("pass", coverage.CriticalFiles[0].Status);
    }

    [TestMethod]
    public void IngestCoverageSummary_AggregatesCoverageAcrossArtifactsForSameFile()
    {
        using var repo = CreateFixtureRepo();
        var authored = QualityService.LoadAuthoredIntent(repo.Path, Path.Combine(repo.Path, QualityService.DefaultContractPath));
        WriteSecondaryCoverageArtifact(repo.Path);

        var coverage = QualityService.IngestCoverageSummary(
            repo.Path,
            "artifacts/raw/coverage",
            authored,
            Array.Empty<TestInventoryProject>(),
            "workbench quality sync");

        Assert.HasCount(1, coverage.Files);
        Assert.AreEqual("src/Sample/Widget.cs", coverage.Files[0].RepoPath);
        Assert.AreEqual(3, coverage.Files[0].LinesCovered);
        Assert.AreEqual(8, coverage.Files[0].LinesValid);
        Assert.AreEqual(0.375, coverage.Files[0].LineRate, 0.0001);
        Assert.AreEqual(1, coverage.Files[0].BranchesCovered);
        Assert.AreEqual(4, coverage.Files[0].BranchesValid);
        Assert.AreEqual(0.25, coverage.Files[0].BranchRate, 0.0001);
        Assert.AreEqual("fail", coverage.CriticalFiles[0].Status);
    }

    [TestMethod]
    public void DiscoverTestInventory_UsesAuthoredSolutionPath_AndCsprojGlobScope()
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
            """,
            solutionRelativePath: "src/Sample.All.slnx");

        var authored = QualityService.LoadAuthoredIntent(repo.Path, Path.Combine(repo.Path, QualityService.DefaultContractPath));
        var inventory = QualityService.DiscoverTestInventory(repo.Path, authored, "workbench quality sync");

        Assert.AreEqual("src/Sample.All.slnx", authored.SolutionPath);
        Assert.AreEqual("src/Sample.All.slnx", inventory.Scope.SolutionPath);
        Assert.HasCount(1, inventory.Projects);
        Assert.HasCount(2, inventory.Tests);
        Assert.AreEqual("tests/Sample.Tests/Sample.Tests.csproj", inventory.Projects[0].ProjectPath);
    }

    private static void AssertSchema(string repoRoot, string schemaPath, string artifactPath)
    {
        var fullPath = Path.Combine(repoRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar));
        var errors = SchemaValidationService.ValidateJsonContent(
            repoRoot,
            schemaPath,
            artifactPath,
            File.ReadAllText(fullPath));
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    private static TempQualityRepo CreateFixtureRepo(string? contractContent = null, string? solutionRelativePath = null)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "artifacts", "raw", "test-results"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "artifacts", "raw", "coverage"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "src", "Sample"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "tests", "Sample.Tests"));

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
                Path.Combine(repoRoot, "docs", "30-contracts", schema));
        }

        File.WriteAllText(Path.Combine(repoRoot, "docs", "30-contracts", "test-gate.contract.yaml"), contractContent ?? """
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

            related:
              docs:
                - /docs/10-product/sample.md
              workItems:
                - TASK-0099
              codeRefs:
                - src/Sample/Widget.cs
            """);

        if (!string.IsNullOrWhiteSpace(solutionRelativePath))
        {
            var solutionPath = Path.Combine(repoRoot, solutionRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(solutionPath) ?? repoRoot);
            File.WriteAllText(solutionPath, "<Solution />\n");
        }

        File.WriteAllText(Path.Combine(repoRoot, "src", "Sample", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Sample</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repoRoot, "src", "Sample", "Widget.cs"), """
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

        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "Sample.Tests.csproj"), """
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
        File.WriteAllText(Path.Combine(repoRoot, "tests", "Sample.Tests", "WidgetTests.cs"), """
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

        WriteSampleResultsArtifact(repoRoot);
        WriteSampleCoverageArtifact(repoRoot);

        return new TempQualityRepo(repoRoot);
    }

    private static string FindSourceRepoRoot()
    {
        var start = AppContext.BaseDirectory;
        var repoRoot = Repository.FindRepoRoot(start);
        return repoRoot ?? throw new DirectoryNotFoundException("Could not locate the Workbench repo root.");
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

    private static void WriteSecondaryCoverageArtifact(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "artifacts", "raw", "coverage", "sample-coverage-secondary.cobertura.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" branch-rate="0" lines-covered="0" lines-valid="4" branches-covered="0" branches-valid="2" version="1.9" timestamp="1772899201">
              <packages>
                <package name="Sample" line-rate="0" branch-rate="0">
                  <classes>
                    <class name="Sample.Widget" filename="src/Sample/Widget.cs" line-rate="0" branch-rate="0">
                      <lines>
                        <line number="1" hits="0" branch="false" />
                        <line number="2" hits="0" branch="true" condition-coverage="0% (0/2)" />
                        <line number="3" hits="0" branch="false" />
                        <line number="4" hits="0" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
    }

    private sealed class TempQualityRepo : IDisposable
    {
        public TempQualityRepo(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
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
