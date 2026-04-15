using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ParserFuzzTests
{
    [TestMethod]
    [TestCategory("Fuzz")]
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
    [TestCategory("Fuzz")]
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

    [TestMethod]
    [TestCategory("Fuzz")]
    public void SpecTraceMarkdown_ParseRequirementClauses_RandomInputs_DoesNotThrow()
    {
        var random = new Random(9001);
        for (var i = 0; i < 250; i++)
        {
            var input = BuildRandomText(random, maxLength: 1024);

            try
            {
                _ = SpecTraceMarkdown.ParseRequirementClauses(input, out _);
            }
            catch (Exception ex)
            {
                Assert.Fail($"SpecTraceMarkdown.ParseRequirementClauses threw on iteration {i}: {ex}");
            }
        }
    }

    [TestMethod]
    [TestCategory("Fuzz")]
    public void SchemaValidation_RandomCanonicalArtifactJsonPayloads_DoesNotThrow()
    {
        using var repo = CreateTempSchemaRepo();
        var random = new Random(5150);

        for (var i = 0; i < 160; i++)
        {
            var input = BuildRandomCanonicalJson(random);

            try
            {
                _ = SchemaValidationService.ValidateCanonicalArtifactJson(
                    repo.Path,
                    Path.Combine(repo.Path, "specs", "requirements", "WB", $"SPEC-WB-FUZZ-{i:D4}.json"),
                    input);
            }
            catch (Exception ex)
            {
                Assert.Fail($"SchemaValidationService.ValidateCanonicalArtifactJson threw on iteration {i}: {ex}");
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

    private static string BuildRandomCanonicalJson(Random random)
    {
        return random.Next(0, 5) switch
        {
            0 => "{}",
            1 => """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "SPEC-WB-FUZZ-0001",
                  "artifact_type": "specification",
                  "title": "Fuzz validation",
                  "domain": "wb",
                  "status": "landed",
                  "requirements": [
                    {
                      "id": "REQ-WB-FUZZ-0001",
                      "title": "Fuzz validation requirement",
                      "statement": "The tool MUST accept fuzzed canonical JSON."
                    }
                  ]
                }
                """,
            2 => """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "ARC-WB-FUZZ-0001",
                  "artifact_type": "architecture",
                  "title": "Fuzz architecture",
                  "domain": "wb",
                  "status": "landed",
                  "owner": "platform",
                  "satisfies": [
                    "REQ-WB-FUZZ-0001"
                  ]
                }
                """,
            3 => """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "WI-WB-FUZZ-0001",
                  "artifact_type": "work_item",
                  "title": "Fuzz work item",
                  "domain": "wb",
                  "status": "landed",
                  "owner": "platform",
                  "addresses": [
                    "REQ-WB-FUZZ-0001"
                  ]
                }
                """,
            _ => BuildRandomText(random, 512)
        };
    }

    private static TempSchemaRepo CreateTempSchemaRepo()
    {
        var repoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-fuzz-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(System.IO.Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(System.IO.Path.Combine(repoRoot, "specs", "requirements", "WB"));
        return new TempSchemaRepo(repoRoot);
    }

    private sealed class TempSchemaRepo(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
#pragma warning disable ERP022
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }
}
