using System.Collections;
using System.Text;
using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    public SpecEditorInput? CreateSpecEditorInput(string reference)
    {
        var doc = GetDoc(reference);
        if (doc is null ||
            !(string.Equals(doc.Summary.Type, "spec", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(doc.Summary.Type, "specification", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return CreateSpecEditorInput(doc);
    }

    public SpecEditorInput CreateSpecEditorInput(RepoDocDetail doc)
    {
        var workbench = GetDocNestedMap(doc.FrontMatter, "workbench");
        var purpose = ExtractSection(doc.Body, "Purpose");
        if (string.IsNullOrWhiteSpace(purpose))
        {
            purpose = ExtractSection(doc.Body, "Summary");
        }

        var relatedArtifacts = GetDocStringList(doc.FrontMatter, "related_artifacts");
        if (relatedArtifacts.Count == 0)
        {
            relatedArtifacts = doc.Summary.WorkItems.ToList();
        }

        return new SpecEditorInput
        {
            Path = doc.Summary.Path,
            ArtifactId = GetDocString(doc.FrontMatter, "artifact_id") ?? GetDocString(doc.FrontMatter, "artifactId"),
            Domain = GetDocString(doc.FrontMatter, "domain"),
            Capability = GetDocString(doc.FrontMatter, "capability"),
            Title = GetDocString(doc.FrontMatter, "title") ?? doc.Summary.Title,
            Status = GetDocString(doc.FrontMatter, "status") ?? GetDocString(workbench, "status") ?? "draft",
            Owner = GetDocString(doc.FrontMatter, "owner"),
            Purpose = purpose,
            Summary = purpose,
            Scope = ExtractSection(doc.Body, "Scope"),
            Context = ExtractSection(doc.Body, "Context"),
            Requirements = SpecTraceMarkdown.ExtractRequirementBlocks(doc.Body),
            RelatedArtifacts = string.Join(Environment.NewLine, relatedArtifacts),
            RelatedArchitectureDocs = string.Join(Environment.NewLine, relatedArtifacts),
            RelatedWorkItems = string.Join(Environment.NewLine, relatedArtifacts),
            OpenQuestions = ExtractSection(doc.Body, "Open Questions"),
            CodeRefs = string.Join(Environment.NewLine, GetDocStringList(workbench, "codeRefs"))
        };
    }

    public string GetSpecIdPolicySummary()
    {
        var policyPath = Path.Combine(RepoRoot, "artifact-id-policy.json");
        var policy = ArtifactIdPolicy.Load(RepoRoot, out var error);
        var specTemplate = policy.GetTemplateForDocType("specification")
            ?? ArtifactIdPolicy.Default.GetTemplateForDocType("specification")
            ?? "SPEC-{domain}{grouping}";

        if (File.Exists(policyPath))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return $"Custom spec IDs are enabled via artifact-id-policy.json. Leave Artifact ID blank and provide Domain/Capability so Workbench can use {specTemplate}.";
            }

            return $"artifact-id-policy.json is present, but it could not be parsed. Workbench will fall back to default spec IDs ({specTemplate}).";
        }

        return $"Default spec IDs use {specTemplate}. Add artifact-id-policy.json to customize the structure.";
    }

    public RepoDocDetail CreateSpec(SpecEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new InvalidOperationException("Spec title is required.");
        }

        var body = BuildSpecBody(input);
        var workItemIds = ParseWorkItemIds(input);
        var relatedArtifacts = ParseArtifactIds(input);
        var created = DocService.CreateGeneratedDoc(
            RepoRoot,
            Config,
            "specification",
            input.Title,
            body,
            input.Path,
            workItemIds,
            Array.Empty<string>(),
            Array.Empty<string>(),
            relatedArtifacts,
            input.Status,
            source: null,
            force: false,
            artifactId: input.ArtifactId,
            domain: input.Domain,
            capability: input.Capability,
            owner: input.Owner);

        return GetDoc(created.Path) ?? throw new InvalidOperationException("Failed to reload created spec.");
    }

    public DocService.DocEditResult SaveSpec(SpecEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Path))
        {
            throw new InvalidOperationException("Spec path is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new InvalidOperationException("Spec title is required.");
        }

        var body = BuildSpecBody(input);
        var relatedArtifacts = ParseArtifactIds(input);

        return DocService.EditDoc(
            RepoRoot,
            Config,
            input.Path,
            input.ArtifactId,
            input.Title,
            input.Status,
            input.Owner,
            input.Domain,
            input.Capability,
            body,
            relatedArtifacts,
            null);
    }

    private static string BuildSpecBody(SpecEditorInput input)
    {
        var purpose = string.IsNullOrWhiteSpace(input.Purpose) ? input.Summary : input.Purpose;
        var requirements = string.IsNullOrWhiteSpace(input.Requirements)
            ? SpecTraceMarkdown.BuildRequirementSkeleton()
            : input.Requirements;
        var body = SpecTraceMarkdown.BuildSpecificationBody(
            input.Title.Trim(),
            purpose,
            input.Scope,
            input.Context,
            requirements,
            string.IsNullOrWhiteSpace(input.ArtifactId) ? null : input.ArtifactId.Trim());

        if (!string.IsNullOrWhiteSpace(input.OpenQuestions))
        {
            body = body.TrimEnd() + Environment.NewLine + Environment.NewLine +
                string.Join(
                    Environment.NewLine,
                    "## Open Questions",
                    string.Empty,
                    input.OpenQuestions.TrimEnd()) + Environment.NewLine;
        }

        return body;
    }

    private static List<string> ParseArtifactIds(SpecEditorInput input)
    {
        var candidates = new List<string>();
        AddLines(candidates, input.RelatedArtifacts);

        if (candidates.Count == 0)
        {
            AddLines(candidates, input.RelatedWorkItems);
            AddLines(candidates, input.RelatedArchitectureDocs);
        }

        return candidates
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseWorkItemIds(SpecEditorInput input)
    {
        var candidates = new List<string>();
        AddLines(candidates, input.RelatedWorkItems);

        return candidates
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddLines(List<string> output, string text)
    {
        foreach (var line in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = line.TrimStart('-', '*', ' ').Trim();
            if (trimmed.Length > 0)
            {
                output.Add(trimmed);
            }
        }
    }

    private static Dictionary<string, object?> GetDocNestedMap(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<string, object?> typed)
        {
            return new Dictionary<string, object?>(typed, StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<object, object> legacy)
        {
            return legacy.ToDictionary(
                kvp => kvp.Key.ToString() ?? string.Empty,
                kvp => (object?)kvp.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetDocString(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static List<string> GetDocStringList(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return new List<string>();
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        return new List<string>();
    }
}
