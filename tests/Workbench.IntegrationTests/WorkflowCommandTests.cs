namespace Workbench.IntegrationTests;

[TestClass]
public class WorkflowCommandTests
{
    [TestMethod]
    public void Context_JsonReturnsVersionedPolicyAndOneNextAction()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        TestAssertions.RunWorkbenchAndAssertSuccess(repo.Path, "--repo", repo.Path, "scaffold");
        CopyValidationSchemas(repo.Path);

        var created = TestAssertions.RunWorkbenchAndParseJson(
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
            "Context command coverage");
        var id = created.GetProperty("data").GetProperty("id").GetString();

        var result = WorkbenchCli.Run(
            repo.Path,
            "--repo",
            repo.Path,
            "--format",
            "json",
            "context",
            "--work-item",
            id!);

        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        var payload = TestAssertions.ParseJson(result.StdOut);
        Assert.IsTrue(payload.GetProperty("ok").GetBoolean(), payload.ToString());
        var data = payload.GetProperty("data");
        Assert.AreEqual(1, data.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("standard", data.GetProperty("policy").GetProperty("level").GetString());
        Assert.AreEqual(id, data.GetProperty("selectedWorkItem").GetProperty("id").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            data.GetProperty("recommendedNextAction").GetProperty("command").GetString()));
    }

    [TestMethod]
    public void Finish_JsonIsIdempotentAndRecordsPullRequestEvidenceOnce()
    {
        using var repo = TempRepo.Create();
        GitTestRepo.InitializeGitRepo(repo.Path);
        TestAssertions.RunWorkbenchAndAssertSuccess(repo.Path, "--repo", repo.Path, "scaffold");
        CopyValidationSchemas(repo.Path);

        var created = TestAssertions.RunWorkbenchAndParseJson(
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
            "Finish command coverage");
        var item = created.GetProperty("data");
        var id = item.GetProperty("id").GetString()!;
        var path = item.GetProperty("path").GetString()!;
        const string PullRequest = "https://github.com/incursa/workbench/pull/42";

        var first = RunFinish(repo.Path, id, PullRequest);
        Assert.AreEqual(2, first.ExitCode, $"stderr: {first.StdErr}\nstdout: {first.StdOut}");
        var firstData = TestAssertions.ParseJson(first.StdOut).GetProperty("data");
        Assert.AreEqual(1, firstData.GetProperty("schemaVersion").GetInt32());
        Assert.IsTrue(firstData.GetProperty("statusChanged").GetBoolean());
        Assert.IsTrue(firstData.GetProperty("evidenceChanged").GetBoolean());
        Assert.IsFalse(firstData.GetProperty("validation").GetProperty("ok").GetBoolean());

        var second = RunFinish(repo.Path, id, PullRequest);
        Assert.AreEqual(2, second.ExitCode, $"stderr: {second.StdErr}\nstdout: {second.StdOut}");
        var secondData = TestAssertions.ParseJson(second.StdOut).GetProperty("data");
        Assert.IsFalse(secondData.GetProperty("statusChanged").GetBoolean());
        Assert.IsFalse(secondData.GetProperty("evidenceChanged").GetBoolean());

        var content = File.ReadAllText(path);
        Assert.AreEqual(1, CountOccurrences(content, $"Pull request: {PullRequest}"));
    }

    private static CommandResult RunFinish(string repoPath, string id, string pullRequest)
    {
        return WorkbenchCli.Run(
            repoPath,
            "--repo",
            repoPath,
            "--format",
            "json",
            "finish",
            id,
            "--pr",
            pullRequest);
    }

    private static int CountOccurrences(string value, string search)
    {
        return value.Split(search, StringSplitOptions.None).Length - 1;
    }

    private static void CopyValidationSchemas(string repoPath)
    {
        var sourceRoot = FindSourceRepoRoot();
        Directory.CreateDirectory(Path.Combine(repoPath, "schemas"));
        Directory.CreateDirectory(Path.Combine(repoPath, "specs", "schemas"));
        foreach (var relativePath in new[]
        {
            Path.Combine("schemas", "workbench-config.schema.json"),
            Path.Combine("specs", "schemas", "artifact-frontmatter.schema.json"),
            Path.Combine("specs", "schemas", "work-item-trace-fields.schema.json")
        })
        {
            File.Copy(
                Path.Combine(sourceRoot, relativePath),
                Path.Combine(repoPath, relativePath),
                overwrite: true);
        }
    }

    private static string FindSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Workbench.slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the source repository.");
    }
}
