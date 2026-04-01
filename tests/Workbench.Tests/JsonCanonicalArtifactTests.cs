using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class JsonCanonicalArtifactTests
{
    [TestMethod]
    public void ValidateRepo_LoadsJsonCanonicalArtifacts()
    {
        using var repo = new TempJsonRepo();
        repo.WriteCanonicalArtifacts();

        var result = ValidationService.ValidateRepo(
            repo.Path,
            WorkbenchConfig.Default,
            new ValidationOptions(
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                ValidationProfiles.Auditable,
                Array.Empty<string>()));

        Assert.IsEmpty(result.Errors, string.Join(Environment.NewLine, result.Errors));
        Assert.IsEmpty(result.Warnings, string.Join(Environment.NewLine, result.Warnings));
        Assert.AreEqual(1, result.WorkItemCount);
    }

    [TestMethod]
    public void CanonicalArtifactJsonLoader_RequiresSchemaValidPayload()
    {
        using var repo = new TempJsonRepo();
        File.WriteAllText(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-BROKEN.json"),
            """
            {
              "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
              "artifact_id": "SPEC-WB-BROKEN",
              "artifact_type": "specification",
              "title": "Broken spec",
              "domain": "wb",
              "capability": "validation",
              "status": "draft",
              "purpose": "Confirm the pinned canonical schema rejects incomplete specification payloads.",
              "requirements": [
                {
                  "id": "REQ-WB-BROKEN-0001",
                  "title": "Broken requirement",
                  "statement": "The tool MUST reject invalid canonical JSON."
                }
              ]
            }
            """);

        var errors = SchemaValidationService.ValidateCanonicalArtifactJson(
            repo.Path,
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-BROKEN.json"));

        Assert.IsNotEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void CanonicalArtifactJsonLoader_IgnoresRepoLocalSchemaFiles()
    {
        using var repo = new TempJsonRepo();
        repo.WriteCanonicalArtifacts();
        Directory.CreateDirectory(Path.Combine(repo.Path, "model"));
        File.WriteAllText(Path.Combine(repo.Path, "model", "model.schema.json"), "{ not-valid-json");

        var errors = SchemaValidationService.ValidateCanonicalArtifactJson(
            repo.Path,
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-JSON.json"));

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    private sealed class TempJsonRepo : IDisposable
    {
        public TempJsonRepo()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-json-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "requirements", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "architecture", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "work-items", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "verification", "WB"));
        }

        public string Path { get; }

        public void WriteCanonicalArtifacts()
        {
            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "requirements", "WB", "SPEC-WB-JSON.json"), """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "SPEC-WB-JSON",
                  "artifact_type": "specification",
                  "title": "Json-backed validation",
                  "domain": "wb",
                  "capability": "validation",
                  "status": "draft",
                  "owner": "platform",
                  "purpose": "Exercise canonical JSON validation using the schema pinned into Workbench.",
                  "scope": "Cover the minimal cross-artifact graph needed by the validation tests.",
                  "context": "Canonical JSON artifacts should load without a repo-local model schema file.",
                  "related_artifacts": [
                    "ARC-WB-JSON-0001",
                    "WI-WB-JSON-0001",
                    "VER-WB-JSON-0001"
                  ],
                  "requirements": [
                    {
                      "id": "REQ-WB-JSON-0001",
                      "title": "Load canonical JSON artifacts",
                      "statement": "The tool MUST load canonical JSON artifacts from the spec-trace roots.",
                      "trace": {
                        "satisfied_by": [
                          "ARC-WB-JSON-0001"
                        ],
                        "implemented_by": [
                          "WI-WB-JSON-0001"
                        ],
                        "verified_by": [
                          "VER-WB-JSON-0001"
                        ]
                      }
                    }
                  ]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "architecture", "WB", "ARC-WB-JSON-0001.json"), """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "ARC-WB-JSON-0001",
                  "artifact_type": "architecture",
                  "title": "Json-backed architecture",
                  "domain": "wb",
                  "status": "implemented",
                  "owner": "platform",
                  "purpose": "Describe how Workbench validates canonical JSON with a pinned schema snapshot.",
                  "design_summary": "Workbench loads the pinned schema from its own assembly resources and validates artifacts against that snapshot.",
                  "satisfies": [
                    "REQ-WB-JSON-0001"
                  ]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "work-items", "WB", "WI-WB-JSON-0001.json"), """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "WI-WB-JSON-0001",
                  "artifact_type": "work_item",
                  "title": "Json-backed work item",
                  "domain": "wb",
                  "status": "complete",
                  "owner": "platform",
                  "addresses": [
                    "REQ-WB-JSON-0001"
                  ],
                  "design_links": [
                    "ARC-WB-JSON-0001"
                  ],
                  "verification_links": [
                    "VER-WB-JSON-0001"
                  ],
                  "summary": "Update Workbench to validate canonical JSON against its pinned schema snapshot.",
                  "planned_changes": "Load the schema from embedded resources instead of searching the target repository for a model directory.",
                  "verification_plan": "Exercise repo validation and direct canonical artifact validation without a repo-local model schema file."
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "verification", "WB", "VER-WB-JSON-0001.json"), """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "VER-WB-JSON-0001",
                  "artifact_type": "verification",
                  "title": "Json-backed verification",
                  "domain": "wb",
                  "status": "passed",
                  "owner": "platform",
                  "verifies": [
                    "REQ-WB-JSON-0001"
                  ],
                  "scope": "Confirm the pinned schema validates the canonical JSON graph.",
                  "verification_method": "Run Workbench validation over a minimal repository containing only canonical JSON artifacts.",
                  "procedure": [
                    "Create the minimal canonical JSON artifact set.",
                    "Run the validation entry point against the repository."
                  ],
                  "expected_result": "The repository validates without depending on a repo-local model schema file."
                }
                """);
        }

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
