// Repository validation orchestration for work items, docs, and links.
// Invariants: validation is read-only; counts reflect items present at scan time.
#pragma warning disable S1144, S1172
using System.Text.RegularExpressions;

namespace Workbench.Core;

public static class ValidationService
{
    public static ValidationResult ValidateRepo(string repoRoot, WorkbenchConfig config, ValidationOptions? options = null)
    {
        options ??= new ValidationOptions();
        var result = new ValidationResult();
        var configErrors = SchemaValidationService.ValidateConfig(repoRoot);
        foreach (var error in configErrors)
        {
            result.AddError(
                ValidationProfiles.RepoState,
                ValidationCategories.Schema,
                error,
                file: WorkbenchConfig.GetConfigPath(repoRoot));
        }

        var selectedProfile = ValidationProfiles.Resolve(options.Profile, config.Validation?.Profile, out var profileError);
        result.Profile = selectedProfile;
        var scopePrefixes = NormalizePrefixes(options.Scope);
        foreach (var prefix in scopePrefixes)
        {
            result.Scope.Add(prefix);
        }

        if (!string.IsNullOrWhiteSpace(profileError))
        {
            result.AddError(
                ValidationProfiles.RepoState,
                ValidationCategories.Profile,
                profileError,
                file: WorkbenchConfig.GetConfigPath(repoRoot));
        }

        var artifactIdPolicyPath = Path.Combine(repoRoot, "artifact-id-policy.json");
        var artifactIdPolicy = ArtifactIdPolicy.Load(repoRoot, out var artifactIdPolicyError);
        var artifactIdPolicyEnabled = File.Exists(artifactIdPolicyPath);
        if (!string.IsNullOrWhiteSpace(artifactIdPolicyError))
        {
            result.AddError(
                ValidationProfiles.RepoState,
                ValidationCategories.Identifier,
                artifactIdPolicyError,
                file: artifactIdPolicyPath);
        }

        var graph = ValidationGraphValidator.ValidateCanonicalGraph(
            repoRoot,
            config,
            options,
            selectedProfile,
            scopePrefixes,
            artifactIdPolicyEnabled,
            artifactIdPolicy,
            result);

        var workItems = CollectWorkItems(repoRoot, config);
        ValidateItems(repoRoot, workItems, result);
        result.WorkItemCount = Math.Max(graph.WorkItems.Count, workItems.Count);

        result.MarkdownFileCount = ValidateMarkdownLinks(repoRoot, config, result, options, scopePrefixes);
        return result;
    }

    private static List<WorkItemRecord> CollectWorkItems(string repoRoot, WorkbenchConfig config)
    {
        var items = new List<WorkItemRecord>();
        var specsRoot = GetSpecsRoot(config);
        foreach (var dir in new[] { Path.Combine(specsRoot, "work-items") })
        {
            var full = Path.Combine(repoRoot, dir);
            if (!Directory.Exists(full))
            {
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (File.Exists(Path.ChangeExtension(file, ".json")))
                {
                    continue;
                }

                items.Add(new WorkItemRecord(file, true));
            }
        }
        return items;
    }

    private static void ValidateItems(string repoRoot, List<WorkItemRecord> items, ValidationResult result)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifactIdPolicyPath = Path.Combine(repoRoot, "artifact-id-policy.json");
        var artifactIdPolicyEnabled = File.Exists(artifactIdPolicyPath);
        string? artifactIdPolicyError = null;
        var artifactIdPolicy = artifactIdPolicyEnabled
            ? ArtifactIdPolicy.Load(repoRoot, out artifactIdPolicyError)
            : ArtifactIdPolicy.Default;
        if (!string.IsNullOrWhiteSpace(artifactIdPolicyError))
        {
            result.Errors.Add(artifactIdPolicyError);
        }
        var canonicalStatuses = new HashSet<string>(SpecTraceMarkdown.CanonicalWorkItemStatuses, StringComparer.OrdinalIgnoreCase);

#pragma warning disable S3267
        foreach (var item in items)
#pragma warning restore S3267
        {
            var content = File.ReadAllText(item.Path);
            if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
            {
                result.Errors.Add($"{item.Path}: {error}");
                continue;
            }

            var data = frontMatter!.Data;
            var artifactType = GetString(data, "artifact_type");
            var artifactIdValue = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
            var isCanonical = item.IsCanonical ||
                              string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase) ||
                              (!string.IsNullOrWhiteSpace(artifactIdValue) &&
                               artifactIdValue.StartsWith("WI-", StringComparison.OrdinalIgnoreCase));

            if (isCanonical)
            {
                var canonicalSchemaErrors = SchemaValidationService.ValidateArtifactFrontMatter(repoRoot, item.Path, data);
                foreach (var schemaError in canonicalSchemaErrors)
                {
                    result.Errors.Add(schemaError);
                }

                var artifactId = artifactIdValue;
                artifactType ??= "work_item";
                var canonicalStatus = GetString(data, "status");
                var title = GetString(data, "title");
                var domain = GetString(data, "domain");
                var owner = GetString(data, "owner");

                if (string.IsNullOrWhiteSpace(artifactId) ||
                    string.IsNullOrWhiteSpace(artifactType) ||
                    string.IsNullOrWhiteSpace(canonicalStatus) ||
                    string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(domain) ||
                    string.IsNullOrWhiteSpace(owner))
                {
                    result.Errors.Add($"{item.Path}: missing required canonical work item fields.");
                }

                if (!string.Equals(artifactType, "work_item", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"{item.Path}: invalid artifact_type '{artifactType ?? "<missing>"}'.");
                }

                if (!string.IsNullOrWhiteSpace(canonicalStatus) && !canonicalStatuses.Contains(canonicalStatus))
                {
                    result.Errors.Add($"{item.Path}: invalid canonical status '{canonicalStatus ?? "<missing>"}'.");
                }

                if (!string.IsNullOrWhiteSpace(artifactId))
                {
                    var artifactTypeForPolicy = artifactType ?? "work_item";
                    var artifactIdForPolicy = artifactId ?? string.Empty;

                    if (!seenIds.Add(artifactIdForPolicy))
                    {
                        result.Errors.Add($"{item.Path}: duplicate artifact_id '{artifactId ?? "<missing>"}'.");
                    }

                    if (artifactIdPolicyEnabled &&
                        !artifactIdPolicy.MatchesArtifactId(artifactTypeForPolicy, artifactIdForPolicy, domain, null))
                    {
                        result.Errors.Add($"{item.Path}: artifact_id '{artifactId ?? "<missing>"}' does not match the configured artifact ID policy.");
                    }
                }

                continue;
            }

            result.Errors.Add($"{item.Path}: legacy work item format is no longer supported.");
        }
    }

    private static void ValidateDocs(
        string repoRoot,
        WorkbenchConfig config,
        ValidationResult result,
        ValidationOptions options)
    {
        var docExcludePrefixes = NormalizePrefixes(config.Validation?.DocExclude);
        var seenArtifactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifactIdPolicyPath = Path.Combine(repoRoot, "artifact-id-policy.json");
        var artifactIdPolicyEnabled = File.Exists(artifactIdPolicyPath);
        string? artifactIdPolicyError = null;
        var artifactIdPolicy = artifactIdPolicyEnabled
            ? ArtifactIdPolicy.Load(repoRoot, out artifactIdPolicyError)
            : ArtifactIdPolicy.Default;
        if (!string.IsNullOrWhiteSpace(artifactIdPolicyError))
        {
            result.Errors.Add(artifactIdPolicyError);
        }
        var specsRoot = GetSpecsRoot(config);
        var architectureRoot = Path.Combine(repoRoot, GetArchitectureRoot(config));
        var requirementsRoot = Path.Combine(repoRoot, specsRoot, "requirements");
        var verificationRoot = Path.Combine(repoRoot, specsRoot, "verification");
        if (!Directory.Exists(requirementsRoot) &&
            !Directory.Exists(architectureRoot) &&
            !Directory.Exists(verificationRoot))
        {
            return;
        }

        foreach (var root in new[] { requirementsRoot, architectureRoot, verificationRoot })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var repoRelative = NormalizeRepoRelative(repoRoot, file);
                if (SpecTraceLayout.IsCanonicalWorkItemPath(repoRelative))
                {
                    continue;
                }

                if (repoRelative.StartsWith("templates/", StringComparison.OrdinalIgnoreCase) ||
                    repoRelative.StartsWith($"{specsRoot}/templates/", StringComparison.OrdinalIgnoreCase) ||
                    repoRelative.StartsWith($"{specsRoot}/schemas/", StringComparison.OrdinalIgnoreCase) ||
                    repoRelative.StartsWith($"{specsRoot}/generated/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (docExcludePrefixes.Count > 0 &&
                    docExcludePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var content = File.ReadAllText(file);
                if (!LooksLikeFrontMatter(content))
                {
                    continue;
                }
                if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
                {
                    result.Errors.Add($"{file}: {error}");
                    continue;
                }

                var data = frontMatter!.Data;
                var artifactType = GetString(data, "artifact_type");
                var repoRelativeSpecs = NormalizeRepoRelative(repoRoot, file);
                if (string.IsNullOrWhiteSpace(artifactType))
                {
                    continue;
                }

                if (string.Equals(artifactType, "specification", StringComparison.OrdinalIgnoreCase) &&
                    !SpecTraceLayout.IsSpecificationRootFile(repoRelativeSpecs))
                {
                    result.Errors.Add($"{file}: specifications must live under '{specsRoot}/requirements/<domain>/'.");
                    continue;
                }

                if (string.Equals(artifactType, "architecture", StringComparison.OrdinalIgnoreCase) &&
                    !SpecTraceLayout.IsCanonicalArchitecturePath(repoRelativeSpecs))
                {
                    result.Errors.Add($"{file}: architecture artifacts must live under '{SpecTraceLayout.ArchitectureRoot}/<domain>/'.");
                    continue;
                }

                if (string.Equals(artifactType, "verification", StringComparison.OrdinalIgnoreCase) &&
                    !SpecTraceLayout.IsCanonicalVerificationPath(repoRelativeSpecs))
                {
                    result.Errors.Add($"{file}: verification artifacts must live under '{SpecTraceLayout.VerificationRoot}/<domain>/'.");
                    continue;
                }

                ValidateCanonicalDoc(
                    repoRoot,
                    file,
                    data,
                    frontMatter!.Body,
                    artifactType,
                    artifactIdPolicyEnabled,
                    artifactIdPolicy,
                    seenArtifactIds,
                    result);
            }
        }
    }

    private static void ValidateCanonicalDoc(
        string repoRoot,
        string file,
        IDictionary<string, object?> data,
        string body,
        string artifactType,
        bool artifactIdPolicyEnabled,
        ArtifactIdPolicy artifactIdPolicy,
        HashSet<string> seenArtifactIds,
        ValidationResult result)
    {
        var artifactId = GetString(data, "artifact_id") ?? GetString(data, "artifactId");
        var domain = GetString(data, "domain");
        var capability = GetString(data, "capability");
        var schemaErrors = SchemaValidationService.ValidateArtifactFrontMatter(repoRoot, file, data);
        foreach (var schemaError in schemaErrors)
        {
            result.Errors.Add(schemaError);
        }

        if (string.IsNullOrWhiteSpace(artifactId))
        {
            result.Errors.Add($"{file}: missing artifact_id for {artifactType} doc.");
        }
        else if (!seenArtifactIds.Add(artifactId))
        {
            result.Errors.Add($"{file}: duplicate artifact_id '{artifactId ?? "<missing>"}'.");
        }

        if (artifactIdPolicyEnabled &&
            !string.IsNullOrWhiteSpace(artifactId) &&
            !artifactIdPolicy.MatchesArtifactId(
                artifactType,
                artifactId,
                domain,
                capability))
        {
            result.Errors.Add($"{file}: artifact_id '{artifactId ?? "<missing>"}' does not match the configured artifact ID policy.");
        }

        if (string.Equals(artifactType, "specification", StringComparison.OrdinalIgnoreCase))
        {
            var requirementClauses = SpecTraceMarkdown.ParseRequirementClauses(body, out var parseErrors);

            foreach (var parseError in parseErrors)
            {
                result.Errors.Add($"{file}: {parseError}");
            }

            if (requirementClauses.Count == 0)
            {
                result.Errors.Add($"{file}: no requirement clauses found in specification body.");
            }

            foreach (var clause in requirementClauses)
            {
                var clauseErrors = SchemaValidationService.ValidateRequirementClause(repoRoot, file, clause);
                foreach (var clauseError in clauseErrors)
                {
                    result.Errors.Add(clauseError);
                }

                if (clause.Trace is not null)
                {
                    var traceData = clause.Trace.ToDictionary(
                        entry => entry.Key,
                        entry => (object?)entry.Value,
                        StringComparer.OrdinalIgnoreCase);
                    var traceErrors = SchemaValidationService.ValidateRequirementTraceFields(repoRoot, file, traceData);
                    foreach (var traceError in traceErrors)
                    {
                        result.Errors.Add(traceError);
                    }
                }
            }
        }
    }

    private static string GetSpecsRoot(WorkbenchConfig config)
    {
        return string.IsNullOrWhiteSpace(config.Paths.SpecsRoot)
            ? SpecTraceLayout.SpecsRoot
            : config.Paths.SpecsRoot;
    }

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string GetArchitectureRoot(WorkbenchConfig config)
    {
        return string.IsNullOrWhiteSpace(config.Paths.ArchitectureDir)
            ? SpecTraceLayout.ArchitectureRoot
            : config.Paths.ArchitectureDir;
    }

    private static int ValidateMarkdownLinks(
        string repoRoot,
        WorkbenchConfig config,
        ValidationResult result,
        ValidationOptions options,
        List<string> scopePrefixes)
    {
        var includePrefixes = NormalizePrefixes(options.LinkInclude);
        var excludePrefixes = NormalizePrefixes(options.LinkExclude);
        var configExcludes = NormalizePrefixes(config.Validation?.LinkExclude);
        if (configExcludes.Count > 0)
        {
            excludePrefixes = excludePrefixes
                .Concat(configExcludes)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        var count = 0;
        foreach (var file in EnumerateMarkdownFiles(repoRoot))
        {
            var repoRelative = NormalizeRepoRelative(repoRoot, file);
            if (!ShouldValidatePath(repoRelative, scopePrefixes, includePrefixes, excludePrefixes))
            {
                continue;
            }
            count++;
            var content = File.ReadAllText(file);
            foreach (var link in ExtractMarkdownLinks(content))
            {
                var target = link;
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (target.Contains("{{", StringComparison.Ordinal) ||
                    target.Contains("}}", StringComparison.Ordinal) ||
                    target.StartsWith("#", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                target = target.Split('#')[0].Split('?')[0];
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                string resolved;
                if (target.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    resolved = Path.Combine(repoRoot, target.TrimStart('/'));
                }
                else
                {
                    var baseDir = Path.GetDirectoryName(file) ?? repoRoot;
                    resolved = Path.GetFullPath(Path.Combine(baseDir, target));
                }

                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    result.AddError(
                        ValidationProfiles.RepoState,
                        ValidationCategories.RepoState,
                        $"broken local link '{link}'.",
                        file: file);
                }
            }
        }
        return count;
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
                    name.Equals(".tools", StringComparison.OrdinalIgnoreCase) ||
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

    private static IEnumerable<string> ExtractMarkdownLinks(string content)
    {
        var matches = Regex.Matches(
            content,
            @"\[[^\]]*\]\((?<link>[^)]+)\)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
#pragma warning disable S3267
        foreach (Match match in matches)
#pragma warning restore S3267
        {
            var linkGroup = match.Groups["link"];
            if (linkGroup.Success)
            {
                yield return linkGroup.Value.Trim();
            }
        }
    }

    private static string NormalizeRepoRelative(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        return relative.TrimStart('/');
    }

    private static List<string> NormalizePrefixes(IList<string>? prefixes)
    {
        if (prefixes is null || prefixes.Count == 0)
        {
            return new List<string>();
        }

        return prefixes
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim().TrimStart('/').Replace('\\', '/').TrimEnd('/'))
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldValidatePath(
        string repoRelative,
        List<string> scopePrefixes,
        List<string> includePrefixes,
        List<string> excludePrefixes)
    {
        if (scopePrefixes.Count > 0 && !scopePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (excludePrefixes.Count > 0 &&
            excludePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (includePrefixes.Count == 0)
        {
            return true;
        }

        return includePrefixes.Any(prefix => repoRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeFrontMatter(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
               trimmed.StartsWith("---\r\n", StringComparison.Ordinal);
    }

    private sealed record WorkItemRecord(string Path, bool IsCanonical);
}
