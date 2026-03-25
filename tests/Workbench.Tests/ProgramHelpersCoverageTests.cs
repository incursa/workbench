using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Workbench.Cli;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ProgramHelpersCoverageTests
{
    [TestMethod]
    public void NormalizeGlobalOptions_ReordersKnownGlobals_AndLeavesNoGlobalArgsUntouched()
    {
        var noGlobals = Array.Empty<string>();
        var untouched = InvokePrivate<string[]>("NormalizeGlobalOptions", new object?[] { noGlobals });
        Assert.AreSame(noGlobals, untouched);

        var reordered = InvokePrivate<string[]>(
            "NormalizeGlobalOptions",
            new object?[]
            {
                new List<string>
                {
                    "item",
                    "new",
                    "--repo",
                    "C:/src/incursa/workbench",
                    "--format=json",
                    "--no-color",
                    "--quiet",
                    "--debug",
                    "--repo",
                    "--format"
                }.ToArray()
            });

        CollectionAssert.AreEqual(
            new List<string>
            {
                "--repo",
                "C:/src/incursa/workbench",
                "--format",
                "json",
                "--no-color",
                "--quiet",
                "--debug",
                "item",
                "new",
                "--repo",
                "--format"
            },
            reordered);
    }

    [TestMethod]
    public void ParseAndTruthHelpers_HandleExpectedForms()
    {
        Assert.IsNull(InvokePrivate<string?>("ResolveFormatFromArgs", new object?[] { Array.Empty<string>() }));
        Assert.IsNull(InvokePrivate<string?>("ResolveFormatFromArgs", new object?[] { new List<string> { "--format" }.ToArray() }));
        Assert.AreEqual("json", InvokePrivate<string?>("ResolveFormatFromArgs", new object?[] { new List<string> { "--format", "json" }.ToArray() }));
        Assert.AreEqual("table", InvokePrivate<string?>("ResolveFormatFromArgs", new object?[] { new List<string> { "--format=table" }.ToArray() }));

        Assert.IsFalse(InvokePrivate<bool>("ResolveDebugFromArgs", new object?[] { Array.Empty<string>() }));
        Assert.IsTrue(InvokePrivate<bool>("ResolveDebugFromArgs", new object?[] { new List<string> { "--debug" }.ToArray() }));

        Assert.IsTrue(InvokePrivate<bool>("IsTruthy", "1"));
        Assert.IsTrue(InvokePrivate<bool>("IsTruthy", " yes "));
        Assert.IsTrue(InvokePrivate<bool>("IsTruthy", "ON"));
        Assert.IsFalse(InvokePrivate<bool>("IsTruthy", "off"));
        Assert.IsFalse(InvokePrivate<bool>("IsTruthy", "   "));
    }

    [TestMethod]
    public void ErrorAndIssueHelpers_MapKnownFailures_AndResolveStatuses()
    {
        SetPrivateField("runtimeDebugEnabled", false);

        var repoError = InvokePrivate<CliErrorData>("MapCliError", new InvalidOperationException("Not a git repository."));
        Assert.AreEqual("repo_not_git", repoError.Code);
        StringAssert.Contains(repoError.Message, "not inside a git repository", StringComparison.OrdinalIgnoreCase);

        var accessError = InvokePrivate<CliErrorData>("MapCliError", new UnauthorizedAccessException("denied"));
        Assert.AreEqual("access_denied", accessError.Code);

        var missingError = InvokePrivate<CliErrorData>("MapCliError", new FileNotFoundException("missing"));
        Assert.AreEqual("path_not_found", missingError.Code);

        var genericError = InvokePrivate<CliErrorData>("MapCliError", new Exception("boom"));
        Assert.AreEqual("unexpected_error", genericError.Code);
        Assert.IsNotNull(genericError.Hint);

        var repo = new GithubRepoRef("github.com", "octo", "demo");
        var openIssue = new GithubIssue(repo, 1, "Open", "Body", "https://github.com/octo/demo/issues/1", "open", new List<string>(), new List<string>());
        var closedIssue = openIssue with { State = "closed" };

        Assert.AreEqual("work_item", InvokePrivate<string>("ResolveIssueType", openIssue, null));
        Assert.AreEqual("work_item", InvokePrivate<string>("ResolveIssueType", openIssue, "specification"));

        Assert.AreEqual("planned", InvokePrivate<string>("ResolveIssueStatus", openIssue, null));
        Assert.AreEqual("complete", InvokePrivate<string>("ResolveIssueStatus", closedIssue, null));

        foreach (var (input, expected) in new List<(string Input, string Expected)>
        {
            ("planned", "planned"),
            ("in-progress", "in_progress"),
            ("in_progress", "in_progress"),
            ("blocked", "blocked"),
            ("complete", "complete"),
            ("cancelled", "cancelled"),
            ("superseded", "superseded")
        })
        {
            Assert.AreEqual(expected, InvokePrivate<string>("ResolveIssueStatus", openIssue, input));
        }

        ExpectException<InvalidOperationException>(
            () => InvokePrivate<string>("ResolveIssueStatus", openIssue, "deferred"));

        Assert.AreEqual("local", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("local"), null));
        Assert.AreEqual("github", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("github"), null));
        Assert.AreEqual("fail", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("fail"), null));
        Assert.AreEqual("fail", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("other"), null));
        Assert.AreEqual("local", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("fail"), " local "));
        Assert.AreEqual("github", InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("fail"), "github"));
        ExpectException<InvalidOperationException>(
            () => InvokePrivate<string>("ResolvePreferredSyncSource", CreateConfig("fail"), "later"));

        Assert.IsTrue(InvokePrivate<bool>("IsTerminalStatus", "complete"));
        Assert.IsTrue(InvokePrivate<bool>("IsTerminalStatus", "cancelled"));
        Assert.IsTrue(InvokePrivate<bool>("IsTerminalStatus", "superseded"));
        Assert.IsFalse(InvokePrivate<bool>("IsTerminalStatus", "in_progress"));

        Assert.IsTrue(InvokeTryResolveDocLinkType("spec").Ok);
        Assert.AreEqual("spec", InvokeTryResolveDocLinkType("spec").Resolved);
        Assert.IsTrue(InvokeTryResolveDocLinkType("specification").Ok);
        Assert.AreEqual("specification", InvokeTryResolveDocLinkType("specification").Resolved);
        Assert.IsTrue(InvokeTryResolveDocLinkType("architecture").Ok);
        Assert.IsTrue(InvokeTryResolveDocLinkType("verification").Ok);
        Assert.IsTrue(InvokeTryResolveDocLinkType("doc").Ok);
        Assert.IsTrue(InvokeTryResolveDocLinkType("work_item").Ok);
        Assert.IsFalse(InvokeTryResolveDocLinkType(null).Ok);
        Assert.AreEqual(string.Empty, InvokeTryResolveDocLinkType(null).Resolved);
        Assert.IsFalse(InvokeTryResolveDocLinkType("work-item").Ok);
        Assert.AreEqual("work-item", InvokeTryResolveDocLinkType("work-item").Resolved);

        Assert.IsTrue(InvokePrivate<bool>("StringsEqual", "  hello ", "hello"));
        Assert.IsFalse(InvokePrivate<bool>("StringsEqual", "hello", "world"));
    }

    [TestMethod]
    public void RepoAndInputHelpers_HandleRepoScopedPathsAndFileBackedContent()
    {
        using var repo = CreateRepoRoot();
        var envName = $"WORKBENCH_HELPERS_TEST_{Guid.NewGuid():N}";

        try
        {
            File.WriteAllText(
                Path.Combine(repo.Path, ".env"),
                $"""
                export {envName}=from-env
                """);
            Directory.CreateDirectory(Path.Combine(repo.Path, ".workbench"));
            File.WriteAllText(
                Path.Combine(repo.Path, ".workbench", "credentials.env"),
                $"""
                {envName}=from-credentials
                """);

            var nestedRepoPath = Path.Combine(repo.Path, "nested");
            Directory.CreateDirectory(nestedRepoPath);
            var resolvedRepo = InvokePrivate<string>("ResolveRepo", nestedRepoPath);
            Assert.AreEqual(Path.GetFullPath(repo.Path), Path.GetFullPath(resolvedRepo));
            Assert.AreEqual("from-credentials", Environment.GetEnvironmentVariable(envName));

            var noGitPath = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(noGitPath);
            try
            {
                ExpectException<InvalidOperationException>(() => InvokePrivate<string>("ResolveRepo", noGitPath));
            }
            finally
            {
                if (Directory.Exists(noGitPath))
                {
                    Directory.Delete(noGitPath, recursive: true);
                }
            }

            Assert.IsTrue(InvokePrivate<bool>("IsFirstRun", repo.Path));
            File.WriteAllText(Path.Combine(repo.Path, ".workbench", "config.json"), "{}");
            Assert.IsFalse(InvokePrivate<bool>("IsFirstRun", repo.Path));

            var insidePath = Path.Combine(repo.Path, "specs", "work-items", "WB", "item.md");
            var outsidePath = Path.Combine(Path.GetTempPath(), "elsewhere", "item.md");

            Assert.IsTrue(InvokePrivate<bool>("IsPathInsideRepo", repo.Path, insidePath));
            Assert.IsFalse(InvokePrivate<bool>("IsPathInsideRepo", repo.Path, outsidePath));

            Assert.AreEqual(
                "specs/work-items/WB/item.md",
                InvokePrivate<string>("NormalizeRepoPath", repo.Path, insidePath));
            Assert.AreEqual(
                Path.GetFullPath(outsidePath).Replace('\\', '/'),
                InvokePrivate<string>("NormalizeRepoPath", repo.Path, outsidePath));

            Assert.AreEqual(
                "/specs/work-items/WB/item.md",
                InvokePrivate<string>("NormalizeRepoLink", repo.Path, insidePath));
            Assert.AreEqual(
                "docs/spec.md",
                InvokePrivate<string>("NormalizeRepoLink", repo.Path, "./docs/spec.md"));
            Assert.AreEqual(
                Path.GetFullPath(outsidePath).Replace('\\', '/'),
                InvokePrivate<string>("NormalizeRepoLink", repo.Path, outsidePath));

            Assert.AreEqual(
                Path.Combine(repo.Path, "notes.txt"),
                InvokePrivate<string>("ResolveCommandInputPath", repo.Path, "notes.txt"));
            Assert.AreEqual(
                outsidePath,
                InvokePrivate<string>("ResolveCommandInputPath", repo.Path, outsidePath));

            var notesPath = Path.Combine(repo.Path, "notes.txt");
            File.WriteAllText(notesPath, "File-backed content");
            Assert.AreEqual("inline content", InvokePrivate<string?>("ReadOptionalInputText", repo.Path, "inline content", notesPath));
            Assert.AreEqual("File-backed content", InvokePrivate<string?>("ReadOptionalInputText", repo.Path, null, notesPath));
            Assert.IsNull(InvokePrivate<string?>("ReadOptionalInputText", repo.Path, null, null));

            var acceptancePath = Path.Combine(repo.Path, "acceptance.txt");
            File.WriteAllText(
                acceptancePath,
                """
                - first criterion
                second criterion

                - third criterion
                """);

            CollectionAssert.AreEqual(
                new List<string> { "inline one", "inline two" },
                InvokePrivate<IReadOnlyList<string>>(
                    "ResolveAcceptanceCriteriaInput",
                    repo.Path,
                    new List<string> { " inline one ", "", "inline two" },
                    null).ToArray());

            CollectionAssert.AreEqual(
                new List<string> { "first criterion", "second criterion", "third criterion" },
                InvokePrivate<IReadOnlyList<string>>(
                    "ResolveAcceptanceCriteriaInput",
                    repo.Path,
                    Array.Empty<string>(),
                    acceptancePath).ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [TestMethod]
    public void TextAndPayloadHelpers_CoverSectionExtractionPayloadAndCleanup()
    {
        var body = """
            Intro paragraph.

            ## Summary
            Summary text.

            ## Details
            Detail one.
            Detail two.
            """;

        Assert.AreEqual("Summary text.", InvokePrivate<string>("ExtractSection", body, "Summary"));
        Assert.AreEqual("Detail one.\nDetail two.", InvokePrivate<string>("ExtractSection", body, "Details"));
        Assert.AreEqual(string.Empty, InvokePrivate<string>("ExtractSection", body, "Missing"));

        var item = new WorkItem(
            "WI-WB-0001",
            "work_item",
            "planned",
            "Payload item",
            "high",
            "platform",
            "2026-03-24",
            "2026-03-25",
            new List<string> { "tag-a" },
            new RelatedLinks(
                new List<string> { "specs/alpha.md" },
                new List<string> { "src/Workbench.Core/WorkItemService.cs" },
                new List<string> { "https://github.com/incursa/workbench/pull/1" },
                new List<string> { "https://github.com/incursa/workbench/issues/2" },
                new List<string> { "work/wi-wb-0001" }),
            "payload-item",
            "/tmp/payload-item.md",
            "Item body");

        var payloadWithoutBody = InvokePrivate<WorkItemPayload>("ItemToPayload", item, false);
        var payloadWithBody = InvokePrivate<WorkItemPayload>("ItemToPayload", item, true);
        Assert.IsNull(payloadWithoutBody.Body);
        Assert.AreEqual("Item body", payloadWithBody.Body);

        Assert.AreEqual("work/WI-WB-0001/payload-item/Payload item", InvokePrivate<string>("ApplyPattern", "work/{id}/{slug}/{title}", item));

        using var temp = new TempDirectory();
        var existing = Path.Combine(temp.Path, "existing.txt");
        File.WriteAllText(existing, "temp");
        var missing = Path.Combine(temp.Path, "missing.txt");

        InvokePrivate("CleanupTempFiles", new List<string> { existing, missing });

        Assert.IsFalse(File.Exists(existing));
        Assert.IsFalse(File.Exists(missing));
    }

    private static T InvokePrivate<T>(string methodName, params object?[] args)
    {
        return (T)InvokePrivate(methodName, args)!;
    }

    private static object? InvokePrivate(string methodName, params object?[] args)
    {
        var method = typeof(global::Workbench.Cli.Program).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(global::Workbench.Cli.Program).FullName, methodName);

        try
        {
            return method.Invoke(null, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw new UnreachableException();
        }
    }

    private static (bool Ok, string Resolved) InvokeTryResolveDocLinkType(string? type)
    {
        var args = new object?[] { type, null };
        var result = (bool)InvokePrivate("TryResolveDocLinkType", args)!;
        return (result, (string)args[1]!);
    }

    private static void ExpectException<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void SetPrivateField<T>(string fieldName, T value)
    {
        var field = typeof(global::Workbench.Cli.Program).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(global::Workbench.Cli.Program).FullName, fieldName);
        field.SetValue(null, value);
    }

    private static WorkbenchConfig CreateConfig(string conflictDefault)
    {
        return WorkbenchConfig.Default with
        {
            Github = WorkbenchConfig.Default.Github with
            {
                Sync = new GithubSyncConfig
                {
                    ConflictDefault = conflictDefault
                }
            }
        };
    }

    private static TempRepoRoot CreateRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        return new TempRepoRoot(repoRoot);
    }

    private sealed class TempRepoRoot : IDisposable
    {
        public TempRepoRoot(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
