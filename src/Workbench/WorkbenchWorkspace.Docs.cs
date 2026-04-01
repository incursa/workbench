using System.Collections;
using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    private static readonly string[] managedDocTypes = ["architecture", "verification", "runbook", "doc"];

    public static IReadOnlyList<string> ManagedDocTypes => managedDocTypes;

    public sealed record DocDeleteResult(DocShowData Doc, int ItemsUpdated);

    public DocEditorInput CreateDocEditorInput(RepoDocDetail doc)
    {
        var workbench = GetDocNestedMapForDocs(doc.FrontMatter, "workbench");
        var artifactType = GetDocStringForDocs(doc.FrontMatter, "artifact_type") ?? doc.Summary.Type;
        var isArchitecture = IsArchitectureDocType(artifactType);
        var isVerification = IsVerificationDocType(artifactType);
        var relatedArtifacts = GetDocStringListForDocs(doc.FrontMatter, "related_artifacts");
        if (relatedArtifacts.Count == 0)
        {
            relatedArtifacts = GetDocStringListForDocs(doc.FrontMatter, "related");
        }
        if (relatedArtifacts.Count == 0)
        {
            relatedArtifacts = doc.Summary.RelatedArtifacts.ToList();
        }

        var workItems = isArchitecture || isVerification
            ? []
            : doc.Summary.WorkItems.ToList();
        var codeRefs = isArchitecture || isVerification
            ? []
            : GetDocStringListForDocs(workbench, "codeRefs");

        return new DocEditorInput
        {
            Path = doc.Summary.Path,
            Type = doc.Summary.Type,
            Title = doc.Summary.Title,
            ArtifactId = GetDocStringForDocs(doc.FrontMatter, "artifact_id") ?? GetDocStringForDocs(doc.FrontMatter, "artifactId"),
            Domain = doc.Summary.Domain ?? GetDocStringForDocs(doc.FrontMatter, "domain"),
            Capability = doc.Summary.Capability ?? GetDocStringForDocs(doc.FrontMatter, "capability"),
            Status = GetDocStringForDocs(doc.FrontMatter, "status") ?? GetDocStringForDocs(workbench, "status") ?? doc.Summary.Status,
            Owner = GetDocStringForDocs(doc.FrontMatter, "owner"),
            RelatedArtifacts = string.Join(Environment.NewLine, relatedArtifacts),
            Satisfies = isArchitecture ? string.Join(Environment.NewLine, GetDocStringListForDocs(doc.FrontMatter, "satisfies")) : string.Empty,
            Verifies = isVerification ? string.Join(Environment.NewLine, GetDocStringListForDocs(doc.FrontMatter, "verifies")) : string.Empty,
            // Canonical docs surface work-item references through the doc summary, while legacy docs keep them in
            // workbench/workItems. Using the summary here preserves the user's explicit list semantics when editing.
            WorkItems = string.Join(Environment.NewLine, workItems),
            CodeRefs = string.Join(Environment.NewLine, codeRefs),
            Body = doc.Body
        };
    }

    public DocEditorInput? CreateDocEditorInput(string reference)
    {
        var doc = GetDoc(reference);
        if (doc is null)
        {
            return null;
        }

        if (!IsManagedDocType(doc.Summary.Type))
        {
            return null;
        }

        return CreateDocEditorInput(doc);
    }

    public RepoDocDetail CreateDoc(DocEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new InvalidOperationException("Doc title is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Type))
        {
            throw new InvalidOperationException("Doc type is required.");
        }

        var body = string.IsNullOrWhiteSpace(input.Body)
            ? DocBodyBuilder.BuildSkeleton(input.Type, input.Title)
            : input.Body;

        var isArchitecture = IsArchitectureDocType(input.Type);
        var isVerification = IsVerificationDocType(input.Type);
        var relatedArtifacts = ParseLineList(input.RelatedArtifacts);
        var satisfies = isArchitecture ? ParseLineList(input.Satisfies) : null;
        var verifies = isVerification ? ParseLineList(input.Verifies) : null;
        IList<string> workItems = isArchitecture || isVerification ? Array.Empty<string>() : ParseLineList(input.WorkItems);
        IList<string> codeRefs = isArchitecture || isVerification ? Array.Empty<string>() : ParseLineList(input.CodeRefs);

        var created = DocService.CreateGeneratedDoc(
            RepoRoot,
            Config,
            input.Type,
            input.Title,
            body,
            string.IsNullOrWhiteSpace(input.Path) ? null : input.Path,
            workItems,
            codeRefs,
            Array.Empty<string>(),
            relatedArtifacts,
            string.IsNullOrWhiteSpace(input.Status) ? null : input.Status,
            source: null,
            force: false,
            artifactId: input.ArtifactId,
            domain: input.Domain,
            capability: input.Capability,
            owner: input.Owner,
            satisfies: satisfies,
            verifies: verifies);

        return GetDoc(created.Path) ?? throw new InvalidOperationException("Failed to reload created doc.");
    }

    public DocService.DocEditResult SaveDoc(DocEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Path))
        {
            throw new InvalidOperationException("Doc path is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new InvalidOperationException("Doc title is required.");
        }

        var currentDoc = GetDoc(input.Path) ?? throw new InvalidOperationException("Doc not found.");
        var relatedArtifacts = ParseLineList(input.RelatedArtifacts);

        if (IsArchitectureDocType(currentDoc.Summary.Type))
        {
            return DocService.EditDoc(
                RepoRoot,
                Config,
                input.Path,
                input.ArtifactId,
                input.Title,
                string.IsNullOrWhiteSpace(input.Status) ? null : input.Status,
                input.Owner,
                input.Domain,
                input.Capability,
                string.IsNullOrWhiteSpace(input.Body) ? null : input.Body,
                ParseLineList(input.Satisfies),
                null,
                relatedArtifacts,
                null,
                null,
                null);
        }

        if (IsVerificationDocType(currentDoc.Summary.Type))
        {
            return DocService.EditDoc(
                RepoRoot,
                Config,
                input.Path,
                input.ArtifactId,
                input.Title,
                string.IsNullOrWhiteSpace(input.Status) ? null : input.Status,
                input.Owner,
                input.Domain,
                input.Capability,
                string.IsNullOrWhiteSpace(input.Body) ? null : input.Body,
                null,
                ParseLineList(input.Verifies),
                relatedArtifacts,
                null,
                null,
                null);
        }

        var workItems = ParseLineList(input.WorkItems);
        return DocService.EditDoc(
            RepoRoot,
            Config,
            input.Path,
            input.ArtifactId,
            input.Title,
            string.IsNullOrWhiteSpace(input.Status) ? null : input.Status,
            input.Owner,
            input.Domain,
            input.Capability,
            string.IsNullOrWhiteSpace(input.Body) ? null : input.Body,
            relatedArtifacts,
            null,
            workItems,
            ParseLineList(input.CodeRefs));
    }

    public DocDeleteResult DeleteDoc(string reference, bool keepLinks = false)
    {
        var doc = DocService.GetDocShowData(RepoRoot, Config, reference);
        var docFullPath = Path.GetFullPath(doc.Path);
        var itemsUpdated = 0;
        if (!keepLinks)
        {
            var items = WorkItemService.ListItems(RepoRoot, Config, includeDone: true).Items;
            foreach (var item in items)
            {
                var itemChanged = false;
                foreach (var spec in item.Related.Specs)
                {
                    var specPath = Path.GetFullPath(DocService.ResolveDocPath(RepoRoot, spec));
                    if (specPath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase) &&
                        WorkItemService.RemoveRelatedLink(item.Path, "specs", spec))
                    {
                        itemChanged = true;
                    }
                }

                foreach (var file in item.Related.Files)
                {
                    var filePath = Path.GetFullPath(DocService.ResolveDocPath(RepoRoot, file));
                    if (filePath.Equals(docFullPath, StringComparison.OrdinalIgnoreCase) &&
                        WorkItemService.RemoveRelatedLink(item.Path, "files", file))
                    {
                        itemChanged = true;
                    }
                }

                if (itemChanged)
                {
                    itemsUpdated++;
                }
            }
        }

        File.Delete(docFullPath);
        return new DocDeleteResult(doc, itemsUpdated);
    }

    private static bool IsManagedDocType(string type)
    {
        return ManagedDocTypes.Any(entry => string.Equals(entry, type, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ParseLineList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' ').Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsArchitectureDocType(string? type)
    {
        return string.Equals(type, "architecture", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVerificationDocType(string? type)
    {
        return string.Equals(type, "verification", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> GetDocNestedMapForDocs(IReadOnlyDictionary<string, object?> data, string key)
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

    private static string? GetDocStringForDocs(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static List<string> GetDocStringListForDocs(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>()
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToList();
        }

        return [];
    }
}
