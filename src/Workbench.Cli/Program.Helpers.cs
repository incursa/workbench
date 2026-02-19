// Helper utilities used by CLI command handlers.
// Keeps normalization and formatting behavior consistent across commands and output modes.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Workbench;
using Workbench.Core;

namespace Workbench.Cli;

public partial class Program
{
    static bool runtimeDebugEnabled;
    static string runtimeFormat = "table";

    static string[] NormalizeGlobalOptions(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        var front = new List<string>();
        var remaining = new List<string>();

        var i = 0;
        while (i < args.Length)
        {
            var arg = args[i];
            if (string.Equals(arg, "--repo", StringComparison.Ordinal))
            {
                if (i + 1 < args.Length)
                {
                    front.Add("--repo");
                    front.Add(args[i + 1]);
                    i += 2;
                    continue;
                }
                remaining.Add(arg);
                i++;
                continue;
            }

            if (arg.StartsWith("--repo=", StringComparison.Ordinal))
            {
                front.Add("--repo");
                front.Add(arg["--repo=".Length..]);
                i++;
                continue;
            }

            if (string.Equals(arg, "--format", StringComparison.Ordinal))
            {
                if (i + 1 < args.Length)
                {
                    front.Add("--format");
                    front.Add(args[i + 1]);
                    i += 2;
                    continue;
                }
                remaining.Add(arg);
                i++;
                continue;
            }

            if (arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                front.Add("--format");
                front.Add(arg["--format=".Length..]);
                i++;
                continue;
            }

            if (string.Equals(arg, "--no-color", StringComparison.Ordinal)
                || string.Equals(arg, "--quiet", StringComparison.Ordinal)
                || string.Equals(arg, "--debug", StringComparison.Ordinal))
            {
                front.Add(arg);
                i++;
                continue;
            }

            remaining.Add(arg);
            i++;
        }

        if (front.Count == 0)
        {
            return args;
        }

        front.AddRange(remaining);
        return front.ToArray();
    }

    static void InitializeRuntimeContext(string[] args)
    {
        runtimeFormat = ResolveFormat(ResolveFormatFromArgs(args) ?? "table");
        runtimeDebugEnabled = ResolveDebugFromArgs(args) || IsTruthy(Environment.GetEnvironmentVariable("WORKBENCH_DEBUG"));
    }

    static string? ResolveFormatFromArgs(string[] args)
    {
        string? format = null;
        var index = 0;
        while (index < args.Length)
        {
            var arg = args[index];
            if (string.Equals(arg, "--format", StringComparison.Ordinal))
            {
                if (index + 1 < args.Length)
                {
                    format = args[index + 1];
                    index += 2;
                    continue;
                }
                index++;
                continue;
            }

            if (arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                format = arg["--format=".Length..];
            }
            index++;
        }
        return format;
    }

    static bool ResolveDebugFromArgs(string[] args)
    {
        return args.Any(arg => string.Equals(arg, "--debug", StringComparison.Ordinal));
    }

    static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }

    static void ReportError(Exception ex, string? format = null)
    {
        var error = MapCliError(ex);
        var resolvedFormat = ResolveFormat(format ?? runtimeFormat);
        if (string.Equals(resolvedFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new CliErrorOutput(
                false,
                error);
            WriteJson(payload, WorkbenchJsonContext.Default.CliErrorOutput);
            if (runtimeDebugEnabled)
            {
                Console.Error.WriteLine(ex);
            }
            return;
        }

        Console.Error.WriteLine($"Error: {error.Message}");
        if (!string.IsNullOrWhiteSpace(error.Hint))
        {
            Console.Error.WriteLine($"Hint: {error.Hint}");
        }
        if (runtimeDebugEnabled)
        {
            Console.Error.WriteLine(ex);
        }
    }

    static CliErrorData MapCliError(Exception ex)
    {
        var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unexpected error." : ex.Message.Trim();

        if (ex is InvalidOperationException &&
            string.Equals(message, "Not a git repository.", StringComparison.Ordinal))
        {
            return new CliErrorData(
                "repo_not_git",
                "Target path is not inside a git repository.",
                "Run `git init` in the target directory, or pass `--repo <path>` for an existing repository.");
        }

        if (ex is UnauthorizedAccessException)
        {
            return new CliErrorData(
                "access_denied",
                "Access denied while reading or writing repository files.",
                "Check file permissions and retry.");
        }

        if (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new CliErrorData(
                "path_not_found",
                message,
                "Verify the referenced file or directory exists.");
        }

        return new CliErrorData(
            "unexpected_error",
            message,
            runtimeDebugEnabled ? null : "Re-run with `--debug` for full diagnostics.");
    }

    static string ResolveRepo(string? repoArg)
    {
        var envRepo = Environment.GetEnvironmentVariable("WORKBENCH_REPO");
        var candidate = repoArg ?? envRepo ?? Directory.GetCurrentDirectory();
        var repoRoot = Repository.FindRepoRoot(candidate);
        if (repoRoot is null)
        {
            throw new InvalidOperationException("Not a git repository.");
        }
        // Load repo-scoped env files before any config or service access.
        EnvLoader.LoadRepoEnv(repoRoot);
        return repoRoot;
    }

    static string ResolveFormat(string formatArg)
    {
        var envFormat = Environment.GetEnvironmentVariable("WORKBENCH_FORMAT");
        return string.IsNullOrWhiteSpace(envFormat) ? formatArg : envFormat;
    }

    static void WriteJson<T>(T payload, JsonTypeInfo<T> typeInfo)
    {
        Console.WriteLine(JsonSerializer.Serialize(payload, typeInfo));
    }

    static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "dropped", StringComparison.OrdinalIgnoreCase);
    }

    static bool StringsEqual(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
    }

    static string ResolvePreferredSyncSource(WorkbenchConfig config, string? prefer)
    {
        if (!string.IsNullOrWhiteSpace(prefer))
        {
            var normalized = prefer.Trim().ToLowerInvariant();
            if (normalized is "local" or "github")
            {
                return normalized;
            }
            throw new InvalidOperationException("Invalid sync preference. Use 'local' or 'github'.");
        }

        var configured = config.Github.Sync.ConflictDefault?.Trim().ToLowerInvariant();
        return configured switch
        {
            "local" => "local",
            "github" => "github",
            "fail" or null or "" => "fail",
            _ => "fail"
        };
    }

    static bool TryResolveDocLinkType(string? type, out string resolved)
    {
        resolved = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (resolved is "spec" or "adr")
        {
            return true;
        }
        return false;
    }

    static string ExtractSection(string body, string heading)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Equals($"## {heading}", StringComparison.OrdinalIgnoreCase))
            {
                start = i + 1;
                break;
            }
        }
        if (start == -1)
        {
            return string.Empty;
        }

        var collected = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }
            collected.Add(line);
        }
        return string.Join("\n", collected).Trim();
    }

    static string ResolveWorkItemType(string? overrideType, string? generatedType)
    {
        var candidate = overrideType ?? generatedType;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "task";
        }
        return candidate.Trim().ToLowerInvariant() switch
        {
            "bug" => "bug",
            "task" => "task",
            "spike" => "spike",
            _ => "task"
        };
    }

    static void CleanupTempFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures.
#pragma warning disable ERP022
            }
#pragma warning restore ERP022
        }
    }

    static void PrintRelatedLinks(string label, IEnumerable<string> links)
    {
        var entries = links.Where(link => !string.IsNullOrWhiteSpace(link)).ToList();
        if (entries.Count == 0)
        {
            return;
        }
        Console.WriteLine($"{label}:");
        foreach (var entry in entries)
        {
            Console.WriteLine($"- {entry}");
        }
    }

    static WorkItemPayload ItemToPayload(WorkItem item, bool includeBody = false)
    {
        return new WorkItemPayload(
            item.Id,
            item.Type,
            item.Status,
            item.Title,
            item.Priority,
            item.Owner,
            item.Created,
            item.Updated,
            item.Tags,
            new RelatedLinksPayload(
                item.Related.Specs,
                item.Related.Adrs,
                item.Related.Files,
                item.Related.Prs,
                item.Related.Issues,
                item.Related.Branches),
            item.Slug,
            item.Path,
            includeBody ? item.Body : null);
    }

    static void SetExitCode(int code) => Environment.ExitCode = code;

    static bool IsFirstRun(string repoRoot)
    {
        var configPath = WorkbenchConfig.GetConfigPath(repoRoot);
        var configDir = Path.GetDirectoryName(configPath) ?? Path.Combine(repoRoot, ".workbench");
        return !Directory.Exists(configDir) || !File.Exists(configPath);
    }

    static bool IsPathInsideRepo(string repoRoot, string path)
    {
        var full = Path.GetFullPath(path);
        var repoFull = Path.GetFullPath(repoRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeRepoPath(string repoRoot, string path)
    {
        var full = Path.GetFullPath(path);
        if (!IsPathInsideRepo(repoRoot, full))
        {
            return full.Replace('\\', '/');
        }
        // Repo-relative paths are stored with forward slashes for portability.
        var relative = Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
        return relative;
    }

    static void EnsureGitignoreEntry(string repoRoot, string entry)
    {
        var gitignorePath = Path.Combine(repoRoot, ".gitignore");
        var normalized = entry.Replace('\\', '/').Trim();
        var lines = File.Exists(gitignorePath)
            ? File.ReadAllLines(gitignorePath).ToList()
            : new List<string>();
        if (lines.Any(line => line.Trim().Equals(normalized, StringComparison.Ordinal)))
        {
            return;
        }
        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.Add(string.Empty);
        }
        lines.Add(normalized);
        File.WriteAllLines(gitignorePath, lines);
    }

    static string Prompt(string prompt, string? defaultValue = null)
    {
        var suffix = defaultValue is null ? "" : $" [{defaultValue}]";
        Console.Write($"{prompt}{suffix}: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue ?? string.Empty;
        }
        return input.Trim();
    }

    static bool Confirm(string prompt, bool defaultYes)
    {
        var suffix = defaultYes ? " [Y/n]" : " [y/N]";
        Console.Write($"{prompt}{suffix}: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultYes;
        }
        return input.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }

    static int PromptSelection(string title, IReadOnlyList<(string Label, string Description)> options)
    {
        Console.WriteLine(title);
        for (var i = 0; i < options.Count; i++)
        {
            var entry = options[i];
            Console.WriteLine($"{i + 1}) {entry.Label} - {entry.Description}");
        }
        Console.Write("Choose an option: ");
        var input = Console.ReadLine();
        if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selection))
        {
            return -1;
        }
        if (selection < 1 || selection > options.Count)
        {
            return -1;
        }
        return selection - 1;
    }

    static string NormalizeRepoLink(string repoRoot, string link)
    {
        var trimmed = link.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }
        var normalized = trimmed.Replace('\\', '/');
        if (Path.IsPathRooted(trimmed))
        {
            var full = Path.GetFullPath(trimmed);
            var repoFull = Path.GetFullPath(repoRoot);
            if (full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
            {
                // Convert repo-rooted absolute paths into stable repo-relative links.
                var relative = Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
                return "/" + relative;
            }
            return full.Replace('\\', '/');
        }
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    static string ApplyPattern(string pattern, WorkItem item)
    {
        return pattern
            .Replace("{id}", item.Id)
            .Replace("{slug}", item.Slug)
            .Replace("{title}", item.Title);
    }

    static string ResolveIssueType(GithubIssue issue, string? overrideType)
    {
        if (!string.IsNullOrWhiteSpace(overrideType))
        {
            return overrideType;
        }

        bool HasLabel(string token)
        {
            return issue.Labels.Any(label =>
                label.Equals(token, StringComparison.OrdinalIgnoreCase)
                || label.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        if (HasLabel("bug"))
        {
            return "bug";
        }
        if (HasLabel("spike"))
        {
            return "spike";
        }
        return "task";
    }

    static string ResolveIssueStatus(GithubIssue issue, string? overrideStatus)
    {
        if (!string.IsNullOrWhiteSpace(overrideStatus))
        {
            return overrideStatus;
        }

        return string.Equals(issue.State, "closed", StringComparison.OrdinalIgnoreCase) ? "done" : "ready";
    }
}
