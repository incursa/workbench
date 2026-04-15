using BenchmarkDotNet.Attributes;
using Workbench.Core;

namespace Workbench.Benchmarks;

[MemoryDiagnoser]
public class CanonicalValidationBenchmarks
{
    private string repoRoot = string.Empty;
    private string canonicalArtifactJson = string.Empty;
    private string frontMatterDocument = string.Empty;
    private string requirementBody = string.Empty;
    private string artifactPath = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        repoRoot = Path.Combine(Path.GetTempPath(), "workbench-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "specs", "requirements", "WB"));

        artifactPath = Path.Combine(repoRoot, "specs", "requirements", "WB", "SPEC-WB-BENCH-0001.json");

        canonicalArtifactJson = """
            {
              "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
              "artifact_id": "SPEC-WB-BENCH-0001",
              "artifact_type": "specification",
              "title": "Benchmark validation",
              "domain": "wb",
              "capability": "validation",
              "status": "draft",
              "owner": "platform",
              "purpose": "Exercise canonical validation in benchmark form.",
              "scope": "Validate the canonical JSON schema snapshot against a representative artifact.",
              "context": "Workbench keeps a pinned schema snapshot and normalizes current repo-native compatibility inputs.",
              "requirements": [
                {
                  "id": "REQ-WB-BENCH-0001",
                  "title": "Benchmark validation",
                  "statement": "The tool MUST validate canonical JSON efficiently.",
                  "coverage": {
                    "positive": "required",
                    "negative": "optional",
                    "edge": "optional",
                    "fuzz": "deferred"
                  },
                  "trace": {
                    "satisfied_by": [
                      "ARC-WB-BENCH-0001"
                    ],
                    "implemented_by": [
                      "WI-WB-BENCH-0001"
                    ],
                    "verified_by": [
                      "VER-WB-BENCH-0001"
                    ]
                  }
                }
              ]
            }
            """;

        frontMatterDocument = """
            ---
            artifact_id: "WI-WB-BENCH-0001"
            artifact_type: "work_item"
            title: "Benchmark work item"
            domain: "wb"
            status: "planned"
            owner: "platform"
            addresses:
              - "REQ-WB-BENCH-0001"
            design_links:
              - "ARC-WB-BENCH-0001"
            verification_links:
              - "VER-WB-BENCH-0001"
            ---

            # WI-WB-BENCH-0001 - Benchmark work item

            ## Summary

            Exercise front matter parsing for benchmark coverage.
            """;

        requirementBody = """
            # SPEC-WB-BENCH-0001 - Benchmark validation

            ## Purpose

            Exercise canonical requirement parsing and validation.

            ## REQ-WB-BENCH-0001 Benchmark validation
            The tool MUST validate canonical JSON efficiently.

            Trace:
            - Satisfied By:
              - ARC-WB-BENCH-0001
            - Implemented By:
              - WI-WB-BENCH-0001
            - Verified By:
              - VER-WB-BENCH-0001
            """;
    }

    [Benchmark]
    public bool TryParseFrontMatter()
    {
        return FrontMatter.TryParse(frontMatterDocument, out _, out _);
    }

    [Benchmark]
    public int ParseRequirementClauses()
    {
        return SpecTraceMarkdown.ParseRequirementClauses(requirementBody, out _).Count;
    }

    [Benchmark]
    public int ValidateCanonicalArtifactJson()
    {
        return SchemaValidationService.ValidateCanonicalArtifactJson(repoRoot, artifactPath, canonicalArtifactJson).Count;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
#pragma warning disable ERP022
        try
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
#pragma warning restore ERP022
    }
}
