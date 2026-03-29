using System.Globalization;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record AttestationExecutionCommandSpec(
    string Command,
    IList<string> Args,
    string? WorkingDirectory);

public sealed record AttestationStatusPolicy(
    IList<string> WorkItemDone,
    IList<string> WorkItemInProgress,
    IList<string> WorkItemOpen,
    IList<string> WorkItemBlocked,
    IList<string> VerificationPassing,
    IList<string> VerificationFailing,
    IList<string> VerificationPending,
    IList<string> VerificationStale);

public sealed record AttestationRollupConfig(
    bool Implemented,
    bool Verified,
    bool ReleaseReady,
    bool RequireNoOpenWorkItems,
    bool RequireNoValidationErrors,
    bool RequireNoFailingVerifications,
    bool RequireNoStaleEvidence);

public sealed record AttestationConfig(
    string ConfigPath,
    IList<string> ScopeIncludes,
    IList<string> ScopeExcludes,
    IList<string> QualityTestingRoots,
    IList<string> TestResultsRoots,
    IList<string> CoverageRoots,
    IList<string> BenchmarkRoots,
    IList<string> ManualQaRoots,
    IList<AttestationExecutionCommandSpec> TestCommands,
    IList<AttestationExecutionCommandSpec> CoverageCommands,
    IList<AttestationExecutionCommandSpec> BenchmarkCommands,
    IList<AttestationExecutionCommandSpec> ManualQaCommands,
    AttestationStatusPolicy StatusPolicy,
    AttestationRollupConfig? Rollups,
    int? StaleAfterDays)
{
    public const string DefaultConfigPath = "quality/attestation.yaml";

    public static AttestationConfig Load(string repoRoot, string? configPath, out string? error)
    {
        error = null;
        var targetPath = ResolveConfigPath(repoRoot, configPath);

        if (!File.Exists(targetPath))
        {
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                error = $"Attestation config not found: {targetPath}";
            }

            return CreateDefault(targetPath);
        }

        var content = File.ReadAllText(targetPath);
        var wrapped = $"---\n{content}\n---\n";
        if (!FrontMatter.TryParse(wrapped, out var frontMatter, out var parseError))
        {
            error = parseError;
            return CreateDefault(targetPath);
        }

        try
        {
            return ParseConfig(targetPath, frontMatter!.Data);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return CreateDefault(targetPath);
        }
    }

    private static AttestationConfig CreateDefault(string configPath)
    {
        return new AttestationConfig(
            configPath,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<AttestationExecutionCommandSpec>(),
            new List<AttestationExecutionCommandSpec>(),
            new List<AttestationExecutionCommandSpec>(),
            new List<AttestationExecutionCommandSpec>(),
            new AttestationStatusPolicy(
                new List<string> { "complete", "cancelled", "superseded" },
                new List<string> { "in_progress" },
                new List<string> { "planned" },
                new List<string> { "blocked" },
                new List<string> { "passed", "waived" },
                new List<string> { "failed" },
                new List<string> { "planned" },
                new List<string> { "obsolete" }),
            null,
            null);
    }

    private static AttestationConfig ParseConfig(string configPath, IDictionary<string, object?> data)
    {
        var scope = GetMap(data, "scope");
        var evidenceRoots = GetMap(data, "evidenceRoots");
        var execution = GetMap(data, "execution");
        var statusPolicy = GetMap(data, "statusPolicy");
        var rollups = GetMap(data, "rollups");
        var freshness = GetMap(data, "freshness");

        return new AttestationConfig(
            configPath,
            GetStringList(scope, "includes"),
            GetStringList(scope, "excludes"),
            GetStringList(evidenceRoots, "qualityTesting"),
            GetStringList(evidenceRoots, "testResults"),
            GetStringList(evidenceRoots, "coverage"),
            GetStringList(evidenceRoots, "benchmarks"),
            GetStringList(evidenceRoots, "manualQa"),
            ReadCommandSpecs(execution, "tests"),
            ReadCommandSpecs(execution, "coverage"),
            ReadCommandSpecs(execution, "benchmarks"),
            ReadCommandSpecs(execution, "manualQa"),
            new AttestationStatusPolicy(
                GetStringList(GetMap(statusPolicy, "workItems"), "done").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "workItems"), "done")
                    : new List<string> { "complete", "cancelled", "superseded" },
                GetStringList(GetMap(statusPolicy, "workItems"), "inProgress").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "workItems"), "inProgress")
                    : new List<string> { "in_progress" },
                GetStringList(GetMap(statusPolicy, "workItems"), "open").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "workItems"), "open")
                    : new List<string> { "planned" },
                GetStringList(GetMap(statusPolicy, "workItems"), "blocked").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "workItems"), "blocked")
                    : new List<string> { "blocked" },
                GetStringList(GetMap(statusPolicy, "verifications"), "passing").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "verifications"), "passing")
                    : new List<string> { "passed", "waived" },
                GetStringList(GetMap(statusPolicy, "verifications"), "failing").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "verifications"), "failing")
                    : new List<string> { "failed" },
                GetStringList(GetMap(statusPolicy, "verifications"), "pending").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "verifications"), "pending")
                    : new List<string> { "planned" },
                GetStringList(GetMap(statusPolicy, "verifications"), "stale").Count > 0
                    ? GetStringList(GetMap(statusPolicy, "verifications"), "stale")
                    : new List<string> { "obsolete" }),
            ReadRollupConfig(rollups),
            ReadNullableInt(freshness, "staleAfterDays"));
    }

    private static AttestationRollupConfig? ReadRollupConfig(IDictionary<string, object?>? rollups)
    {
        if (rollups is null || rollups.Count == 0)
        {
            return null;
        }

        var releaseReady = GetMap(rollups, "releaseReady");
        return new AttestationRollupConfig(
            GetBool(rollups, "implemented", false),
            GetBool(rollups, "verified", false),
            GetBool(releaseReady, "enabled", false),
            GetBool(releaseReady, "requireNoOpenWorkItems", true),
            GetBool(releaseReady, "requireNoValidationErrors", true),
            GetBool(releaseReady, "requireNoFailingVerifications", true),
            GetBool(releaseReady, "requireNoStaleEvidence", false));
    }

    private static IList<AttestationExecutionCommandSpec> ReadCommandSpecs(IDictionary<string, object?>? section, string key)
    {
        if (section is null || !TryGetValue(section, key, out var value) || value is null)
        {
            return new List<AttestationExecutionCommandSpec>();
        }

        var commands = new List<AttestationExecutionCommandSpec>();
        if (value is IEnumerable<object?> list)
        {
            foreach (var entry in list)
            {
                if (entry is IDictionary<string, object?> map)
                {
                    commands.Add(ParseCommandSpec(map));
                }
            }

            return commands;
        }

        if (value is IDictionary<string, object?> mapValue)
        {
            commands.Add(ParseCommandSpec(mapValue));
            return commands;
        }

        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            commands.Add(new AttestationExecutionCommandSpec(text.Trim(), new List<string>(), null));
        }

        return commands;
    }

    private static AttestationExecutionCommandSpec ParseCommandSpec(IDictionary<string, object?> map)
    {
        var command = GetString(map, "command") ?? GetString(map, "executable") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Execution command is missing a command or executable value.");
        }

        return new AttestationExecutionCommandSpec(
            command,
            GetStringList(map, "args"),
            GetString(map, "workingDirectory"));
    }

    private static IDictionary<string, object?>? GetMap(IDictionary<string, object?>? parent, string key)
    {
        if (parent is null || !TryGetValue(parent, key, out var value) || value is null)
        {
            return null;
        }

        if (value is IDictionary<string, object?> map)
        {
            return map;
        }

        return null;
    }

    private static bool TryGetValue(IDictionary<string, object?> parent, string key, out object? value)
    {
        foreach (var entry in parent)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IList<string> GetStringList(IDictionary<string, object?>? parent, string key)
    {
        if (parent is null || !TryGetValue(parent, key, out var value) || value is null)
        {
            return new List<string>();
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? new List<string>()
                : new List<string> { text.Trim() };
        }

        if (value is IEnumerable<object?> list)
        {
            return list
                .Select(entry => entry?.ToString()?.Trim())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry!)
                .ToList();
        }

        return new List<string> { value.ToString()?.Trim() ?? string.Empty }
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList();
    }

    private static string? GetString(IDictionary<string, object?>? parent, string key)
    {
        if (parent is null || !TryGetValue(parent, key, out var value) || value is null)
        {
            return null;
        }

        var text = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool GetBool(IDictionary<string, object?>? parent, string key, bool defaultValue)
    {
        if (parent is null || !TryGetValue(parent, key, out var value) || value is null)
        {
            return defaultValue;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        if (bool.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static int? ReadNullableInt(IDictionary<string, object?>? parent, string key)
    {
        if (parent is null || !TryGetValue(parent, key, out var value) || value is null)
        {
            return null;
        }

        if (value is int i)
        {
            return i;
        }

        if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string ResolveConfigPath(string repoRoot, string? configPath)
    {
        var target = string.IsNullOrWhiteSpace(configPath)
            ? DefaultConfigPath
            : configPath.Trim();

        return Path.IsPathRooted(target)
            ? Path.GetFullPath(target)
            : Path.Combine(repoRoot, target);
    }
}

#pragma warning restore MA0048
