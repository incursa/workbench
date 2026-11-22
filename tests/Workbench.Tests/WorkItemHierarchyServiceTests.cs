using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class WorkItemHierarchyServiceTests
{
    [TestMethod]
    public void Set_RoundTripsOptionalRelationshipsAndDerivesLocalChildren()
    {
        using var repo = new TempRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var parent = Create(repo.Path, config, "Parent initiative");
        var child = Create(repo.Path, config, "Child work item");
        var remote = "workbench://github.com/incursa/portfolio/WI-PORTFOLIO-0042";

        var before = WorkItemService.LoadItem(child.Path);
        Assert.IsNotNull(before);
        Assert.IsNull(before.ParentRef);
        Assert.IsEmpty(before.InitiativeRefs);

        var result = WorkItemHierarchyService.Set(
            repo.Path,
            config,
            child.Id,
            updateParent: true,
            parent.Id,
            updateInitiatives: true,
            [parent.Id, remote]);

        Assert.AreEqual(WorkItemHierarchyService.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(parent.Id, result.ParentRef);
        CollectionAssert.AreEqual(new[] { parent.Id, remote }, result.InitiativeRefs.ToArray());
        var reloaded = WorkItemService.LoadItem(child.Path);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(before.Status, reloaded.Status);
        Assert.AreEqual(parent.Id, reloaded.ParentRef);
        CollectionAssert.AreEqual(new[] { parent.Id, remote }, reloaded.InitiativeRefs.ToArray());

        var parentView = WorkItemHierarchyService.Show(repo.Path, config, parent.Id);
        CollectionAssert.AreEqual(new[] { child.Id }, parentView.ChildRefs.ToArray());

        var stableContent = File.ReadAllBytes(child.Path);
        _ = WorkItemHierarchyService.Set(
            repo.Path,
            config,
            child.Id,
            updateParent: true,
            parent.Id,
            updateInitiatives: true,
            [parent.Id, remote]);
        CollectionAssert.AreEqual(stableContent, File.ReadAllBytes(child.Path));
    }

    [TestMethod]
    public void Set_RejectsSelfReferencesCyclesDuplicatesAndMissingLocalItemsWithoutWriting()
    {
        using var repo = new TempRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var first = Create(repo.Path, config, "First");
        var second = Create(repo.Path, config, "Second");
        _ = WorkItemHierarchyService.Set(
            repo.Path,
            config,
            second.Id,
            updateParent: true,
            first.Id,
            updateInitiatives: false,
            []);
        var original = File.ReadAllBytes(first.Path);

        Assert.Throws<InvalidOperationException>(() => WorkItemHierarchyService.Set(
            repo.Path,
            config,
            first.Id,
            updateParent: true,
            second.Id,
            updateInitiatives: false,
            []));
        Assert.Throws<InvalidOperationException>(() => WorkItemHierarchyService.Set(
            repo.Path,
            config,
            first.Id,
            updateParent: false,
            null,
            updateInitiatives: true,
            [first.Id]));
        Assert.Throws<InvalidOperationException>(() => WorkItemHierarchyService.Set(
            repo.Path,
            config,
            first.Id,
            updateParent: false,
            null,
            updateInitiatives: true,
            [second.Id, second.Id]));
        Assert.Throws<FileNotFoundException>(() => WorkItemHierarchyService.Set(
            repo.Path,
            config,
            first.Id,
            updateParent: true,
            "WI-WB-9999",
            updateInitiatives: false,
            []));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(first.Path));
    }

    [TestMethod]
    public void NormalizeReference_ValidatesCrossRepositoryUrisOffline()
    {
        Assert.AreEqual(
            "workbench://github.com/Incursa/Portfolio/WI-PORTFOLIO-0042",
            WorkItemHierarchyService.NormalizeReference(
                "workbench://GITHUB.COM/Incursa/Portfolio/WI-PORTFOLIO-0042"));
        Assert.Throws<InvalidOperationException>(() =>
            WorkItemHierarchyService.NormalizeReference("https://github.com/incursa/workbench/WI-WB-0001"));
        Assert.Throws<InvalidOperationException>(() =>
            WorkItemHierarchyService.NormalizeReference("workbench://github.com/incursa/workbench/REQ-WB-0001"));
    }

    [TestMethod]
    public void RepositoryValidation_RejectsManuallyAuthoredLocalCycles()
    {
        using var repo = new TempRepoRoot();
        ScaffoldService.Scaffold(repo.Path, force: false);
        var config = WorkbenchConfig.Load(repo.Path, out var configError);
        Assert.IsNull(configError);
        var first = Create(repo.Path, config, "First");
        var second = Create(repo.Path, config, "Second");
        _ = WorkItemHierarchyService.Set(
            repo.Path,
            config,
            second.Id,
            updateParent: true,
            first.Id,
            updateInitiatives: false,
            []);
        Assert.IsTrue(FrontMatter.TryParse(File.ReadAllText(first.Path), out var frontMatter, out var error), error);
        frontMatter!.Data["parent_ref"] = second.Id;
        File.WriteAllText(first.Path, frontMatter.Serialize());

        var result = ValidationService.ValidateRepo(repo.Path, config);

        Assert.IsTrue(result.Errors.Any(message => message.Contains("local hierarchy cycle", StringComparison.Ordinal)));
    }

    private static WorkItemService.WorkItemResult Create(
        string repoRoot,
        WorkbenchConfig config,
        string title)
    {
        return WorkItemService.CreateItem(
            repoRoot,
            config,
            "work_item",
            title,
            "in_progress",
            "high",
            "platform");
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot()
        {
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "workbench-hierarchy-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
