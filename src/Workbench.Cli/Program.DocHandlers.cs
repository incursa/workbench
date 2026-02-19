// Shared doc command handlers for CLI entrypoints and deprecated aliases.
// Keeps doc link logic consistent across commands and output formats.
using System.Collections.Generic;
using System.Linq;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static void HandleDocCreate(
        string? repo,
        string format,
        string type,
        string title,
        string? path,
        string[] workItems,
        string[] codeRefs,
        bool force)
    {
        try
        {
            var repoRoot = ResolveRepo(repo);
            var resolvedFormat = ResolveFormat(format);
            var config = WorkbenchConfig.Load(repoRoot, out var configError);
            if (configError is not null)
            {
                Console.WriteLine($"Config error: {configError}");
                SetExitCode(2);
                return;
            }

            var result = DocService.CreateDoc(
                repoRoot,
                config,
                type,
                title,
                path,
                workItems.ToList(),
                codeRefs.ToList(),
                force);

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocCreateOutput(
                    true,
                    new DocCreateData(result.Path, result.Type, result.WorkItems));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocCreateOutput);
            }
            else
            {
                Console.WriteLine($"Doc created at {result.Path}");
            }
            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static void HandleDocLink(
        string? repo,
        string format,
        string docType,
        string docPath,
        string[] workItems,
        bool add,
        bool dryRun)
    {
        try
        {
            if (workItems.Length == 0)
            {
                Console.WriteLine("No work items provided.");
                SetExitCode(2);
                return;
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

            var normalizedDoc = NormalizeRepoLink(repoRoot, docPath);
            var itemsUpdated = 0;
            var docUpdated = false;

            foreach (var workItemId in workItems)
            {
                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
                // Only ADR/spec link types are supported; normalize to the front matter key.
                var key = docType.Equals("adr", StringComparison.OrdinalIgnoreCase) ? "adrs" : "specs";
                var updated = add
                    ? WorkItemService.AddRelatedLink(itemPath, key, normalizedDoc, apply: !dryRun)
                    : WorkItemService.RemoveRelatedLink(itemPath, key, normalizedDoc, apply: !dryRun);
                if (updated)
                {
                    itemsUpdated++;
                }

                if (DocService.TryUpdateDocWorkItemLink(repoRoot, config, normalizedDoc, workItemId, add, apply: !dryRun))
                {
                    docUpdated = true;
                }
            }

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocLinkOutput(
                    true,
                    new DocLinkData(
                        normalizedDoc,
                        docType,
                        workItems.ToList(),
                        itemsUpdated,
                        docUpdated));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocLinkOutput);
            }
            else
            {
                var action = add ? "linked" : "unlinked";
                Console.WriteLine($"{docType.ToUpperInvariant()} {action}: {normalizedDoc}");
                Console.WriteLine($"Work items updated: {itemsUpdated}");
                Console.WriteLine($"Doc updated: {(docUpdated ? "yes" : "no")}");
                if (dryRun)
                {
                    Console.WriteLine("Dry run: no files were modified.");
                }
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
