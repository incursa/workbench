using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace Workbench;

public static class DocSummaryService
{
    private const int MaxDiffChars = 6000;

    public static async Task<DocSummaryResult> SummarizeDocsAsync(
        string repoRoot,
        IEnumerable<string> paths,
        bool staged,
        bool dryRun,
        bool updateIndex)
    {
        var updatedFiles = new List<string>();
        var skippedFiles = new List<string>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var notesAdded = 0;

        if (!AiSummaryClient.TryCreate(out var client, out var reason))
        {
            warnings.Add($"AI summaries disabled: {reason}");
            return new DocSummaryResult(0, 0, updatedFiles, skippedFiles, errors, warnings);
        }

        foreach (var path in paths)
        {
            var normalized = path.Replace('\\', '/');
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
            if (!File.Exists(fullPath))
            {
                skippedFiles.Add($"{normalized} (missing)");
                continue;
            }
            if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                skippedFiles.Add($"{normalized} (not markdown)");
                continue;
            }

            var diff = staged
                ? GitService.GetStagedDiff(repoRoot, path)
                : GitService.GetWorkingDiff(repoRoot, path);
            if (string.IsNullOrWhiteSpace(diff))
            {
                skippedFiles.Add($"{normalized} (no diff)");
                continue;
            }

            var trimmedDiff = diff.Length > MaxDiffChars
                ? diff[^MaxDiffChars..]
                : diff;

            var summary = await client!.SummarizeAsync(trimmedDiff).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(summary))
            {
                warnings.Add($"{normalized} (AI returned empty summary)");
                continue;
            }

            var diffHash = ComputeHash(diff);
            if (!TryAppendSummary(repoRoot, fullPath, summary, diffHash, dryRun, out var added, out var result))
            {
                if (!string.IsNullOrWhiteSpace(result))
                {
                    warnings.Add($"{normalized} ({result})");
                }
                continue;
            }

            if (!added)
            {
                skippedFiles.Add($"{normalized} (summary already present)");
                continue;
            }

            if (!dryRun)
            {
                notesAdded++;
                updatedFiles.Add(normalized);
                if (updateIndex)
                {
                    GitService.Add(repoRoot, path);
                }
            }
            else
            {
                updatedFiles.Add($"{normalized} (dry run)");
            }
        }

        return new DocSummaryResult(updatedFiles.Count, notesAdded, updatedFiles, skippedFiles, errors, warnings);
    }

    private static bool TryAppendSummary(
        string repoRoot,
        string path,
        string summary,
        string diffHash,
        bool dryRun,
        out bool added,
        out string? error)
    {
        added = false;
        error = null;
        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var parseError))
        {
            error = $"front matter parse failed: {parseError}";
            return false;
        }

        var workbench = EnsureWorkbench(frontMatter!, out var changed);
        changed |= EnsurePathMetadata(workbench, repoRoot, path);
        var notes = EnsureStringList(workbench, "changeNotes", out var notesChanged);
        var shortHash = diffHash[..8];
        if (notes.Any(note => string.Equals(ExtractHash(note), shortHash, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        notes.Add($"[{shortHash}] {DateTime.UtcNow:yyyy-MM-dd}: {summary}");
        notesChanged = true;
        added = true;

        if ((changed || notesChanged) && !dryRun)
        {
            File.WriteAllText(path, frontMatter!.Serialize());
        }
        return true;
    }

    private static string ComputeHash(string diff)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(diff));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    private static string? ExtractHash(string note)
    {
        var trimmed = note.TrimStart();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return null;
        }
        var endIndex = trimmed.IndexOf(']');
        if (endIndex <= 1)
        {
            return null;
        }
        return trimmed[1..endIndex];
    }

    private static Dictionary<string, object?> EnsureWorkbench(FrontMatter frontMatter, out bool changed)
    {
        changed = false;
        var workbench = new Dictionary<string, object?>(StringComparer.Ordinal);
        var data = frontMatter.Data;
        if (!data.TryGetValue("workbench", out var workbenchObj) || workbenchObj is null)
        {
            data["workbench"] = workbench;
            changed = true;
        }
        else if (workbenchObj is Dictionary<string, object?> typed)
        {
            workbench = typed;
        }
        else if (workbenchObj is Dictionary<object, object> legacy)
        {
            workbench = legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
            data["workbench"] = workbench;
            changed = true;
        }

        return workbench;
    }

    private static bool EnsurePathMetadata(Dictionary<string, object?> workbench, string repoRoot, string docPath)
    {
        var changed = false;
        var relativePath = Path.GetRelativePath(repoRoot, docPath)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var currentPath = string.Concat(Path.AltDirectorySeparatorChar, relativePath);

        var existingPath = workbench.TryGetValue("path", out var value) ? value?.ToString() : null;
        if (!string.IsNullOrWhiteSpace(existingPath) &&
            !string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            var history = EnsureStringList(workbench, "pathHistory", out var historyChanged);
            if (!history.Any(entry => entry.Equals(existingPath, StringComparison.OrdinalIgnoreCase)))
            {
                history.Add(existingPath);
                changed = true;
            }
            if (historyChanged)
            {
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(existingPath) ||
            !string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            workbench["path"] = currentPath;
            changed = true;
        }

        _ = EnsureStringList(workbench, "pathHistory", out var listChanged);
        if (listChanged)
        {
            changed = true;
        }

        return changed;
    }

    private static List<string> EnsureStringList(Dictionary<string, object?> data, string key, out bool changed)
    {
        changed = false;
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            var list = new List<string>();
            data[key] = list;
            changed = true;
            return list;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var list = enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
            data[key] = list;
            return list;
        }
        var reset = new List<string>();
        data[key] = reset;
        changed = true;
        return reset;
    }
}
