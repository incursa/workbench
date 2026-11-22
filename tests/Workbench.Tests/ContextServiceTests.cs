using System.Text.Json.Nodes;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class ContextServiceTests
{
    [TestMethod]
    public void WorkbenchConfig_LoadWithoutWorkflow_DefaultsToStandard()
    {
        using var repo = new ContextTestRepo();
        var node = JsonNode.Parse(JsonSerializer.Serialize(
            WorkbenchConfig.Default,
            WorkbenchJsonContext.Default.WorkbenchConfig))!.AsObject();
        node.Remove("Workflow");
        repo.Write(".workbench/config.json", node.ToJsonString());

        var config = WorkbenchConfig.Load(repo.Path, out var error);

        Assert.IsNull(error);
        Assert.AreEqual(WorkflowPolicyResolver.Standard, config.Workflow.Level);
    }

    [TestMethod]
    public void WorkflowPolicyResolver_RejectsUnknownLevel()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkflowPolicyResolver.Resolve(new WorkflowConfig { Level = "maximum" }, new ValidationConfig()));

        StringAssert.Contains(error.Message, "Expected light, standard, or governed", StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetContext_ExplicitItemFiltersTerminalWorkAndResolvesApplicableArtifacts()
    {
        using var repo = new ContextTestRepo();
        repo.WriteCanonicalItem("WI-WB-0001", "planned", "Add context", ["REQ-WB-CTX-0001"], ["ARC-WB-0001"]);
        repo.WriteCanonicalItem("WI-WB-0002", "complete", "Already done", [], []);
        repo.Write(
            "specs/requirements/WB/SPEC-WB-CTX.md",
            """
            ---
            artifact_id: SPEC-WB-CTX
            artifact_type: specification
            title: "Context contract"
            domain: WB
            status: draft
            owner: platform
            ---

            # SPEC-WB-CTX - Context contract

            ## REQ-WB-CTX-0001 Context output

            Workbench MUST provide context.
            """);
        repo.Write(
            "specs/architecture/WB/ARC-WB-0001-context.md",
            """
            ---
            artifact_id: ARC-WB-0001
            artifact_type: architecture
            title: "Context design"
            domain: WB
            status: draft
            owner: platform
            satisfies:
              - REQ-WB-CTX-0001
            related_artifacts:
              - SPEC-WB-CTX
            ---

            # ARC-WB-0001 - Context design
            """);

        var result = ContextService.GetContext(repo.Path, WorkbenchConfig.Default, "WI-WB-0001");

        Assert.AreEqual(1, result.SchemaVersion);
        Assert.IsNotNull(result.SelectedWorkItem);
        Assert.AreEqual("WI-WB-0001", result.SelectedWorkItem.Id);
        Assert.AreEqual("specs/work-items/WB/WI-WB-0001-add-context.md", result.SelectedWorkItem.Path);
        Assert.HasCount(1, result.ActiveWork);
        Assert.AreEqual("WI-WB-0001", result.ActiveWork[0].Id);
        Assert.IsTrue(result.ApplicableArtifacts.Any(entry =>
            string.Equals(entry.Reference, "REQ-WB-CTX-0001", StringComparison.Ordinal) &&
            string.Equals(entry.ArtifactId, "SPEC-WB-CTX", StringComparison.Ordinal) &&
            string.Equals(entry.Path, "specs/requirements/WB/SPEC-WB-CTX.md", StringComparison.Ordinal)),
            string.Join(" | ", result.ApplicableArtifacts.Select(entry => $"{entry.Reference}:{entry.ArtifactId}:{entry.Path}")));
        Assert.IsTrue(result.ApplicableArtifacts.Any(entry =>
            string.Equals(entry.Reference, "ARC-WB-0001", StringComparison.Ordinal) && entry.Resolved));
        Assert.AreEqual("workbench item status WI-WB-0001 in_progress", result.RecommendedNextAction.Command);
    }

    [TestMethod]
    public void GetContext_InfersItemFromBranchAndReturnsOneNextAction()
    {
        using var repo = new ContextTestRepo(initializeGit: true);
        repo.WriteCanonicalItem("WI-WB-0042", "in_progress", "Focused work", [], []);
        var checkout = GitService.Run(repo.Path, "checkout", "-b", "work/WI-WB-0042-focused-work");
        Assert.AreEqual(0, checkout.ExitCode, checkout.StdErr);

        var result = ContextService.GetContext(repo.Path, WorkbenchConfig.Default);

        Assert.AreEqual("work/WI-WB-0042-focused-work", result.Repository.Branch);
        Assert.IsTrue(
            string.Equals("WI-WB-0042", result.SelectedWorkItem?.Id, StringComparison.Ordinal),
            $"branch={result.Repository.Branch}; active={string.Join(',', result.ActiveWork.Select(item => item.Id))}");
        Assert.AreEqual("workbench validate --profile core", result.RecommendedNextAction.Command);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.RecommendedNextAction.Reason));
    }

    [TestMethod]
    public void GetContext_LightPolicyWithoutWorkRecommendsCoreValidation()
    {
        using var repo = new ContextTestRepo();
        var config = WorkbenchConfig.Default with
        {
            Workflow = new WorkflowConfig { Level = WorkflowPolicyResolver.Light }
        };

        var result = ContextService.GetContext(repo.Path, config);

        Assert.AreEqual(WorkflowPolicyResolver.Light, result.Policy.Level);
        Assert.IsFalse(result.Policy.WorkItemRequired);
        Assert.AreEqual("workbench validate --profile core", result.RecommendedNextAction.Command);
        Assert.HasCount(4, result.Constraints);
    }

    [TestMethod]
    public void GetContext_DoesNotResolveRequirementPrefixesOrPathsOutsideRepo()
    {
        using var repo = new ContextTestRepo();
        var outsidePath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(repo.Path)!,
            $"outside-{Guid.NewGuid():N}.md");
        File.WriteAllText(outsidePath, "outside");
        try
        {
            repo.WriteCanonicalItem(
                "WI-WB-0003",
                "in_progress",
                "Strict context resolution",
                ["REQ-WB-CTX-0001"],
                []);
            repo.Write(
                "specs/requirements/WB/SPEC-WB-CTX.md",
                """
                ---
                artifact_id: SPEC-WB-CTX
                artifact_type: specification
                title: "Context contract"
                domain: WB
                status: draft
                owner: platform
                ---

                ## REQ-WB-CTX-00010 Different requirement
                """);
            var itemPath = Directory.EnumerateFiles(
                System.IO.Path.Combine(repo.Path, "specs", "work-items", "WB"),
                "WI-WB-0003-*.md").Single();
            var content = File.ReadAllText(itemPath).Replace(
                "related_artifacts: []",
                $"related_artifacts:\n  - ../{System.IO.Path.GetFileName(outsidePath)}",
                StringComparison.Ordinal);
            File.WriteAllText(itemPath, content);

            var result = ContextService.GetContext(repo.Path, WorkbenchConfig.Default, "WI-WB-0003");

            Assert.IsTrue(result.ApplicableArtifacts.All(entry => !entry.Resolved));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    private sealed class ContextTestRepo : IDisposable
    {
        public ContextTestRepo(bool initializeGit = false)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-context-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            if (initializeGit)
            {
                var init = GitService.Run(Path, "init");
                if (init.ExitCode != 0)
                {
                    throw new InvalidOperationException(init.StdErr);
                }
            }
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? Path);
            File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal));
        }

        public void WriteCanonicalItem(
            string id,
            string status,
            string title,
            IReadOnlyList<string> addresses,
            IReadOnlyList<string> designLinks)
        {
            var slug = WorkItemService.Slugify(title);
            Write(
                $"specs/work-items/WB/{id}-{slug}.md",
                $"""
                ---
                artifact_id: {id}
                artifact_type: work_item
                title: "{title}"
                domain: WB
                status: {status}
                owner: platform
                {FormatField("addresses", addresses)}
                {FormatField("design_links", designLinks)}
                verification_links: []
                related_artifacts: []
                ---

                # {id} - {title}

                ## Summary

                Test work item.
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private static string FormatField(string name, IReadOnlyList<string> values)
        {
            return values.Count == 0
                ? $"{name}: []"
                : $"{name}:\n{string.Join("\n", values.Select(value => $"  - {value}"))}";
        }
    }
}
