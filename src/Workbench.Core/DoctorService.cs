namespace Workbench.Core;

/// <summary>Runs repository health checks and deterministic local repairs.</summary>
public static class DoctorService
{
    private const int SchemaVersion = 1;

    public static async Task<DoctorRunResult> RunAsync(
        string repoRoot,
        bool fix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        var normalizedRoot = Path.GetFullPath(repoRoot);
        var repairs = new List<string>();
        var checks = new List<DoctorCheck>();

        var config = WorkbenchConfig.Load(normalizedRoot, out var configError);
        var configPath = WorkbenchConfig.GetConfigPath(normalizedRoot);
        var missing = GetMissingPaths(normalizedRoot, config);
        if (fix && configError is null && (!File.Exists(configPath) || missing.Count > 0))
        {
            ScaffoldService.Scaffold(normalizedRoot, force: false);
            repairs.Add("scaffolded missing repository files and directories");
            config = WorkbenchConfig.Load(normalizedRoot, out configError);
            missing = GetMissingPaths(normalizedRoot, config);
        }

        var schemaErrors = SchemaValidationService.ValidateConfig(normalizedRoot);
        var hasErrors = configError is not null || schemaErrors.Count > 0;
        var hasWarnings = false;

        AddGitCheck(normalizedRoot, checks, ref hasWarnings);
        checks.Add(new DoctorCheck(
            "repo",
            "ok",
            new DoctorCheckDetails(null, null, null, normalizedRoot, null, null)));

        if (File.Exists(configPath) && configError is null && schemaErrors.Count == 0)
        {
            checks.Add(new DoctorCheck(
                "config",
                "ok",
                new DoctorCheckDetails(null, null, null, configPath, null, null)));
        }
        else
        {
            checks.Add(new DoctorCheck(
                "config",
                "warn",
                new DoctorCheckDetails(null, configError, null, configPath, null, schemaErrors)));
            hasWarnings = true;
        }

        if (missing.Count == 0)
        {
            checks.Add(new DoctorCheck("paths", "ok", null));
        }
        else
        {
            checks.Add(new DoctorCheck(
                "paths",
                "warn",
                new DoctorCheckDetails(null, null, null, null, missing, null)));
            hasWarnings = true;
        }

        var navigationDrift = 0;
        if (!hasErrors)
        {
            var preview = await NavigationService.SyncNavigationAsync(
                normalizedRoot,
                config,
                includeDone: false,
                syncIssues: false,
                force: false,
                dryRun: true,
                syncDocs: false,
                normalizeItems: false,
                createMissingIndexes: false).ConfigureAwait(false);
            navigationDrift = preview.IndexFilesUpdated;
            if (fix && navigationDrift > 0)
            {
                var repairedCount = 0;
                for (var attempt = 0; attempt < 5 && navigationDrift > 0; attempt++)
                {
                    var repaired = await NavigationService.SyncNavigationAsync(
                        normalizedRoot,
                        config,
                        includeDone: false,
                        syncIssues: false,
                        force: false,
                        dryRun: false,
                        syncDocs: false,
                        normalizeItems: false,
                        createMissingIndexes: false).ConfigureAwait(false);
                    repairedCount += repaired.IndexFilesUpdated;
                    preview = await NavigationService.SyncNavigationAsync(
                        normalizedRoot,
                        config,
                        includeDone: false,
                        syncIssues: false,
                        force: false,
                        dryRun: true,
                        syncDocs: false,
                        normalizeItems: false,
                        createMissingIndexes: false).ConfigureAwait(false);
                    navigationDrift = preview.IndexFilesUpdated;
                }
                repairs.Add($"refreshed {repairedCount} generated navigation index update(s)");
            }
        }

        if (navigationDrift == 0)
        {
            checks.Add(new DoctorCheck("generated-navigation", "ok", null));
        }
        else
        {
            checks.Add(new DoctorCheck(
                "generated-navigation",
                "warn",
                new DoctorCheckDetails(
                    null,
                    null,
                    $"{navigationDrift} generated index file(s) are stale.",
                    null,
                    null,
                    null)));
            hasWarnings = true;
        }

        var githubProvider = string.IsNullOrWhiteSpace(config.Github.Provider) ? "octokit" : config.Github.Provider;
        var githubStatus = await GithubService.CheckAuthStatusAsync(normalizedRoot, config).ConfigureAwait(false);
        if (string.Equals(githubStatus.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new DoctorCheck(
                "github",
                "ok",
                new DoctorCheckDetails(githubStatus.Version, null, null, null, null, null)));
        }
        else
        {
            checks.Add(new DoctorCheck(
                "github",
                githubStatus.Status,
                new DoctorCheckDetails(null, null, githubStatus.Reason, null, null, null)));
            hasWarnings = true;
        }

        var aiReason = AddAiCheck(checks, "openai", "WORKBENCH_AI_MODEL", "OPENAI_MODEL", "gpt-4o-mini");
        if (aiReason is not null)
        {
            hasWarnings = true;
        }

        var codexAvailable = CodexService.TryGetVersion(normalizedRoot, out var codexVersion, out var codexError);
        checks.Add(new DoctorCheck(
            "codex",
            codexAvailable ? "ok" : "warn",
            new DoctorCheckDetails(codexVersion, codexAvailable ? null : codexError, null, null, null, null)));
        if (!codexAvailable)
        {
            hasWarnings = true;
        }

        var voiceReason = AddAiCheck(
            checks,
            "voice-transcription",
            "WORKBENCH_AI_TRANSCRIPTION_MODEL",
            "OPENAI_MODEL",
            "gpt-4o-mini-transcribe");
        if (voiceReason is not null)
        {
            hasWarnings = true;
        }

        var nextAction = GetNextAction(
            configPath,
            configError,
            schemaErrors,
            missing,
            navigationDrift,
            githubStatus.Status,
            githubProvider,
            aiReason,
            codexAvailable,
            voiceReason,
            fix);
        return new DoctorRunResult(
            new DoctorData(SchemaVersion, normalizedRoot, checks, fix, repairs, nextAction),
            hasErrors,
            hasWarnings);
    }

    private static List<string> GetMissingPaths(string repoRoot, WorkbenchConfig config)
    {
        return new[]
            {
                config.Paths.DocsRoot,
                config.Paths.WorkRoot,
                config.Paths.ItemsDir,
                config.Paths.SpecsTemplatesDir
            }
            .Where(path => !Directory.Exists(Path.Combine(repoRoot, path)))
            .ToList();
    }

    private static void AddGitCheck(string repoRoot, IList<DoctorCheck> checks, ref bool hasWarnings)
    {
        try
        {
            var gitResult = GitService.Run(repoRoot, "--version");
            if (gitResult.ExitCode == 0)
            {
                checks.Add(new DoctorCheck(
                    "git",
                    "ok",
                    new DoctorCheckDetails(gitResult.StdOut, null, null, null, null, null)));
                return;
            }

            checks.Add(new DoctorCheck(
                "git",
                "warn",
                new DoctorCheckDetails(null, gitResult.StdErr, null, null, null, null)));
        }
#pragma warning disable ERP022
        catch
        {
            checks.Add(new DoctorCheck(
                "git",
                "warn",
                new DoctorCheckDetails(null, "git not installed or not on PATH.", null, null, null, null)));
        }
#pragma warning restore ERP022
        hasWarnings = true;
    }

    private static string? GetNextAction(
        string configPath,
        string? configError,
        IList<string> schemaErrors,
        IList<string> missing,
        int navigationDrift,
        string githubStatus,
        string githubProvider,
        string? aiReason,
        bool codexAvailable,
        string? voiceReason,
        bool fix)
    {
        if (configError is not null || schemaErrors.Count > 0)
        {
            return $"Fix the invalid configuration at {configPath}, then rerun workbench doctor.";
        }
        if (missing.Count > 0 || navigationDrift > 0)
        {
            return fix
                ? "Review the remaining local repair warning, then rerun workbench doctor."
                : "workbench doctor --fix";
        }
        if (!string.Equals(githubStatus, "ok", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(githubProvider, "gh", StringComparison.OrdinalIgnoreCase)
                ? "Run gh auth login and rerun workbench doctor."
                : "Set WORKBENCH_GITHUB_TOKEN or GITHUB_TOKEN and rerun workbench doctor.";
        }
        if (aiReason is not null || voiceReason is not null)
        {
            return "Set WORKBENCH_AI_OPENAI_KEY or OPENAI_API_KEY, or set WORKBENCH_AI_PROVIDER=none, then rerun workbench doctor.";
        }
        if (!codexAvailable)
        {
            return "Install the codex executable on PATH, then rerun workbench doctor.";
        }
        return null;
    }

    private static string? AddAiCheck(
        IList<DoctorCheck> checks,
        string name,
        string specificModelVariable,
        string fallbackModelVariable,
        string defaultModel)
    {
        var provider = Environment.GetEnvironmentVariable("WORKBENCH_AI_PROVIDER") ?? "openai";
        if (string.Equals(provider, "none", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(new DoctorCheck(
                name,
                "ok",
                new DoctorCheckDetails(null, null, "disabled by WORKBENCH_AI_PROVIDER=none", null, null, null)));
            return null;
        }
        if (!string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var unsupported = $"Unsupported AI provider '{provider}'.";
            checks.Add(new DoctorCheck(
                name,
                "warn",
                new DoctorCheckDetails(null, null, unsupported, null, null, null)));
            return unsupported;
        }

        var key = Environment.GetEnvironmentVariable("WORKBENCH_AI_OPENAI_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            const string Missing = "Missing WORKBENCH_AI_OPENAI_KEY or OPENAI_API_KEY.";
            checks.Add(new DoctorCheck(
                name,
                "warn",
                new DoctorCheckDetails(null, null, Missing, null, null, null)));
            return Missing;
        }

        var model = Environment.GetEnvironmentVariable(specificModelVariable)
            ?? Environment.GetEnvironmentVariable("WORKBENCH_AI_MODEL")
            ?? Environment.GetEnvironmentVariable(fallbackModelVariable)
            ?? defaultModel;
        checks.Add(new DoctorCheck(
            name,
            "ok",
            new DoctorCheckDetails(model, null, "configuration present; no network call made", null, null, null)));
        return null;
    }
}
