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
        var addsNumbers = result.Inventory.Tests.Single(test => string.Equals(test.DisplayName, "Adds_numbers", StringComparison.Ordinal));
        CollectionAssert.AreEquivalent(new[] { "REQ-SAMPLE-0001" }, addsNumbers.Traits["Requirement"]);
        CollectionAssert.AreEquivalent(new[] { "Positive" }, addsNumbers.Traits["Category"]);
        CollectionAssert.AreEquivalent(new[] { "Fact" }, addsNumbers.Traits["framework"]);
        var handlesZero = result.Inventory.Tests.Single(test => string.Equals(test.DisplayName, "Handles_zero", StringComparison.Ordinal));
        CollectionAssert.AreEquivalent(new[] { "REQ-SAMPLE-0002" }, handlesZero.Traits["Requirement"]);
        CollectionAssert.AreEquivalent(new[] { "Negative" }, handlesZero.Traits["Category"]);
        CollectionAssert.AreEquivalent(new[] { "Theory" }, handlesZero.Traits["framework"]);
        Assert.AreEqual(0.75, result.Coverage.Summary.LineRate, 0.0001);
        Assert.AreEqual(0.5, result.Coverage.Summary.BranchRate, 0.0001);
        Assert.AreEqual("fail", result.Report.Assessment.Status);

        AssertSchema(repo.Path, "schemas/test-inventory.schema.json", "artifacts/quality/testing/test-inventory.json");
        AssertSchema(repo.Path, "schemas/test-run-summary.schema.json", "artifacts/quality/testing/test-run-summary.json");
        AssertSchema(repo.Path, "schemas/coverage-summary.schema.json", "artifacts/quality/testing/coverage-summary.json");
        AssertSchema(repo.Path, "schemas/quality-report.schema.json", "artifacts/quality/testing/quality-report.json");

        var summaryPath = Path.Combine(repo.Path, "artifacts", "quality", "testing", "quality-summary.md");
        Assert.IsTrue(File.Exists(summaryPath));
        var summary = File.ReadAllText(summaryPath);
        StringAssert.Contains(summary, "## Authored Truth", StringComparison.Ordinal);
        StringAssert.Contains(summary, "## Observed Truth", StringComparison.Ordinal);
        StringAssert.Contains(summary, "## Findings", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Sync_BackfillsCanonicalSpecTraceArtifacts()
    {
        using var repo = CreateFixtureRepo(includeRequirementSpec: true);
        var specPath = Path.Combine(repo.Path, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-0001.md");
        var original = File.ReadAllText(specPath);

        var result = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                false));

        Assert.AreEqual("fail", result.Report.Assessment.Status);
        var after = File.ReadAllText(specPath);
        Assert.AreNotEqual(original, after);
        StringAssert.Contains(after, "Test Refs:", StringComparison.Ordinal);
        StringAssert.Contains(after, "tests/Sample.Tests/WidgetTests.cs::Adds_numbers", StringComparison.Ordinal);
        Assert.IsNotNull(result.Data.TraceSync);
        Assert.AreEqual(1, result.Data.TraceSync!.Specifications.FilesUpdated);
        Assert.AreEqual(1, result.Data.TraceSync.Specifications.RequirementsUpdated);
        Assert.IsNull(result.Data.TraceSync.TestRequirementComments);
    }

    [TestMethod]
    public void Sync_SyncRequirementComments_InsertsGeneratedRequirementBlocks()
    {
        using var repo = CreateFixtureRepo(includeRequirementSpec: true);
        WriteRequirementCommentSpec(repo.Path);
        WriteRequirementCommentSource(repo.Path);

        var result = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                false,
                true));

        Assert.IsNotNull(result.Data.TraceSync);
        Assert.AreEqual(1, result.Data.TraceSync!.Specifications.FilesUpdated);
        Assert.AreEqual(4, result.Data.TraceSync.Specifications.RequirementsUpdated);
        Assert.IsNotNull(result.Data.TraceSync.TestRequirementComments);
        Assert.AreEqual(1, result.Data.TraceSync.TestRequirementComments!.FilesUpdated);
        Assert.AreEqual(4, result.Data.TraceSync.TestRequirementComments.RequirementsUpdated);

        var testPath = Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs");
        var updatedTest = File.ReadAllText(testPath);
        const string BlockMarker = "<workbench-requirements generated=\"true\" source=\"workbench quality sync\">";
        Assert.AreEqual(3, updatedTest.Split(new[] { BlockMarker }, StringSplitOptions.None).Length - 1);

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

        Assert.IsLessThan(
            updatedTest.IndexOf("[Requirement(\"REQ-SAMPLE-0004\")]", StringComparison.Ordinal),
            updatedTest.IndexOf("requirementId=\"REQ-SAMPLE-0004\"", StringComparison.Ordinal));
        Assert.IsLessThan(
            updatedTest.IndexOf("requirementId=\"REQ-SAMPLE-0001\"", StringComparison.Ordinal),
            updatedTest.IndexOf("[Trait(\"Category\", \"Positive\")]", StringComparison.Ordinal));
        Assert.IsLessThan(
            updatedTest.IndexOf("[Requirement(\"REQ-SAMPLE-0001\")]", StringComparison.Ordinal),
            updatedTest.IndexOf("requirementId=\"REQ-SAMPLE-0001\"", StringComparison.Ordinal));
        Assert.IsLessThan(
            updatedTest.IndexOf("[Requirement(\"REQ-SAMPLE-0002\")]", StringComparison.Ordinal),
            updatedTest.IndexOf("requirementId=\"REQ-SAMPLE-0002\"", StringComparison.Ordinal));
        Assert.IsLessThan(
            updatedTest.IndexOf("[Requirement(\"REQ-SAMPLE-0003\")]", StringComparison.Ordinal),
            updatedTest.IndexOf("requirementId=\"REQ-SAMPLE-0003\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Sync_SyncRequirementComments_InsertsGeneratedRequirementBlocks_FromJsonSpec()
    {
        using var repo = CreateFixtureRepo();
        WriteRequirementCommentJsonSpec(repo.Path);
        WriteRequirementCommentSource(repo.Path);

        var result = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                false,
                true));

        Assert.IsNotNull(result.Data.TraceSync);
        Assert.AreEqual(0, result.Data.TraceSync!.Specifications.FilesUpdated);
        Assert.IsNotNull(result.Data.TraceSync.TestRequirementComments);
        Assert.AreEqual(1, result.Data.TraceSync.TestRequirementComments!.FilesUpdated);
        Assert.AreEqual(4, result.Data.TraceSync.TestRequirementComments.RequirementsUpdated);

        var testPath = Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs");
        var updatedTest = File.ReadAllText(testPath);
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
    public void Sync_SyncRequirementComments_InsertsGeneratedRequirementBlocks_ForEmptyTestFile()
    {
        using var repo = CreateFixtureRepo();
        WriteRequirementCommentJsonSpec(repo.Path);
        WriteRequirementCommentEmptySource(repo.Path);

        var result = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                false,
                true));

        Assert.IsNotNull(result.Data.TraceSync);
        Assert.IsNotNull(result.Data.TraceSync.TestRequirementComments);
        Assert.AreEqual(1, result.Data.TraceSync.TestRequirementComments!.FilesUpdated);
        Assert.AreEqual(1, result.Data.TraceSync.TestRequirementComments.RequirementsUpdated);

        var testPath = Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs");
        var updatedTest = File.ReadAllText(testPath);
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
    public void Sync_DryRunDoesNotMutateCanonicalSpecTraceArtifacts()
    {
        using var repo = CreateFixtureRepo(includeRequirementSpec: true);
        var specPath = Path.Combine(repo.Path, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-0001.md");
        var testPath = Path.Combine(repo.Path, "tests", "Sample.Tests", "WidgetTests.cs");
        var original = File.ReadAllText(specPath);
        var originalTest = File.ReadAllText(testPath);

        _ = QualityService.Sync(
            repo.Path,
            new QualitySyncOptions(
                null,
                "artifacts/raw/test-results",
                "artifacts/raw/coverage",
                null,
                true,
                true));

        var after = File.ReadAllText(specPath);
        Assert.AreEqual(original, after);
        var afterTest = File.ReadAllText(testPath);
        Assert.AreEqual(originalTest, afterTest);
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

    private static TempQualityRepo CreateFixtureRepo(string? contractContent = null, string? solutionRelativePath = null, bool includeRequirementSpec = false)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "schemas"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "contracts"));
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
                Path.Combine(sourceRepoRoot, "schemas", schema),
                Path.Combine(repoRoot, "schemas", schema));
        }

        Directory.CreateDirectory(Path.Combine(repoRoot, "quality"));
        File.WriteAllText(Path.Combine(repoRoot, "quality", "testing-intent.yaml"), contractContent ?? """
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

            intentionalGaps:
              - subject: src/Sample/Generated
                rationale: Generated sample coverage should be excluded from the authored quality bar.

            scenarios:
              requiredTests:
                - tests/Sample.Tests/WidgetTests.cs::Adds_numbers
                - tests/Sample.Tests/WidgetTests.cs::Handles_zero

            related:
              docs:
                - /overview/sample.md
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
                [Requirement("REQ-SAMPLE-0001")]
                [Trait("Category", "Positive")]
                public void Adds_numbers()
                {
                }

                [Theory]
                [Requirement("REQ-SAMPLE-0002")]
                [Trait("Category", "Negative")]
                public void Handles_zero()
                {
                }
            }
            """);

        if (includeRequirementSpec)
        {
            Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE"));
            File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-0001.md"), """
                ---
                artifact_id: SPEC-SAMPLE-0001
                artifact_type: specification
                title: Sample test linkage
                domain: SAMPLE
                capability: quality
                status: draft
                owner: platform
                ---

                # SPEC-SAMPLE-0001 - Sample test linkage

                ## Purpose

                Keep sample test refs synchronized back into canonical requirement trace blocks.

                ## Scope

                - Discover test refs from requirement metadata
                - Backfill matching requirement trace blocks

                ## Context

                Sample repository fixture for quality sync backfill coverage.

                ## REQ-SAMPLE-0001 Sample test linkage
                The system MUST keep the sample test linked to its requirement.

                Trace:
                - Implemented By:
                  - WI-SAMPLE-0001

                Notes:
                - Test refs should be backfilled by quality sync.

                ## Open Questions

                - None
                """);
        }

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
                    <class name="Sample.Generated.Widget" filename="src/Sample/Generated/Extra.cs" line-rate="1" branch-rate="1">
                      <lines>
                        <line number="1" hits="1" branch="false" />
                        <line number="2" hits="1" branch="true" condition-coverage="100% (2/2)" />
                      </lines>
                    </class>
                    <class name="Sample.Widget.Generated" filename="src/Sample/obj/Generated.g.cs" line-rate="1" branch-rate="1">
                      <lines>
                        <line number="1" hits="1" branch="false" />
                      </lines>
                    </class>
                    <class name="Elsewhere.Widget" filename="src/Elsewhere/Outside.cs" line-rate="1" branch-rate="1">
                      <lines>
                        <line number="1" hits="1" branch="false" />
                      </lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);
    }

    private static void WriteRequirementCommentSpec(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, "specs", "requirements", "SAMPLE", "SPEC-SAMPLE-0001.md"), """
            ---
            artifact_id: SPEC-SAMPLE-0001
            artifact_type: specification
            title: Sample requirement comments
            domain: SAMPLE
            capability: quality
            status: draft
            owner: platform
            ---

            # SPEC-SAMPLE-0001 - Sample requirement comments

            ## REQ-SAMPLE-0004 Widget class requirement
            The widget test class MUST be documented with generated requirement comments.

            ## REQ-SAMPLE-0001 Adds numbers requirement
            The system MUST verify addition behavior.

            ## REQ-SAMPLE-0002 Handles zero requirement
            The system MUST document zero handling.

            ## REQ-SAMPLE-0003 Handles zero fallback requirement
            The system MUST document the fallback path.
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
