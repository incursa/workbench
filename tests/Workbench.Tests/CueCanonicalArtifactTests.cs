using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class CueCanonicalArtifactTests
{
    private static string? originalCueOverride;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        originalCueOverride = Environment.GetEnvironmentVariable("WORKBENCH_CUE_BIN");
        Environment.SetEnvironmentVariable("WORKBENCH_CUE_BIN", ResolveRepoCueExecutable());
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Environment.SetEnvironmentVariable("WORKBENCH_CUE_BIN", originalCueOverride);
    }

    [TestMethod]
    public void ValidateRepo_LoadsCueCanonicalArtifacts()
    {
        using var repo = new TempCueRepo();
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

    private sealed class TempCueRepo : IDisposable
    {
        public TempCueRepo()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-cue-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "requirements", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "architecture", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "work-items", "WB"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "verification", "WB"));
        }

        public string Path { get; }

        public void WriteCanonicalArtifacts()
        {
            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "requirements", "WB", "SPEC-WB-CUE.cue"), """
                package artifacts

                artifact: {
                  artifact_id: "SPEC-WB-CUE"
                  artifact_type: "specification"
                  title: "Cue-backed validation"
                  domain: "WB"
                  capability: "validation"
                  status: "draft"
                  owner: "platform"
                  related_artifacts: [
                    "ARC-WB-CUE-0001",
                    "WI-WB-CUE-0001",
                    "VER-WB-CUE-0001",
                  ]
                  requirements: [{
                    id: "REQ-WB-CUE-0001"
                    title: "Load canonical CUE artifacts"
                    statement: "The tool MUST load canonical CUE artifacts from the spec-trace roots."
                    trace: {
                      satisfied_by: ["ARC-WB-CUE-0001"]
                      implemented_by: ["WI-WB-CUE-0001"]
                      verified_by: ["VER-WB-CUE-0001"]
                    }
                  }]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "architecture", "WB", "ARC-WB-CUE-0001.cue"), """
                package artifacts

                artifact: {
                  artifact_id: "ARC-WB-CUE-0001"
                  artifact_type: "architecture"
                  title: "Cue-backed architecture"
                  domain: "WB"
                  capability: "validation"
                  status: "implemented"
                  owner: "platform"
                  satisfies: ["REQ-WB-CUE-0001"]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "work-items", "WB", "WI-WB-CUE-0001.cue"), """
                package artifacts

                artifact: {
                  artifact_id: "WI-WB-CUE-0001"
                  artifact_type: "work_item"
                  title: "Cue-backed work item"
                  domain: "WB"
                  capability: "validation"
                  status: "complete"
                  owner: "platform"
                  addresses: ["REQ-WB-CUE-0001"]
                  design_links: ["ARC-WB-CUE-0001"]
                  verification_links: ["VER-WB-CUE-0001"]
                }
                """);

            File.WriteAllText(System.IO.Path.Combine(Path, "specs", "verification", "WB", "VER-WB-CUE-0001.cue"), """
                package artifacts

                artifact: {
                  artifact_id: "VER-WB-CUE-0001"
                  artifact_type: "verification"
                  title: "Cue-backed verification"
                  domain: "WB"
                  capability: "validation"
                  status: "passed"
                  owner: "platform"
                  verifies: ["REQ-WB-CUE-0001"]
                }
                """);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
#pragma warning disable ERP022
            catch
            {
                // Best-effort cleanup.
            }
#pragma warning restore ERP022
        }
    }

    private static string ResolveRepoCueExecutable()
    {
        var repoRoot = FindRepoRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(Path.Combine(repoRoot, "scripts", "Resolve-Cue.ps1"));
        startInfo.ArgumentList.Add("-RootPath");
        startInfo.ArgumentList.Add(repoRoot);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEnd().Trim();
        var standardError = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new AssertFailedException($"Failed to resolve repo-local CUE CLI. {standardError}");
        }

        return standardOutput;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Workbench.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new AssertFailedException("Could not locate the repository root for CUE test setup.");
    }
}
