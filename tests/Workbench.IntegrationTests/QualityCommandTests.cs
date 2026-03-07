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

        var sync = WorkbenchCli.Run(
            repo.Path,
            "quality",
            "sync",
            "--results",
            "artifacts/raw/test-results",
            "--coverage",
            "artifacts/raw/coverage",
            "--format",
            "json");
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

    private static TempRepo CreateFixtureRepo()
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

        File.WriteAllText(Path.Combine(repo.Path, "docs", "30-contracts", "test-gate.contract.yaml"), """
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

        File.Copy(
            Path.Combine(sourceRepoRoot, "testdata", "quality", "sample-results.trx"),
            Path.Combine(repo.Path, "artifacts", "raw", "test-results", "sample-results.trx"));
        File.Copy(
            Path.Combine(sourceRepoRoot, "testdata", "quality", "sample-coverage.cobertura.xml"),
            Path.Combine(repo.Path, "artifacts", "raw", "coverage", "sample-coverage.cobertura.xml"));

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
}
