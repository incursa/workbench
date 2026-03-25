using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class SpecTraceLayoutTests
{
    [TestMethod]
    public void GetDefaultDomain_UsesCanonicalRootsBeforeRepositoryName()
    {
        using var repo = CreateRepoRoot("workbench-layout-domain");
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.WorkItemsRoot, "alpha-team"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.ArchitectureRoot, "beta-team"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.VerificationRoot, "gamma-team"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.RequirementsRoot, "delta-team"));

        Assert.AreEqual("ALPHA-TEAM", SpecTraceLayout.GetDefaultDomain(repo.Path));
    }

    [TestMethod]
    public void GetDefaultDomain_FallsBackToRepositoryNameWhenNoCanonicalRootsExist()
    {
        using var repo = CreateRepoRoot("workbench-layout-fallback");

        Assert.AreEqual("WORKBENCH-LAYOUT-FALLBACK", SpecTraceLayout.GetDefaultDomain(repo.Path));
    }

    [TestMethod]
    public void PathHelpersAndCanonicalChecks_CoverRootsSectionsAndReadableNames()
    {
        using var repo = CreateRepoRoot("workbench-layout-paths");
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.WorkItemsRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.ArchitectureRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.VerificationRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.RequirementsRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.GeneratedRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.TemplatesRoot, "WB"));
        Directory.CreateDirectory(Path.Combine(repo.Path, SpecTraceLayout.SchemasRoot, "WB"));

        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "requirements")), Normalize(SpecTraceLayout.GetSpecificationDirectory(repo.Path)));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "architecture", "WB")), Normalize(SpecTraceLayout.GetArchitectureDirectory(repo.Path, "wb")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "work-items", "WB")), Normalize(SpecTraceLayout.GetWorkItemDirectory(repo.Path, "wb")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "verification", "WB")), Normalize(SpecTraceLayout.GetVerificationDirectory(repo.Path, "wb")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-0001.md")), Normalize(SpecTraceLayout.GetSpecificationPath(repo.Path, "SPEC-WB-0001")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "requirements", "CLI", "SPEC-CLI-0001.md")), Normalize(SpecTraceLayout.GetSpecificationPath(repo.Path, "CLI", "SPEC-CLI-0001")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "architecture", "WB", "architecture-overview.md")), Normalize(SpecTraceLayout.GetArchitecturePath(repo.Path, "wb", "ARC-WB-0001", "Architecture Overview")));
        Assert.AreEqual(Normalize(Path.Combine(repo.Path, "specs", "work-items", "WB", "wi-wb-0001.md")), Normalize(SpecTraceLayout.GetWorkItemPath(repo.Path, "wb", "WI-WB-0001", "!!!")));

        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/requirements/WB/SPEC-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/architecture/WB/ARC-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/verification/WB/VER-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/work-items/WB/WI-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/generated/commands.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/templates/spec-template.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalPath("specs/schemas/artifact-frontmatter.schema.json"));
        Assert.IsFalse(SpecTraceLayout.IsCanonicalPath("docs/README.md"));

        Assert.IsTrue(SpecTraceLayout.IsCanonicalSpecificationPath("specs/requirements/WB/SPEC-WB-0001.md"));
        Assert.IsFalse(SpecTraceLayout.IsCanonicalSpecificationPath("specs/requirements/WB/_index.md"));
        Assert.IsFalse(SpecTraceLayout.IsCanonicalSpecificationPath("specs/requirements/WB/sub/SPEC-WB-0001.md"));

        Assert.IsTrue(SpecTraceLayout.IsCanonicalArchitecturePath("specs/architecture/WB/ARC-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalVerificationPath("specs/verification/WB/VER-WB-0001.md"));
        Assert.IsTrue(SpecTraceLayout.IsCanonicalWorkItemPath("specs/work-items/WB/WI-WB-0001.md"));

        Assert.AreEqual("requirements", SpecTraceLayout.GetCanonicalSection("specs/requirements/WB/SPEC-WB-0001.md"));
        Assert.AreEqual("architecture", SpecTraceLayout.GetCanonicalSection("specs/architecture/WB/ARC-WB-0001.md"));
        Assert.AreEqual("verification", SpecTraceLayout.GetCanonicalSection("specs/verification/WB/VER-WB-0001.md"));
        Assert.AreEqual("work-items", SpecTraceLayout.GetCanonicalSection("specs/work-items/WB/WI-WB-0001.md"));
        Assert.AreEqual("generated", SpecTraceLayout.GetCanonicalSection("specs/generated/commands.md"));
        Assert.AreEqual("templates", SpecTraceLayout.GetCanonicalSection("specs/templates/spec-template.md"));
        Assert.AreEqual("schemas", SpecTraceLayout.GetCanonicalSection("specs/schemas/artifact-frontmatter.schema.json"));
        Assert.IsNull(SpecTraceLayout.GetCanonicalSection("docs/README.md"));

        Assert.IsTrue(SpecTraceLayout.IsDirectChildPath(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "SPEC-WB-0001.md"),
            Path.Combine(repo.Path, "specs", "requirements", "WB")));
        Assert.IsFalse(SpecTraceLayout.IsDirectChildPath(
            Path.Combine(repo.Path, "specs", "requirements", "WB", "nested", "SPEC-WB-0001.md"),
            Path.Combine(repo.Path, "specs", "requirements", "WB")));
        Assert.IsFalse(SpecTraceLayout.IsDirectChildPath(
            Path.Combine(repo.Path, "specs", "architecture", "WB", "ARC-WB-0001.md"),
            Path.Combine(repo.Path, "specs", "requirements", "WB")));

        Assert.AreEqual("specs/requirements/WB/SPEC-WB-0001.md", SpecTraceLayout.NormalizePath(@"specs\requirements\WB\SPEC-WB-0001.md"));
    }

    private static TempRepoRoot CreateRepoRoot(string name)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", name);
        if (Directory.Exists(repoRoot))
        {
            Directory.Delete(repoRoot, recursive: true);
        }

        Directory.CreateDirectory(repoRoot);
        return new TempRepoRoot(repoRoot);
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, recursive: true);
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
}
