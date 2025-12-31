using System.Text;
using System.Text.RegularExpressions;

namespace Workbench;

public static class LinkUpdater
{
    public sealed record LinkUpdateResult(int FilesUpdated);

    public static LinkUpdateResult UpdateLinks(string repoRoot, string oldPath, string newPath)
    {
        var updated = 0;
        var oldRepoRelative = NormalizeRepoRelative(repoRoot, oldPath);
        var newRepoRelative = NormalizeRepoRelative(repoRoot, newPath);
        var oldAbsolute = "/" + oldRepoRelative.Replace('\\', '/');
        var newAbsolute = "/" + newRepoRelative.Replace('\\', '/');

        foreach (var file in EnumerateMarkdownFiles(repoRoot))
        {
            var content = File.ReadAllText(file);
            var updatedContent = ReplaceLinks(content, repoRoot, file, oldRepoRelative, newRepoRelative, oldAbsolute, newAbsolute);
            if (!ReferenceEquals(content, updatedContent) && !string.Equals(content, updatedContent, StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(file, updatedContent);
                updated++;
            }
        }

        return new LinkUpdateResult(updated);
    }

    private static string ReplaceLinks(
        string content,
        string repoRoot,
        string currentFile,
        string oldRepoRelative,
        string newRepoRelative,
        string oldAbsolute,
        string newAbsolute)
    {
        var matches = Regex.Matches(content, @"\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        if (matches.Count == 0)
        {
            return content;
        }

        var builder = new StringBuilder(content);
        var offset = 0;
        foreach (Match match in matches)
        {
            var group = match.Groups[1];
            var target = group.Value;
            var updatedTarget = UpdateTarget(repoRoot, currentFile, target, oldRepoRelative, newRepoRelative, oldAbsolute, newAbsolute);
            if (string.Equals(updatedTarget, target, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = group.Index + offset;
            builder.Remove(start, group.Length);
            builder.Insert(start, updatedTarget);
            offset += updatedTarget.Length - group.Length;
        }

        return builder.ToString();
    }

    private static string UpdateTarget(
        string repoRoot,
        string currentFile,
        string target,
        string oldRepoRelative,
        string newRepoRelative,
        string oldAbsolute,
        string newAbsolute)
    {
        var trimmed = target.Trim();
        if (trimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        var cleanTarget = trimmed.Split('#')[0].Split('?')[0];
        if (string.Equals(cleanTarget, oldAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            return target.Replace(oldAbsolute, newAbsolute);
        }

        var currentDir = Path.GetDirectoryName(currentFile) ?? repoRoot;
        var currentRepoRelative = NormalizeRepoRelative(repoRoot, currentDir);
        var oldRelative = MakeRelative(currentRepoRelative, oldRepoRelative);
        var newRelative = MakeRelative(currentRepoRelative, newRepoRelative);
        if (string.Equals(cleanTarget, oldRelative, StringComparison.OrdinalIgnoreCase))
        {
            return target.Replace(oldRelative, newRelative);
        }

        if (string.Equals(cleanTarget, oldRepoRelative, StringComparison.OrdinalIgnoreCase))
        {
            return target.Replace(oldRepoRelative, newRepoRelative);
        }

        return target;
    }

    private static string NormalizeRepoRelative(string repoRoot, string path)
    {
        var full = Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
        var relative = Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
        return string.Equals(relative, ".", StringComparison.OrdinalIgnoreCase) ? string.Empty : relative;
    }

    private static string MakeRelative(string fromRepoRelativeDir, string toRepoRelativePath)
    {
        var from = Path.Combine(Path.DirectorySeparatorChar.ToString(), fromRepoRelativeDir);
        var to = Path.Combine(Path.DirectorySeparatorChar.ToString(), toRepoRelativePath);
        var relative = Path.GetRelativePath(from, to);
        return relative.Replace('\\', '/');
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string repoRoot)
    {
        var stack = new Stack<string>();
        stack.Push(repoRoot);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var dir in Directory.EnumerateDirectories(current))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                stack.Push(dir);
            }

            foreach (var file in Directory.EnumerateFiles(current, "*.md"))
            {
                yield return file;
            }
        }
    }
}
