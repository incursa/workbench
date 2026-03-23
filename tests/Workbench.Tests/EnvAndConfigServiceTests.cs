using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class EnvAndConfigServiceTests
{
    [TestMethod]
    public async Task EnvFileService_SetValue_CreatesNewFileWithHeaderAsync()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ".workbench", "credentials.env");

        var result = EnvFileService.SetValue(path, "OPENAI_API_KEY", "secret");

        Assert.IsTrue(result.Created);
        Assert.IsTrue(result.Updated);
        Assert.IsFalse(result.Removed);

        var content = await File.ReadAllTextAsync(path);
        StringAssert.Contains(content, "# Workbench credentials", StringComparison.Ordinal);
        StringAssert.Contains(content, "OPENAI_API_KEY=secret", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task EnvFileService_SetValue_UpdatesMatchingKeyAndPreservesCommentsAsync()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ".workbench", "credentials.env");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? temp.Path);
        await File.WriteAllTextAsync(
            path,
            """
            # Existing comment
            export OPENAI_API_KEY=old-value
            OTHER_KEY=keep

            """);

        var result = EnvFileService.SetValue(path, "OPENAI_API_KEY", "new-value");

        Assert.IsFalse(result.Created);
        Assert.IsTrue(result.Updated);
        Assert.IsFalse(result.Removed);

        var content = await File.ReadAllTextAsync(path);
        StringAssert.Contains(content, "# Existing comment", StringComparison.Ordinal);
        StringAssert.Contains(content, "export OPENAI_API_KEY=new-value", StringComparison.Ordinal);
        StringAssert.Contains(content, "OTHER_KEY=keep", StringComparison.Ordinal);
        Assert.IsFalse(content.Contains("old-value", StringComparison.Ordinal), content);
    }

    [TestMethod]
    public void EnvFileService_SetValue_UnchangedValue_IsNoOp()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ".workbench", "credentials.env");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? temp.Path);
        File.WriteAllText(path, "OPENAI_API_KEY=secret\n");
        var before = File.GetLastWriteTimeUtc(path);

        var result = EnvFileService.SetValue(path, "OPENAI_API_KEY", "secret");

        Assert.IsFalse(result.Created);
        Assert.IsFalse(result.Updated);
        Assert.IsFalse(result.Removed);
        Assert.AreEqual(before, File.GetLastWriteTimeUtc(path));
    }

    [TestMethod]
    public async Task EnvFileService_UnsetValue_RemovesOnlyMatchingKeyAsync()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ".workbench", "credentials.env");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? temp.Path);
        await File.WriteAllTextAsync(
            path,
            """
            KEEP_ME=1
            export OPENAI_API_KEY=secret
            OTHER_KEY=2
            """);

        var result = EnvFileService.UnsetValue(path, "OPENAI_API_KEY");

        Assert.IsFalse(result.Created);
        Assert.IsFalse(result.Updated);
        Assert.IsTrue(result.Removed);

        var content = await File.ReadAllTextAsync(path);
        StringAssert.Contains(content, "KEEP_ME=1", StringComparison.Ordinal);
        StringAssert.Contains(content, "OTHER_KEY=2", StringComparison.Ordinal);
        Assert.IsFalse(content.Contains("OPENAI_API_KEY", StringComparison.Ordinal), content);
    }

    [TestMethod]
    public void EnvFileService_UnsetValue_MissingFile_IsNoOp()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ".workbench", "credentials.env");

        var result = EnvFileService.UnsetValue(path, "OPENAI_API_KEY");

        Assert.IsFalse(result.Created);
        Assert.IsFalse(result.Updated);
        Assert.IsFalse(result.Removed);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void ConfigService_SetConfigValue_UpdatesNestedStringPath()
    {
        var updated = ConfigService.SetConfigValue(
            WorkbenchConfig.Default,
            "paths.docsRoot",
            "knowledge",
            parseJson: false,
            out var changed);

        Assert.IsTrue(changed);
        Assert.AreEqual("knowledge", updated.Paths.DocsRoot);
    }

    [TestMethod]
    public void ConfigService_SetConfigValue_WithJsonObject_UpdatesNestedObject()
    {
        var updated = ConfigService.SetConfigValue(
            WorkbenchConfig.Default,
            "paths",
            """{"docsRoot":"knowledge","itemsDir":"specs/work-items/WB","specsTemplatesDir":"specs/templates","workRoot":"specs/work-items"}""",
            parseJson: true,
            out var changed);

        Assert.IsTrue(changed);
        Assert.AreEqual("knowledge", updated.Paths.DocsRoot);
        Assert.AreEqual("specs/work-items/WB", updated.Paths.ItemsDir);
        Assert.AreEqual("specs/templates", updated.Paths.SpecsTemplatesDir);
    }

    [TestMethod]
    public void ConfigService_SetConfigValue_RejectsUnknownPath()
    {
        try
        {
            _ = ConfigService.SetConfigValue(
                WorkbenchConfig.Default,
                "paths.missingSegment",
                "value",
                parseJson: false,
                out _);
            Assert.Fail("Expected unknown config path to fail.");
        }
        catch (InvalidOperationException error)
        {
            StringAssert.Contains(error.Message, "Unknown config path segment", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void ConfigService_SetConfigValue_InvalidJson_Throws()
    {
        try
        {
            _ = ConfigService.SetConfigValue(
                WorkbenchConfig.Default,
                "paths",
                "{",
                parseJson: true,
                out _);
            Assert.Fail("Expected invalid JSON to fail.");
        }
        catch (InvalidOperationException error)
        {
            StringAssert.Contains(error.Message, "Invalid JSON value", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task ConfigService_SaveConfig_WritesWorkbenchConfigFileWithTrailingNewlineAsync()
    {
        using var temp = new TempDirectory();

        ConfigService.SaveConfig(temp.Path, WorkbenchConfig.Default with
        {
            Paths = WorkbenchConfig.Default.Paths with
            {
                DocsRoot = "knowledge"
            }
        });

        var configPath = WorkbenchConfig.GetConfigPath(temp.Path);
        Assert.IsTrue(File.Exists(configPath));

        var content = await File.ReadAllTextAsync(configPath);
        StringAssert.Contains(content, "\"DocsRoot\": \"knowledge\"", StringComparison.Ordinal);
        Assert.IsTrue(content.EndsWith("\n", StringComparison.Ordinal), "Expected config file to end with a newline.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
