using System.Text.Json;
using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class SchemaValidationTests
{
    [TestMethod]
    public void ValidateFrontMatter_ReturnsSchemaErrors()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

        var schemaDir = Path.Combine(repoRoot, "docs", "30-contracts");
        Directory.CreateDirectory(schemaDir);
        File.WriteAllText(Path.Combine(schemaDir, "work-item.schema.json"), """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["id"]
            }
            """);

        var data = new Dictionary<string, object?>(StringComparer.InvariantCulture) { ["type"] = "task" };
        var errors = SchemaValidationService.ValidateFrontMatter(repoRoot, "docs/70-work/items/TASK-0001-test.md", data);
        Assert.IsNotEmpty(errors);
    }

    [TestMethod]
    public void ValidateConfig_ReturnsSchemaErrors()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

        var schemaDir = Path.Combine(repoRoot, "docs", "30-contracts");
        Directory.CreateDirectory(schemaDir);
        File.WriteAllText(Path.Combine(schemaDir, "workbench-config.schema.json"), """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "required": ["paths"]
            }
            """);

        var configDir = Path.Combine(repoRoot, ".workbench");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "config.json"), JsonSerializer.Serialize(new { ids = new { width = 4 } }));

        var errors = SchemaValidationService.ValidateConfig(repoRoot);
        Assert.IsNotEmpty(errors);
    }

    [TestMethod]
    public void ValidateDocFrontMatter_AllowsContractDocType()
    {
        var tempRepoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRepoRoot);
        Directory.CreateDirectory(Path.Combine(tempRepoRoot, ".git"));

        var schemaDir = Path.Combine(tempRepoRoot, "docs", "30-contracts");
        Directory.CreateDirectory(schemaDir);

        var sourceRepoRoot = Repository.FindRepoRoot(Directory.GetCurrentDirectory())
            ?? throw new InvalidOperationException("Repo root not found for schema fixture.");
        var sourceSchemaPath = Path.Combine(sourceRepoRoot, "docs", "30-contracts", "doc.schema.json");
        File.Copy(sourceSchemaPath, Path.Combine(schemaDir, "doc.schema.json"));

        var data = new Dictionary<string, object?>(StringComparer.InvariantCulture)
        {
            ["workbench"] = new Dictionary<string, object?>
(StringComparer.Ordinal)
            {
                ["type"] = "contract",
                ["workItems"] = Array.Empty<string>(),
                ["codeRefs"] = Array.Empty<string>()
            },
            ["owner"] = "platform",
            ["status"] = "active",
            ["updated"] = "2026-03-07"
        };

        var errors = SchemaValidationService.ValidateDocFrontMatter(
            tempRepoRoot,
            "docs/30-contracts/error-codes.md",
            data);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }
}
