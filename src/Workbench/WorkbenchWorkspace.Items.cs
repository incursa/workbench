using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    public sealed record ItemDeleteResult(WorkItem Item, int DocsUpdated);

    public ItemDeleteResult DeleteItem(string id, bool keepLinks = false)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Work item ID is required.");
        }

        var path = WorkItemService.GetItemPathById(RepoRoot, Config, id);
        var item = WorkItemService.LoadItem(path) ?? throw new InvalidOperationException("Work item not found.");

        var docsUpdated = 0;
        if (!keepLinks)
        {
            var linksToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in item.Related.Specs.Concat(item.Related.Files))
            {
                if (!string.IsNullOrWhiteSpace(link))
                {
                    linksToUpdate.Add(link);
                }
            }

            foreach (var doc in ListDocs(typeFilter: null, query: null))
            {
                if (doc.WorkItems.Contains(item.Id, StringComparer.OrdinalIgnoreCase) ||
                    doc.RelatedArtifacts.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
                {
                    linksToUpdate.Add(doc.Path);
                }
            }

            foreach (var link in linksToUpdate)
            {
                if (DocService.TryUpdateDocWorkItemLink(RepoRoot, Config, link, item.Id, add: false, apply: true))
                {
                    docsUpdated++;
                }
            }
        }

        File.Delete(path);
        return new ItemDeleteResult(item, docsUpdated);
    }
}
