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
              "artifact_id": "SPEC-WB-BROKEN",
              "artifact_type": "specification",
              "title": "Broken spec",
              "domain": "WB",
              "status": "draft",
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

        Assert.IsTrue(errors.Any(error => error.Contains("owner", StringComparison.OrdinalIgnoreCase)), string.Join(Environment.NewLine, errors));
    }

    private sealed class TempJsonRepo : IDisposable
    {
        public TempJsonRepo()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-json-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "model"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "requirements", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "architecture", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "work-items", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "verification", "WB"));
            File.WriteAllText(System.IO.Path.Combine(Path, "model", "model.schema.json"), BuildSchema());
        }

        public string Path { get; }

        public void WriteCanonicalArtifacts()
        {
            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "requirements", "WB", "SPEC-WB-JSON.json"), """
                {
                  "artifact_id": "SPEC-WB-JSON",
                  "artifact_type": "specification",
                  "title": "Json-backed validation",
                  "domain": "WB",
                  "capability": "validation",
                  "status": "draft",
                  "owner": "platform",
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
                  "artifact_id": "ARC-WB-JSON-0001",
                  "artifact_type": "architecture",
                  "title": "Json-backed architecture",
                  "domain": "WB",
                  "capability": "validation",
                  "status": "implemented",
                  "owner": "platform",
                  "satisfies": [
                    "REQ-WB-JSON-0001"
                  ]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "work-items", "WB", "WI-WB-JSON-0001.json"), """
                {
                  "artifact_id": "WI-WB-JSON-0001",
                  "artifact_type": "work_item",
                  "title": "Json-backed work item",
                  "domain": "WB",
                  "capability": "validation",
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
                  ]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "verification", "WB", "VER-WB-JSON-0001.json"), """
                {
                  "artifact_id": "VER-WB-JSON-0001",
                  "artifact_type": "verification",
                  "title": "Json-backed verification",
                  "domain": "WB",
                  "capability": "validation",
                  "status": "passed",
                  "owner": "platform",
                  "verifies": [
                    "REQ-WB-JSON-0001"
                  ]
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

        private static string BuildSchema()
        {
            return """
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "oneOf": [
                    {
                      "$ref": "#/$defs/specificationArtifact"
                    },
                    {
                      "$ref": "#/$defs/architectureArtifact"
                    },
                    {
                      "$ref": "#/$defs/workItemArtifact"
                    },
                    {
                      "$ref": "#/$defs/verificationArtifact"
                    }
                  ],
                  "$defs": {
                    "nonEmptyString": {
                      "type": "string",
                      "minLength": 1
                    },
                    "stringList": {
                      "type": "array",
                      "items": {
                        "$ref": "#/$defs/nonEmptyString"
                      },
                      "minItems": 1
                    },
                    "artifactBase": {
                      "type": "object",
                      "required": [
                        "artifact_id",
                        "artifact_type",
                        "title",
                        "domain",
                        "status",
                        "owner"
                      ],
                      "properties": {
                        "artifact_id": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "artifact_type": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "title": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "domain": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "capability": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "status": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "owner": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "related_artifacts": {
                          "$ref": "#/$defs/stringList"
                        }
                      },
                      "additionalProperties": true
                    },
                    "requirement": {
                      "type": "object",
                      "required": [
                        "id",
                        "title",
                        "statement"
                      ],
                      "properties": {
                        "id": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "title": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "statement": {
                          "$ref": "#/$defs/nonEmptyString"
                        },
                        "trace": {
                          "type": "object",
                          "additionalProperties": true
                        }
                      },
                      "additionalProperties": true
                    },
                    "specificationArtifact": {
                      "allOf": [
                        {
                          "$ref": "#/$defs/artifactBase"
                        },
                        {
                          "type": "object",
                          "properties": {
                            "artifact_type": {
                              "const": "specification"
                            },
                            "requirements": {
                              "type": "array",
                              "minItems": 1,
                              "items": {
                                "$ref": "#/$defs/requirement"
                              }
                            }
                          },
                          "required": [
                            "requirements"
                          ]
                        }
                      ]
                    },
                    "architectureArtifact": {
                      "allOf": [
                        {
                          "$ref": "#/$defs/artifactBase"
                        },
                        {
                          "type": "object",
                          "properties": {
                            "artifact_type": {
                              "const": "architecture"
                            },
                            "satisfies": {
                              "$ref": "#/$defs/stringList"
                            }
                          },
                          "required": [
                            "satisfies"
                          ]
                        }
                      ]
                    },
                    "workItemArtifact": {
                      "allOf": [
                        {
                          "$ref": "#/$defs/artifactBase"
                        },
                        {
                          "type": "object",
                          "properties": {
                            "artifact_type": {
                              "const": "work_item"
                            },
                            "addresses": {
                              "$ref": "#/$defs/stringList"
                            },
                            "design_links": {
                              "$ref": "#/$defs/stringList"
                            },
                            "verification_links": {
                              "$ref": "#/$defs/stringList"
                            }
                          },
                          "required": [
                            "addresses",
                            "design_links",
                            "verification_links"
                          ]
                        }
                      ]
                    },
                    "verificationArtifact": {
                      "allOf": [
                        {
                          "$ref": "#/$defs/artifactBase"
                        },
                        {
                          "type": "object",
                          "properties": {
                            "artifact_type": {
                              "const": "verification"
                            },
                            "verifies": {
                              "$ref": "#/$defs/stringList"
                            }
                          },
                          "required": [
                            "verifies"
                          ]
                        }
                      ]
                    }
                  }
                }
                """;
        }
    }
}
