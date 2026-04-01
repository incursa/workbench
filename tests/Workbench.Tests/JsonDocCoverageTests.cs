using Workbench;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class JsonDocCoverageTests
{
    [TestMethod]
    public void WorkspaceAndDocService_HandleJsonCanonicalSpecificationArtifacts()
    {
        using var repo = new TempJsonDocRepo();
        ScaffoldService.Scaffold(repo.Path, force: true);
        repo.WriteSpecification();

        var workItem = WorkItemService.CreateItem(
            repo.Path,
            WorkbenchConfig.Default,
            "work_item",
            "Json doc coverage item",
            "planned",
            null,
            null);

        var workspace = new WorkbenchWorkspace(repo.Path, WorkbenchConfig.Default);
        var listed = workspace.ListDocs(typeFilter: null, query: "SPEC-WB-JSONDOC");
        Assert.IsTrue(listed.Any(doc => string.Equals(doc.ArtifactId, "SPEC-WB-JSONDOC", StringComparison.Ordinal)));

        var detail = workspace.GetDoc("SPEC-WB-JSONDOC");
        Assert.IsNotNull(detail);
        Assert.AreEqual("specification", detail.Summary.Type);
        Assert.AreEqual("Json doc coverage", detail.Summary.Title);
        StringAssert.Contains(detail.Body, "\"artifact_type\": \"specification\"", StringComparison.Ordinal);

        var resolved = DocService.TryResolveDocPathByArtifactId(
            repo.Path,
            WorkbenchConfig.Default,
            "SPEC-WB-JSONDOC",
            out var resolvedPath);
        Assert.IsTrue(resolved);
        Assert.IsTrue(
            resolvedPath.Replace('\\', '/').EndsWith("/specs/requirements/WB/SPEC-WB-JSONDOC.json", StringComparison.OrdinalIgnoreCase),
            resolvedPath);

        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, resolvedPath, workItem.Id, add: true));

        var afterAdd = workspace.GetDoc("SPEC-WB-JSONDOC");
        Assert.IsNotNull(afterAdd);
        Assert.IsTrue(afterAdd.Summary.WorkItems.Contains(workItem.Id, StringComparer.OrdinalIgnoreCase));

        Assert.IsTrue(DocService.TryUpdateDocWorkItemLink(repo.Path, WorkbenchConfig.Default, resolvedPath, workItem.Id, add: false));

        var afterRemove = workspace.GetDoc("SPEC-WB-JSONDOC");
        Assert.IsNotNull(afterRemove);
        Assert.IsFalse(afterRemove.Summary.WorkItems.Contains(workItem.Id, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class TempJsonDocRepo : IDisposable
    {
        public TempJsonDocRepo()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-json-doc-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "specs", "requirements", "WB"));
        }

        public string Path { get; }

        public void WriteSpecification()
        {
            File.WriteAllText(
                System.IO.Path.Combine(Path, "specs", "requirements", "WB", "SPEC-WB-JSONDOC.json"),
                """
                {
                  "$schema": "https://github.com/incursa/spec-trace/raw/refs/heads/main/model/model.schema.json",
                  "artifact_id": "SPEC-WB-JSONDOC",
                  "artifact_type": "specification",
                  "title": "Json doc coverage",
                  "domain": "wb",
                  "capability": "json-docs",
                  "status": "draft",
                  "owner": "platform",
                  "purpose": "Exercise Workbench JSON doc browsing and link mutation.",
                  "scope": "Exercise a minimal canonical JSON specification.",
                  "context": "Workbench should treat canonical JSON as the authoritative document surface.",
                  "requirements": [
                    {
                      "id": "REQ-WB-JSONDOC-0001",
                      "title": "Load JSON spec docs",
                      "statement": "The tool MUST load canonical JSON specification artifacts."
                    }
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
                    Directory.Delete(Path, recursive: true);
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
