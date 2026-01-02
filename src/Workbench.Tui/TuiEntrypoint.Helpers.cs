using Workbench.Core;

namespace Workbench.Tui;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Workbench;

public static partial class TuiEntrypoint
{
    private static List<WorkItem> LoadItems(string repoRoot, WorkbenchConfig config)
    {
        return WorkItemService.ListItems(repoRoot, config, includeDone: false).Items
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> LoadDocs(string repoRoot, WorkbenchConfig config)
    {
        var docsRoot = Path.Combine(repoRoot, config.Paths.DocsRoot);
        if (!Directory.Exists(docsRoot))
        {
            return new List<string>();
        }

        var rootPrefix = config.Paths.DocsRoot.TrimEnd('/', '\\') + "/";
        return Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Select(path => path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? path[rootPrefix.Length..]
                : path)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ITreeNode> BuildDocsTree(IList<string> docs)
    {
        var roots = new List<ITreeNode>();
        foreach (var doc in docs)
        {
            var parts = doc.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentList = (IList<ITreeNode>)roots;
            var currentPath = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                var existing = currentList
                    .OfType<TreeNode>()
                    .FirstOrDefault(node => string.Equals(node.Text, part, StringComparison.Ordinal));
                if (existing is null)
                {
                    existing = new TreeNode(part)
                    {
                        Children = new List<ITreeNode>()
                    };
                    currentList.Add(existing);
                }

                existing.Tag = currentPath;
                if (existing.Children is null)
                {
                    existing.Children = new List<ITreeNode>();
                }
                currentList = existing.Children;
            }
        }

        return roots;
    }

    private static bool ShouldIncludeLink(string linkType, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var normalized = filter.Trim().ToLowerInvariant();
        if (normalized is "all" or "*")
        {
            return true;
        }

        return string.Equals(linkType, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static void PopulateLinks(
        WorkItem item,
        TextField linkTypeField,
        Label linkHint,
        ListView linksList,
        List<string> linkTargets)
    {
        var filter = linkTypeField.Text?.ToString() ?? string.Empty;
        linkTargets.Clear();
        var linkLabels = new List<string>();

        foreach (var link in item.Related.Specs)
        {
            if (!ShouldIncludeLink("spec", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"spec: {link}");
        }
        foreach (var link in item.Related.Adrs)
        {
            if (!ShouldIncludeLink("adr", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"adr: {link}");
        }
        foreach (var link in item.Related.Files)
        {
            if (!ShouldIncludeLink("file", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"file: {link}");
        }
        foreach (var link in item.Related.Issues)
        {
            if (!ShouldIncludeLink("issue", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"issue: {link}");
        }
        foreach (var link in item.Related.Prs)
        {
            if (!ShouldIncludeLink("pr", filter))
            {
                continue;
            }
            linkTargets.Add(link);
            linkLabels.Add($"pr: {link}");
        }

        linksList.SetSource(linkLabels);
        var counts = $"spec {item.Related.Specs.Count}, adr {item.Related.Adrs.Count}, file {item.Related.Files.Count}, issue {item.Related.Issues.Count}, pr {item.Related.Prs.Count}";
        if (linkLabels.Count > 0)
        {
            linksList.SelectedItem = 0;
            linkHint.Text = $"{counts} | Enter: open selected link";
        }
        else
        {
            linkHint.Text = $"{counts} | No links for this filter.";
        }
    }

    private static List<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .ToList();
    }

    private static string ResolveLink(string repoRoot, string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var trimmed = link.TrimStart('/');
        var combined = Path.Combine(repoRoot, trimmed);
        return Path.GetFullPath(combined);
    }

    private static string ResolveDocsLink(string repoRoot, WorkbenchConfig config, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var trimmed = path.TrimStart('/');
        var docsRoot = config.Paths.DocsRoot.TrimEnd('/', '\\');
        var combined = Path.Combine(repoRoot, docsRoot, trimmed);
        return Path.GetFullPath(combined);
    }

    private static string? GetDocsPathSuggestion(WorkbenchConfig config, string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        if (selectedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(dir) ? null : PrefixDocsRoot(config, dir);
        }

        return PrefixDocsRoot(config, selectedPath.Replace('\\', '/'));
    }

    private static string PrefixDocsRoot(WorkbenchConfig config, string path)
    {
        var docsRoot = config.Paths.DocsRoot.TrimEnd('/', '\\');
        return $"{docsRoot}/{path.TrimStart('/', '\\')}";
    }
}
