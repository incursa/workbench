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

    private static TempQualityRepo CreateFixtureRepo()
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

        File.WriteAllText(Path.Combine(repoRoot, "docs", "30-contracts", "test-gate.contract.yaml"), """
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

        File.Copy(
            Path.Combine(sourceRepoRoot, "testdata", "quality", "sample-results.trx"),
            Path.Combine(repoRoot, "artifacts", "raw", "test-results", "sample-results.trx"));
        File.Copy(
            Path.Combine(sourceRepoRoot, "testdata", "quality", "sample-coverage.cobertura.xml"),
            Path.Combine(repoRoot, "artifacts", "raw", "coverage", "sample-coverage.cobertura.xml"));

        return new TempQualityRepo(repoRoot);
    }

    private static string FindSourceRepoRoot()
    {
        var start = AppContext.BaseDirectory;
        var repoRoot = Repository.FindRepoRoot(start);
        return repoRoot ?? throw new DirectoryNotFoundException("Could not locate the Workbench repo root.");
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
