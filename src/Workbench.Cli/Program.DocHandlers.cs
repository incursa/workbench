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
        string? artifactId,
        string? domain,
        string? capability,
        string[] workItems,
        string[] codeRefs,
        bool force,
        string displayLabel = "Doc")
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
                force,
                artifactId,
                domain,
                capability);

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocCreateOutput(
                    true,
                    new DocCreateData(result.Path, result.ArtifactId, domain, capability, result.Type, result.WorkItems));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocCreateOutput);
            }
            else
            {
                Console.WriteLine($"{displayLabel} created at {result.Path}");
                if (!string.IsNullOrWhiteSpace(result.ArtifactId))
                {
                    Console.WriteLine($"Artifact ID: {result.ArtifactId}");
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

    static void HandleDocShow(
        string? repo,
        string format,
        string reference,
        string? expectedDocType = null)
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

            var doc = DocService.GetDocShowData(repoRoot, config, reference);
            EnsureDocType(expectedDocType, doc.Type, reference);
            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocShowOutput(true, doc);
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocShowOutput);
            }
            else
            {
                Console.WriteLine($"{doc.ArtifactId ?? Path.GetFileNameWithoutExtension(doc.Path)} - {doc.Title}");
                Console.WriteLine($"Type: {doc.Type}");
                Console.WriteLine($"Status: {doc.Status ?? "-"}");
                Console.WriteLine($"Owner: {doc.Owner ?? "-"}");
                Console.WriteLine($"Domain: {doc.Domain ?? "-"}");
                Console.WriteLine($"Capability: {doc.Capability ?? "-"}");
                Console.WriteLine($"Path: {doc.Path}");
                PrintRelatedLinks("Work items", doc.WorkItems);
                PrintRelatedLinks("Code refs", doc.CodeRefs);
                Console.WriteLine();
                Console.WriteLine(doc.Body);
            }

            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static void HandleDocEdit(
        string? repo,
        string format,
        string reference,
        string? artifactId,
        string? title,
        string? status,
        string? owner,
        string? domain,
        string? capability,
        string? body,
        string? bodyFile,
        string[] workItems,
        string[] codeRefs,
        string? expectedDocType = null,
        string displayLabel = "Doc")
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

            var currentDoc = DocService.GetDocShowData(repoRoot, config, reference);
            EnsureDocType(expectedDocType, currentDoc.Type, reference);

            string? effectiveBody = body;
            if (!string.IsNullOrWhiteSpace(bodyFile))
            {
                var resolvedBodyFile = Path.IsPathRooted(bodyFile)
                    ? bodyFile
                    : Path.Combine(repoRoot, bodyFile);
                effectiveBody = File.ReadAllText(resolvedBodyFile);
            }

            var result = DocService.EditDoc(
                repoRoot,
                config,
                reference,
                artifactId,
                title,
                status,
                owner,
                domain,
                capability,
                effectiveBody,
                workItems.Length == 0 ? null : workItems.ToList(),
                codeRefs.Length == 0 ? null : codeRefs.ToList());

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocEditOutput(
                    true,
                    new DocEditData(
                        result.Path,
                        result.ArtifactId,
                        result.ArtifactIdUpdated,
                        result.TitleUpdated,
                        result.StatusUpdated,
                        result.OwnerUpdated,
                        result.DomainUpdated,
                        result.CapabilityUpdated,
                        result.BodyUpdated,
                        result.WorkItemsUpdated,
                        result.CodeRefsUpdated));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocEditOutput);
            }
            else
            {
                Console.WriteLine($"{displayLabel} updated: {result.Path}");
                if (!string.IsNullOrWhiteSpace(result.ArtifactId))
                {
                    Console.WriteLine($"Artifact ID: {result.ArtifactId}");
                }
                Console.WriteLine($"Artifact ID updated: {result.ArtifactIdUpdated}");
                Console.WriteLine($"Title updated: {result.TitleUpdated}");
                Console.WriteLine($"Status updated: {result.StatusUpdated}");
                Console.WriteLine($"Owner updated: {result.OwnerUpdated}");
                Console.WriteLine($"Domain updated: {result.DomainUpdated}");
                Console.WriteLine($"Capability updated: {result.CapabilityUpdated}");
                Console.WriteLine($"Body updated: {result.BodyUpdated}");
                Console.WriteLine($"Work items updated: {result.WorkItemsUpdated}");
                Console.WriteLine($"Code refs updated: {result.CodeRefsUpdated}");
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
        bool dryRun,
        string? expectedDocType = null,
        string? displayLabel = null)
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

            if (!DocService.TryResolveDocPath(repoRoot, config, docPath, out var resolvedDocPath))
            {
                Console.WriteLine($"Doc not found: {docPath}");
                SetExitCode(2);
                return;
            }

            var currentDoc = DocService.GetDocShowData(repoRoot, config, resolvedDocPath);
            EnsureDocType(expectedDocType, currentDoc.Type, resolvedDocPath);

            var normalizedDoc = "/" + Path.GetRelativePath(repoRoot, resolvedDocPath).Replace('\\', '/');
            var itemsUpdated = 0;
            var docUpdated = false;

            foreach (var workItemId in workItems)
            {
                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
                var key = DocService.DocTypeToRelatedKey(docType);
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
                var label = string.IsNullOrWhiteSpace(displayLabel) ? docType.ToUpperInvariant() : displayLabel;
                Console.WriteLine($"{label} {action}: {normalizedDoc}");
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

    static void HandleDocDelete(
        string? repo,
        string format,
        string link,
        bool keepLinks,
        string? expectedDocType = null,
        string displayLabel = "Doc")
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

            if (!DocService.TryResolveDocPath(repoRoot, config, link, out var resolvedDocPath))
            {
                Console.WriteLine($"Doc not found: {link}");
                SetExitCode(2);
                return;
            }

            var currentDoc = DocService.GetDocShowData(repoRoot, config, resolvedDocPath);
            EnsureDocType(expectedDocType, currentDoc.Type, resolvedDocPath);

            var docFullPath = Path.GetFullPath(resolvedDocPath);
            var itemsUpdated = 0;
            if (!keepLinks)
            {
                var items = WorkItemService.ListItems(repoRoot, config, includeDone: true).Items;
                foreach (var item in items)
                {
                    var itemChanged = false;
                    foreach (var spec in item.Related.Specs)
                    {
                        var specPath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, spec));
                        if (specPath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                            && WorkItemService.RemoveRelatedLink(item.Path, "specs", spec))
                        {
                            itemChanged = true;
                        }
                    }
                    foreach (var adr in item.Related.Adrs)
                    {
                        var adrPath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, adr));
                        if (adrPath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                            && WorkItemService.RemoveRelatedLink(item.Path, "adrs", adr))
                        {
                            itemChanged = true;
                        }
                    }
                    foreach (var file in item.Related.Files)
                    {
                        var filePath = Path.GetFullPath(DocService.ResolveDocPath(repoRoot, file));
                        if (filePath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase)
                            && WorkItemService.RemoveRelatedLink(item.Path, "files", file))
                        {
                            itemChanged = true;
                        }
                    }
                    if (itemChanged)
                    {
                        itemsUpdated++;
                    }
                }
            }

            File.Delete(docFullPath);

            if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new DocDeleteOutput(
                    true,
                    new DocDeleteData(docFullPath, itemsUpdated));
                WriteJson(payload, Core.WorkbenchJsonContext.Default.DocDeleteOutput);
            }
            else
            {
                Console.WriteLine($"{displayLabel} deleted: {docFullPath}");
            }
            SetExitCode(0);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            SetExitCode(2);
        }
    }

    static void EnsureDocType(string? expectedDocType, string actualDocType, string reference)
    {
        if (string.IsNullOrWhiteSpace(expectedDocType))
        {
            return;
        }

        var expected = expectedDocType.Trim().ToLowerInvariant();
        var actual = actualDocType.Trim().ToLowerInvariant();
        var matches =
            string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(expected, "spec", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(actual, "specification", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(expected, "specification", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(actual, "spec", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(expected, "guide", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(actual, "architecture", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(expected, "architecture", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(actual, "guide", StringComparison.OrdinalIgnoreCase));

        if (!matches)
        {
            throw new InvalidOperationException(
                $"{reference} is a '{actualDocType}' document. This command only supports '{expectedDocType}' documents.");
        }
    }
}
