using System.Text.Json;
using Workbench.Core;

namespace Workbench.IntegrationTests;

[TestClass]
public sealed class ExecutionCommandTests
{
    [TestMethod]
    public void ExecutionShow_ReadsVersionedSnapshotWithoutProviderAccess()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        TestAssertions.RunWorkbenchAndAssertSuccess(repo.Path, "--repo", repo.Path, "scaffold");
        var item = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "item",
            "new",
            "--type",
            "work_item",
            "--title",
            "Observe execution state");
        var workItemId = item.GetProperty("data").GetProperty("id").GetString();
        Assert.IsNotNull(workItemId);

        var snapshot = new ExecutionStateSnapshot(
            ExecutionStateService.CurrentSchemaVersion,
            workItemId,
            "github",
            new DateTimeOffset(2026, 11, 22, 1, 2, 3, TimeSpan.Zero),
            [new ExecutionObservation(
                "github",
                ExecutionKinds.PullRequest,
                "github.com/incursa/workbench#616",
                "https://github.com/incursa/workbench/pull/616",
                ExecutionStates.Succeeded,
                "merged",
                new DateTimeOffset(2026, 11, 22, 1, 2, 3, TimeSpan.Zero),
                "abc123")],
            []);
        var snapshotPath = ExecutionStateService.GetSnapshotPath(repo.Path, workItemId);
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        File.WriteAllText(
            snapshotPath,
            JsonSerializer.Serialize(snapshot, WorkbenchJsonContext.Default.ExecutionStateSnapshot));

        var output = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "execution",
            "show",
            "--work-item",
            workItemId);

        Assert.IsTrue(output.GetProperty("ok").GetBoolean());
        var returned = output.GetProperty("data").GetProperty("snapshot");
        Assert.AreEqual(1, returned.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("merged", returned.GetProperty("observations")[0].GetProperty("nativeState").GetString());
    }
}
