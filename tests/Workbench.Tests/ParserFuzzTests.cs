using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ParserFuzzTests
{
    [TestMethod]
    public void FrontMatterTryParse_RandomInputs_DoesNotThrow()
    {
        var random = new Random(1337);
        for (var i = 0; i < 250; i++)
        {
            var input = BuildRandomText(random, maxLength: 512);

            try
            {
                _ = FrontMatter.TryParse(input, out _, out _);
            }
            catch (Exception ex)
            {
                Assert.Fail($"FrontMatter.TryParse threw on iteration {i}: {ex}");
            }
        }
    }

    [TestMethod]
    public void SchemaValidation_RandomFrontMatterPayloads_DoesNotThrow()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs", "30-contracts"));
        File.WriteAllText(
            Path.Combine(repoRoot, "docs", "30-contracts", "work-item.schema.json"),
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object"
            }
            """);

        var random = new Random(4242);
        for (var i = 0; i < 120; i++)
        {
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = $"TASK-{random.Next(1, 9999):0000}",
                ["type"] = random.Next(0, 3) switch
                {
                    0 => "task",
                    1 => "bug",
                    _ => "spike",
                },
                ["status"] = random.Next(0, 4) switch
                {
                    0 => "draft",
                    1 => "ready",
                    2 => "in-progress",
                    _ => "done",
                },
                ["created"] = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                ["related"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["specs"] = new[] { BuildRandomText(random, 18) },
                    ["adrs"] = Array.Empty<string>(),
                    ["files"] = Array.Empty<string>(),
                    ["prs"] = Array.Empty<string>(),
                    ["issues"] = Array.Empty<string>(),
                    ["branches"] = Array.Empty<string>(),
                },
            };

            try
            {
                _ = SchemaValidationService.ValidateFrontMatter(
                    repoRoot,
                    $"docs/70-work/items/TASK-{i:0000}-fuzz.md",
                    payload);
            }
            catch (Exception ex)
            {
                Assert.Fail($"SchemaValidationService.ValidateFrontMatter threw on iteration {i}: {ex}");
            }
        }
    }

    private static string BuildRandomText(Random random, int maxLength)
    {
        var length = random.Next(0, maxLength + 1);
        var chars = new char[length];
        const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_/.: \n\r\t";
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[random.Next(0, Alphabet.Length)];
        }
        return new string(chars);
    }
}
