// PR command helpers for the CLI.
// Responsibilities: build PR title/body, call GitHub provider, and backlink the PR URL into work items.
// Assumes repoRoot is valid and config already loaded by callers.
using System.IO;
using System.Threading.Tasks;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static async Task<string> CreatePrAsync(
        string repoRoot,
        WorkbenchConfig config,
        WorkItem item,
        string? baseBranch,
        bool draft,
        bool fill)
    {
        var prTitle = $"{item.Id}: {item.Title}";
        var prBody = fill ? PullRequestBuilder.BuildBody(item) : $"Work item: /{Path.GetRelativePath(repoRoot, item.Path).Replace('\\', '/')}";
        var isDraft = draft || config.Github.DefaultDraft;
        var prRepo = GithubService.ResolveRepo(repoRoot, config);
        var prUrl = await GithubService.CreatePullRequestAsync(repoRoot, config, prRepo, prTitle, prBody, baseBranch ?? config.Git.DefaultBaseBranch, isDraft).ConfigureAwait(false);
        WorkItemService.AddPrLink(item.Path, prUrl);
        return prUrl;
    }

    static async Task HandlePrCreateAsync(
        string? repo,
        string format,
        string id,
        string? baseBranch,
        bool draft,
        bool fill,
        string? deprecatedMessage)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(deprecatedMessage))
            {
                Console.WriteLine(deprecatedMessage);
            }

            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
                SetExitCode(2);
                return;
            }
            var path = WorkItemService.GetItemPathById(repoRoot, config, id);
            var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Invalid work item.");
            var prUrl = await CreatePrAsync(repoRoot, config, item, baseBranch, draft, fill).ConfigureAwait(false);

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new PrOutput(
                    true,
                    new PrData(prUrl, item.Id));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.PrOutput);
            }
            else
            {
                Console.WriteLine(prUrl);
            }
            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }
}
