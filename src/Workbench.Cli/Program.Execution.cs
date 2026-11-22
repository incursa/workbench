using System.CommandLine;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static Command BuildExecutionCommand(Option<string?> repoOption, Option<string> formatOption)
    {
        var executionCommand = new Command(
            "execution",
            "Observe external PR, release, and deployment state without mutating the provider or canonical work items.");

        var showCommand = new Command("show", "Read the latest derived execution-state snapshot without network access.");
        var showWorkItemOption = CreateWorkItemOption();
        showCommand.Options.Add(showWorkItemOption);
        showCommand.SetAction(parseResult =>
        {
            try
            {
                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var workItemId = parseResult.GetValue(showWorkItemOption) ?? string.Empty;
                var data = ExecutionStateService.Show(repoRoot, workItemId)
                    ?? throw new InvalidOperationException($"No execution-state snapshot exists for '{workItemId}'. Run `workbench execution refresh` explicitly.");
                WriteExecutionState(data, format);
                SetExitCode(data.Snapshot.Diagnostics.Count > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        executionCommand.Subcommands.Add(showCommand);

        var refreshCommand = new Command(
            "refresh",
            "Explicitly query read-only execution providers and write a derived local snapshot.");
        var refreshWorkItemOption = CreateWorkItemOption();
        var providerOption = new Option<string>("--provider")
        {
            Description = "Read-only execution provider.",
            DefaultValueFactory = _ => "github"
        };
        providerOption.CompletionSources.Add("github");
        var pullRequestOption = new Option<string[]>("--pr")
        {
            Description = "Pull request URL, owner/repo#number, or number. Repeat as needed."
        };
        var releaseOption = new Option<string[]>("--release")
        {
            Description = "Release URL, owner/repo@tag, or tag. Repeat as needed."
        };
        var deploymentOption = new Option<string[]>("--deployment")
        {
            Description = "Deployment API URL, owner/repo#ID, or ID. Repeat as needed."
        };
        refreshCommand.Options.Add(refreshWorkItemOption);
        refreshCommand.Options.Add(providerOption);
        refreshCommand.Options.Add(pullRequestOption);
        refreshCommand.Options.Add(releaseOption);
        refreshCommand.Options.Add(deploymentOption);
        refreshCommand.SetAction(async parseResult =>
        {
            try
            {
                var repoRoot = ResolveRepo(parseResult.GetValue(repoOption));
                var format = ResolveFormat(parseResult.GetValue(formatOption) ?? "table");
                var workItemId = parseResult.GetValue(refreshWorkItemOption) ?? string.Empty;
                var providerName = parseResult.GetValue(providerOption) ?? "github";
                if (!string.Equals(providerName, "github", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unsupported execution provider '{providerName}'.");
                }

                var config = WorkbenchConfig.Load(repoRoot, out var configError);
                if (configError is not null)
                {
                    throw new InvalidOperationException($"Config error: {configError}");
                }

                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
                var item = WorkItemService.LoadItem(itemPath)
                    ?? throw new InvalidOperationException($"Work item '{workItemId}' could not be loaded.");
                var defaultRepo = TryResolveExecutionRepo(repoRoot, config);
                var pullRequests = (parseResult.GetValue(pullRequestOption) ?? [])
                    .Concat(item.Related.Prs)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var references = pullRequests
                    .Select(value => ExecutionStateReferenceParser.ParsePullRequest(value, defaultRepo))
                    .Concat((parseResult.GetValue(releaseOption) ?? [])
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => ExecutionStateReferenceParser.ParseRelease(value, defaultRepo)))
                    .Concat((parseResult.GetValue(deploymentOption) ?? [])
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => ExecutionStateReferenceParser.ParseDeployment(value, defaultRepo)))
                    .ToList();
                EnsureQualifiedReferences(references);

                var service = new ExecutionStateService(new GithubExecutionStateProvider());
                var data = await service.RefreshAsync(repoRoot, workItemId, references).ConfigureAwait(false);
                WriteExecutionState(data, format);
                SetExitCode(data.Snapshot.Diagnostics.Count > 0 ? 1 : 0);
            }
            catch (Exception ex)
            {
                ReportError(ex);
                SetExitCode(2);
            }
        });
        executionCommand.Subcommands.Add(refreshCommand);

        return executionCommand;
    }

    private static Option<string> CreateWorkItemOption()
    {
        return new Option<string>("--work-item")
        {
            Description = "Canonical work item ID used to group the derived snapshot.",
            Required = true
        };
    }

    private static GithubRepoRef TryResolveExecutionRepo(string repoRoot, WorkbenchConfig config)
    {
        try
        {
            return GithubService.ResolveRepo(repoRoot, config);
        }
        catch (InvalidOperationException)
        {
            return new GithubRepoRef(
                string.IsNullOrWhiteSpace(config.Github.Host) ? "github.com" : config.Github.Host,
                config.Github.Owner ?? string.Empty,
                config.Github.Repository ?? string.Empty);
        }
    }

    private static void EnsureQualifiedReferences(IEnumerable<ExecutionStateReference> references)
    {
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference.Repository.Owner) ||
                string.IsNullOrWhiteSpace(reference.Repository.Repo))
            {
                throw new InvalidOperationException(
                    $"Reference '{reference.Identifier}' does not identify a repository. Use a full URL or owner/repository syntax.");
            }
        }
    }

    private static void WriteExecutionState(ExecutionStateData data, string format)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(
                new ExecutionStateOutput(data.Snapshot.Diagnostics.Count == 0, data),
                WorkbenchJsonContext.Default.ExecutionStateOutput);
            return;
        }

        Console.WriteLine($"Execution snapshot: {data.Path}");
        Console.WriteLine($"Work item: {data.Snapshot.WorkItemId}");
        foreach (var observation in data.Snapshot.Observations)
        {
            Console.WriteLine($"- {observation.Kind} {observation.ExternalReference}: {observation.NormalizedState} ({observation.NativeState})");
        }

        foreach (var diagnostic in data.Snapshot.Diagnostics)
        {
            var retained = diagnostic.RetainedObservation
                ? $" Previous observation retained ({diagnostic.PreviousObservationAgeSeconds ?? 0} seconds old)."
                : string.Empty;
            Console.WriteLine($"- warning: {diagnostic.Message}{retained}");
        }
    }
}
