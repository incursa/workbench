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
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "schemas"));
        File.WriteAllText(
            Path.Combine(repoRoot, "specs", "schemas", "artifact-frontmatter.schema.json"),
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
                ["artifact_id"] = $"WI-WB-{random.Next(1, 9999):D4}",
                ["artifact_type"] = "work_item",
                ["title"] = $"Fuzz item {i}",
                ["domain"] = "WB",
                ["status"] = random.Next(0, 4) switch
                {
                    0 => "planned",
                    1 => "in_progress",
                    2 => "blocked",
                    _ => "complete",
                },
                ["owner"] = "platform",
                ["addresses"] = new[] { $"REQ-WB-{i:D4}" },
                ["design_links"] = new[] { $"ARC-WB-{i:D4}" },
                ["verification_links"] = new[] { $"VER-WB-{i:D4}" },
                ["related_artifacts"] = new[] { $"SPEC-WB-{i:D4}" },
                ["trace"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Satisfied By"] = new[] { BuildRandomText(random, 18) },
                    ["Implemented By"] = Array.Empty<string>(),
                    ["Verified By"] = Array.Empty<string>(),
                },
            };

            try
            {
                _ = SchemaValidationService.ValidateFrontMatter(
                    repoRoot,
                    $"specs/work-items/WB/WI-WB-{i:D4}-fuzz.md",
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
