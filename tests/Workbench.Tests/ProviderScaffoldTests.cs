using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class ProviderScaffoldTests
{
    [TestMethod]
    public void Scaffold_CopiesProviderSchemasAndCsvTemplates()
    {
        using var temp = new TempDirectory();

        ScaffoldService.Scaffold(temp.Path, force: false);

        foreach (string relativePath in new[]
        {
            "specs/schemas/resource-authority-config.schema.json",
            "specs/schemas/resource-provider-protocol-v1.schema.json",
            "specs/schemas/csv-specification-row-v1.schema.json",
            "specs/schemas/csv-work-item-row-v1.schema.json",
            "specs/templates/csv-specifications-v1.csv",
            "specs/templates/csv-work-items-v1.csv"
        })
        {
            Assert.IsTrue(File.Exists(Path.Combine(temp.Path, relativePath)), relativePath);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "workbench-provider-scaffold-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
