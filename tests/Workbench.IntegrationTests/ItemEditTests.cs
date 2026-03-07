namespace Workbench.IntegrationTests;

[TestClass]
public class ItemEditTests
{
    [TestMethod]
    public void ItemEdit_JsonUpdatesStructuredFields_AndRenamesPath()
    {
        using var repo = TempRepo.Create();
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git"));

        TestAssertions.RunWorkbenchAndAssertSuccess(repo.Path, "--repo", repo.Path, "scaffold");
        var createJson = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "item",
            "new",
            "--type",
            "task",
            "--title",
            "Original title",
            "--format",
            "json");
        var id = createJson.GetProperty("data").GetProperty("id").GetString()!;

        var summaryFile = Path.Combine(repo.Path, "summary.txt");
        File.WriteAllText(summaryFile, "Updated summary from file.\n\nSecond paragraph.");

        var editJson = TestAssertions.RunWorkbenchAndParseJson(
            repo.Path,
            "--repo",
            repo.Path,
            "item",
            "edit",
            id,
            "--title",
            "Updated title",
            "--summary-file",
            "summary.txt",
            "--acceptance",
            "first acceptance",
            "--acceptance",
            "second acceptance",
            "--append-note",
            "Added note from edit flow.",
            "--format",
            "json");

        var data = editJson.GetProperty("data");
        var updatedItem = data.GetProperty("item");
        Assert.IsTrue(data.GetProperty("pathChanged").GetBoolean());
        Assert.IsTrue(data.GetProperty("titleUpdated").GetBoolean());
        Assert.IsTrue(data.GetProperty("summaryUpdated").GetBoolean());
        Assert.IsTrue(data.GetProperty("acceptanceCriteriaUpdated").GetBoolean());
        Assert.IsTrue(data.GetProperty("notesAppended").GetBoolean());
        Assert.AreEqual("Updated title", updatedItem.GetProperty("title").GetString());

        var itemPath = updatedItem.GetProperty("path").GetString()!;
        var body = updatedItem.GetProperty("body").GetString()!;
        Assert.IsTrue(File.Exists(itemPath));
        StringAssert.Contains(itemPath, "updated-title.md", StringComparison.Ordinal);
        StringAssert.Contains(body, "# ", StringComparison.Ordinal);
        StringAssert.Contains(body, "Updated summary from file.", StringComparison.Ordinal);
        StringAssert.Contains(body, "- first acceptance", StringComparison.Ordinal);
        StringAssert.Contains(body, "- second acceptance", StringComparison.Ordinal);
        StringAssert.Contains(body, "- Added note from edit flow.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void ItemEdit_HelpListsStructuredEditOptions()
    {
        var result = WorkbenchCli.Run(Environment.CurrentDirectory, "item", "edit", "--help");
        Assert.AreEqual(0, result.ExitCode, $"stderr: {result.StdErr}\nstdout: {result.StdOut}");
        StringAssert.Contains(result.StdOut, "Safely edit structured work item fields", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "--summary-file", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "--acceptance-file", StringComparison.Ordinal);
        StringAssert.Contains(result.StdOut, "--keep-path", StringComparison.Ordinal);
    }
}
