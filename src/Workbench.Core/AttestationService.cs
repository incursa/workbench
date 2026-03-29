using System.Globalization;
using System.Text.Json;

#pragma warning disable MA0048

namespace Workbench.Core;

public sealed record AttestationRunOptions(
    IList<string>? Scope,
    string? Profile,
    string Emit,
    string OutDir,
    string? ConfigPath,
    string? ResultsPath,
    string? CoveragePath,
    string? BenchmarksPath,
    string? ManualQaPath,
    bool Exec,
    bool NoExec);

public sealed record AttestationRunResult(
    AttestationSnapshot Snapshot,
    string? SummaryHtmlPath,
    string? DetailsHtmlPath,
    string? JsonPath,
    IList<string> Warnings);

public static partial class AttestationService
{
    public const string DefaultOutputDirectory = "artifacts/quality/attestation";

    internal static readonly string[] DefaultQualityReportRoots =
    [
        "artifacts/quality/testing",
        "artifacts/quality",
        "artifacts"
    ];

    internal static readonly string[] DefaultTestResultsRoots =
    [
        "artifacts/raw/test-results",
        "artifacts/test-results",
        "artifacts/results"
    ];

    internal static readonly string[] DefaultCoverageRoots =
    [
        "artifacts/raw/coverage",
        "artifacts/coverage"
    ];

    internal static readonly string[] DefaultBenchmarkRoots =
    [
        "artifacts/raw/benchmarks",
        "artifacts/benchmarks",
        "quality/benchmarks"
    ];

    internal static readonly string[] DefaultManualQaRoots =
    [
        "quality/manual-qa",
        "artifacts/manual-qa",
        "artifacts/quality/manual-qa"
    ];

    internal static readonly IReadOnlySet<string> PassingBenchmarkStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "passing", "passed", "pass", "ok", "success", "succeeded", "complete", "completed", "done"
        };

    internal static readonly IReadOnlySet<string> FailingBenchmarkStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "failing", "failed", "fail", "error", "blocked"
        };

    internal static readonly IReadOnlySet<string> PendingBenchmarkStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pending", "planned", "in_progress", "in-progress", "queued", "running"
        };

    internal static readonly IReadOnlySet<string> StaleBenchmarkStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stale", "obsolete", "outdated"
        };

    internal static readonly IReadOnlySet<string> PassingVerificationStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "passed", "waived"
        };

    internal static readonly IReadOnlySet<string> FailingVerificationStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "failed"
        };

    internal static readonly IReadOnlySet<string> PendingVerificationStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "planned", "blocked"
        };

    internal static readonly IReadOnlySet<string> StaleVerificationStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "obsolete"
        };

    public static AttestationRunResult Generate(string repoRoot, AttestationRunOptions options)
    {
        var warnings = new List<string>();

        var workbenchConfig = WorkbenchConfig.Load(repoRoot, out var workbenchConfigError);
        if (!string.IsNullOrWhiteSpace(workbenchConfigError))
        {
            warnings.Add($"Workbench config: {workbenchConfigError}");
        }

        var attestationConfig = AttestationConfig.Load(repoRoot, options.ConfigPath, out var attestationConfigError);
        if (!string.IsNullOrWhiteSpace(attestationConfigError))
        {
            warnings.Add($"Attestation config: {attestationConfigError}");
        }

        var selectedProfile = ResolveSelectedProfile(options.Profile, workbenchConfig.Validation?.Profile);
        var effectiveScope = BuildEffectiveScope(options.Scope, attestationConfig.ScopeIncludes, attestationConfig.ScopeExcludes);
        var validationScope = effectiveScope.Count > 0 ? effectiveScope : new List<string>();

        var artifactIdPolicy = ArtifactIdPolicy.Load(repoRoot, out var artifactIdPolicyError);
        if (!string.IsNullOrWhiteSpace(artifactIdPolicyError))
        {
            warnings.Add(artifactIdPolicyError);
        }

        var validationSnapshots = BuildValidationSnapshots(repoRoot, workbenchConfig, attestationConfig, validationScope);
        var selectedValidation = ValidationService.ValidateRepo(
            repoRoot,
            workbenchConfig,
            new ValidationOptions(Array.Empty<string>(), Array.Empty<string>(), false, selectedProfile, validationScope));
        var graph = ValidationGraphValidator.BuildGraph(
            repoRoot,
            workbenchConfig,
            new ValidationOptions(Array.Empty<string>(), Array.Empty<string>(), false, selectedProfile, validationScope),
            artifactIdPolicy,
            new ValidationResult(),
            validationScope,
            NormalizePrefixes(workbenchConfig.Validation?.DocExclude));

        var authoredIntent = LoadQualityIntent(repoRoot, attestationConfig, warnings);
        var evidence = BuildEvidenceSnapshot(repoRoot, attestationConfig, authoredIntent, options, warnings);
        var snapshot = BuildSnapshot(
            repoRoot,
            options,
            attestationConfig,
            selectedProfile,
            effectiveScope,
            validationSnapshots,
            selectedValidation,
            graph,
            evidence,
            warnings);

        var outputDirectory = ResolveOutputDirectory(repoRoot, options.OutDir);
        Directory.CreateDirectory(outputDirectory);

        string? summaryPath = null;
        string? detailsPath = null;
        string? jsonPath = null;

        var emit = NormalizeEmit(options.Emit);
        if (emit is not "html" and not "json" and not "both")
        {
            throw new InvalidOperationException($"Unsupported attestation emit mode '{options.Emit}'. Expected html, json, or both.");
        }

        if (emit is "html" or "both")
        {
            summaryPath = Path.Combine(outputDirectory, "summary.html");
            detailsPath = Path.Combine(outputDirectory, "details.html");
            AttestationHtmlWriter.WriteSummary(summaryPath, snapshot, "details.html", "attestation.json");
            AttestationHtmlWriter.WriteDetails(detailsPath, snapshot, "summary.html", "attestation.json");
        }

        if (emit is "json" or "both")
        {
            jsonPath = Path.Combine(outputDirectory, "attestation.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(snapshot, WorkbenchJsonContext.Default.AttestationSnapshot));
        }

        return new AttestationRunResult(
            snapshot,
            summaryPath is null ? null : NormalizeRepoPath(repoRoot, summaryPath),
            detailsPath is null ? null : NormalizeRepoPath(repoRoot, detailsPath),
            jsonPath is null ? null : NormalizeRepoPath(repoRoot, jsonPath),
            warnings);
    }

    private static string ResolveSelectedProfile(string? requestedProfile, string? configuredProfile)
    {
        var candidate = requestedProfile ?? configuredProfile ?? ValidationProfiles.Auditable;
        var resolved = ValidationProfiles.Resolve(candidate, null, out var error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }

        return resolved;
    }

    private static string ResolveOutputDirectory(string repoRoot, string outDir)
    {
        return ResolvePath(repoRoot, string.IsNullOrWhiteSpace(outDir) ? DefaultOutputDirectory : outDir);
    }

    private static string NormalizeEmit(string emit)
    {
        return string.IsNullOrWhiteSpace(emit) ? "both" : emit.Trim().ToLowerInvariant();
    }
}

#pragma warning restore MA0048
