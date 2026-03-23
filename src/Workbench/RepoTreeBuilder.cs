namespace Workbench;

public static class RepoTreeBuilder
{
    public static RepoTreeBranch BuildRoot(IReadOnlyList<RepoTreeEntry> entries)
    {
        var root = new TreeNode("Repository", string.Empty);
        foreach (var entry in entries.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            AddEntry(root, entry);
        }

        return Convert(root);
    }

    private static void AddEntry(TreeNode root, RepoTreeEntry entry)
    {
        var segments = entry.Path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            root.Entries.Add(entry);
            return;
        }

        var current = root;
        var currentPath = string.Empty;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segments[i]
                : $"{currentPath}/{segments[i]}";
            current = current.GetOrCreateChild(segments[i], currentPath);
        }

        current.Entries.Add(entry);
    }

    private static RepoTreeBranch Convert(TreeNode node)
    {
        var children = node.Children.Values
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Convert)
            .ToList();

        var entries = node.Entries
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entryCount = entries.Count + children.Sum(child => child.EntryCount);
        return new RepoTreeBranch(node.Name, node.Path, entryCount, children, entries);
    }

    private sealed class TreeNode(string name, string path)
    {
        public string Name { get; } = name;

        public string Path { get; } = path;

        public SortedDictionary<string, TreeNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<RepoTreeEntry> Entries { get; } = new();

        public TreeNode GetOrCreateChild(string name, string path)
        {
            if (!Children.TryGetValue(name, out var child))
            {
                child = new TreeNode(name, path);
                Children[name] = child;
            }

            return child;
        }
    }
}
