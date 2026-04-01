using System.Collections;
using System.Text.Json;
using Workbench.Core;

namespace Workbench;

public sealed partial class WorkbenchWorkspace
{
    private static readonly string[] specTopLevelKeys =
    [
        "$schema",
        "artifact_id",
        "artifact_type",
        "title",
        "domain",
        "capability",
        "status",
        "owner",
        "tags",
        "related_artifacts",
        "purpose",
        "scope",
        "context",
        "open_questions",
        "supplemental_sections",
        "requirements"
    ];

    private static readonly string[] requirementKeys =
    [
        "id",
        "title",
        "statement",
        "trace",
        "notes"
    ];

    private static readonly string[] requirementTraceKeys =
    [
        "satisfied_by",
        "implemented_by",
        "verified_by",
        "derived_from",
        "supersedes",
        "upstream_refs",
        "related"
    ];

    private static readonly string[] supplementalSectionKeys =
    [
        "heading",
        "content"
    ];

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
        var input = doc.Summary.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? CreateSpecEditorInputFromJson(doc)
            : CreateSpecEditorInputFromMarkdown(doc);

        EnsureEditorCollections(input);
        return input;
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

    public string GetPreferredSpecFormat()
    {
        return CanonicalArtifactDiscovery
            .EnumerateCanonicalSources(RepoRoot, Config)
            .Any(source =>
                string.Equals(source.Format, "json", StringComparison.OrdinalIgnoreCase) &&
                source.SourceRepoRelativePath.StartsWith($"{SpecTraceLayout.RequirementsRoot}/", StringComparison.OrdinalIgnoreCase))
            ? "json"
            : "markdown";
    }

    public RepoDocDetail CreateSpec(SpecEditorInput input)
    {
        EnsureEditorCollections(input);
        ValidateSpecEditorInput(input);

        return string.Equals(input.SourceFormat, "json", StringComparison.OrdinalIgnoreCase)
            ? CreateCanonicalJsonSpec(input)
            : CreateMarkdownSpec(input);
    }

    public DocService.DocEditResult SaveSpec(SpecEditorInput input)
    {
        EnsureEditorCollections(input);
        ValidateSpecEditorInput(input);

        if (string.IsNullOrWhiteSpace(input.Path))
        {
            throw new InvalidOperationException("Spec path is required.");
        }

        return input.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.SourceFormat, "json", StringComparison.OrdinalIgnoreCase)
            ? SaveCanonicalJsonSpec(input)
            : SaveMarkdownSpec(input);
    }

    private static void EnsureEditorCollections(SpecEditorInput input)
    {
        input.Requirements ??= [];
        input.SupplementalSections ??= [];

        if (input.Requirements.Count == 0)
        {
            input.Requirements.Add(SpecRequirementEditorInput.CreateBlank());
        }

        foreach (var requirement in input.Requirements)
        {
            requirement.Trace ??= new SpecRequirementTraceEditorInput();
        }
    }

    private SpecEditorInput CreateSpecEditorInputFromJson(RepoDocDetail doc)
    {
        var requirements = GetDocObjectList(doc.FrontMatter, "requirements")
            .Select(CreateRequirementEditorInputFromJson)
            .ToList();
        var supplementalSections = GetDocObjectList(doc.FrontMatter, "supplemental_sections")
            .Select(CreateSupplementalSectionEditorInput)
            .ToList();

        return new SpecEditorInput
        {
            Path = doc.Summary.Path,
            SourceFormat = "json",
            SchemaReference = GetDocString(doc.FrontMatter, "$schema") ?? string.Empty,
            ExtensionJson = SerializeExtensionFields(doc.FrontMatter, specTopLevelKeys),
            ArtifactId = GetDocString(doc.FrontMatter, "artifact_id") ?? GetDocString(doc.FrontMatter, "artifactId") ?? doc.Summary.ArtifactId,
            Domain = GetDocString(doc.FrontMatter, "domain") ?? doc.Summary.Domain,
            Capability = GetDocString(doc.FrontMatter, "capability") ?? doc.Summary.Capability,
            Title = GetDocString(doc.FrontMatter, "title") ?? doc.Summary.Title,
            Status = GetDocString(doc.FrontMatter, "status") ?? doc.Summary.Status,
            Owner = GetDocString(doc.FrontMatter, "owner"),
            Purpose = GetDocString(doc.FrontMatter, "purpose") ?? string.Empty,
            Scope = GetDocString(doc.FrontMatter, "scope") ?? string.Empty,
            Context = GetDocString(doc.FrontMatter, "context") ?? string.Empty,
            TagsText = string.Join(Environment.NewLine, GetDocStringList(doc.FrontMatter, "tags")),
            RelatedArtifactsText = string.Join(Environment.NewLine, GetDocStringList(doc.FrontMatter, "related_artifacts")),
            OpenQuestionsText = string.Join(Environment.NewLine, GetDocStringList(doc.FrontMatter, "open_questions")),
            SupplementalSections = supplementalSections,
            Requirements = requirements
        };
    }

    private SpecEditorInput CreateSpecEditorInputFromMarkdown(RepoDocDetail doc)
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
            relatedArtifacts = doc.Summary.RelatedArtifacts.ToList();
        }

        var tags = GetDocStringList(doc.FrontMatter, "tags");
        if (tags.Count == 0)
        {
            tags = doc.Summary.Tags.ToList();
        }

        var requirements = SpecTraceMarkdown.ParseRequirementClauses(doc.Body, out _)
            .Select(CreateRequirementEditorInputFromMarkdown)
            .ToList();

        return new SpecEditorInput
        {
            Path = doc.Summary.Path,
            SourceFormat = "markdown",
            ExtensionJson = SerializeExtensionFields(doc.FrontMatter, Array.Empty<string>()),
            ArtifactId = GetDocString(doc.FrontMatter, "artifact_id") ?? GetDocString(doc.FrontMatter, "artifactId") ?? doc.Summary.ArtifactId,
            Domain = GetDocString(doc.FrontMatter, "domain") ?? doc.Summary.Domain,
            Capability = GetDocString(doc.FrontMatter, "capability") ?? doc.Summary.Capability,
            Title = GetDocString(doc.FrontMatter, "title") ?? doc.Summary.Title,
            Status = GetDocString(doc.FrontMatter, "status") ?? GetDocString(workbench, "status") ?? doc.Summary.Status,
            Owner = GetDocString(doc.FrontMatter, "owner"),
            Purpose = purpose,
            Scope = ExtractSection(doc.Body, "Scope"),
            Context = ExtractSection(doc.Body, "Context"),
            TagsText = string.Join(Environment.NewLine, tags),
            RelatedArtifactsText = string.Join(Environment.NewLine, relatedArtifacts),
            OpenQuestionsText = ExtractSection(doc.Body, "Open Questions"),
            Requirements = requirements
        };
    }

    private RepoDocDetail CreateMarkdownSpec(SpecEditorInput input)
    {
        var body = BuildSpecBody(input);
        var relatedArtifacts = ParseLineList(input.RelatedArtifactsText);
        var tags = ParseLineList(input.TagsText);
        var workItemIds = ParseWorkItemIds(relatedArtifacts);
        var created = DocService.CreateGeneratedDoc(
            RepoRoot,
            Config,
            "specification",
            input.Title,
            body,
            input.Path,
            workItemIds,
            Array.Empty<string>(),
            tags,
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

    private DocService.DocEditResult SaveMarkdownSpec(SpecEditorInput input)
    {
        var body = BuildSpecBody(input);
        var relatedArtifacts = ParseLineList(input.RelatedArtifactsText);
        var tags = ParseLineList(input.TagsText);

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
            tags,
            null,
            null);
    }

    private RepoDocDetail CreateCanonicalJsonSpec(SpecEditorInput input)
    {
        var createdPath = ResolveJsonSpecPath(input, pathOverride: null);
        var json = BuildJsonSpec(input, createdPath);
        Directory.CreateDirectory(Path.GetDirectoryName(createdPath) ?? RepoRoot);
        File.WriteAllText(createdPath, json);
        SyncSpecWorkItemLinks(null, ParseWorkItemIds(ParseLineList(input.RelatedArtifactsText)), createdPath);
        return GetDoc(createdPath) ?? throw new InvalidOperationException("Failed to reload created spec.");
    }

    private DocService.DocEditResult SaveCanonicalJsonSpec(SpecEditorInput input)
    {
        var existing = GetDoc(input.Path) ?? throw new InvalidOperationException("Spec not found.");
        var savedPath = ResolveJsonSpecPath(input, input.Path);
        var json = BuildJsonSpec(input, savedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(savedPath) ?? RepoRoot);
        File.WriteAllText(savedPath, json);
        SyncSpecWorkItemLinks(existing, ParseWorkItemIds(ParseLineList(input.RelatedArtifactsText)), savedPath);

        return new DocService.DocEditResult(
            savedPath,
            DocService.NormalizeArtifactId(input.ArtifactId),
            ArtifactIdUpdated: false,
            TitleUpdated: false,
            StatusUpdated: false,
            OwnerUpdated: false,
            DomainUpdated: false,
            CapabilityUpdated: false,
            BodyUpdated: true,
            RelatedArtifactsUpdated: true,
            WorkItemsUpdated: false,
            CodeRefsUpdated: false);
    }

    private string ResolveJsonSpecPath(SpecEditorInput input, string? pathOverride)
    {
        if (!string.IsNullOrWhiteSpace(pathOverride))
        {
            var explicitPath = Path.IsPathRooted(pathOverride)
                ? pathOverride
                : Path.Combine(RepoRoot, pathOverride);
            return EnsureJsonExtension(Path.GetFullPath(explicitPath));
        }

        var artifactId = ResolveSpecArtifactId(input);
        var domain = NormalizeJsonDomain(input.Domain);
        input.Domain = domain;
        return Path.Combine(RepoRoot, SpecTraceLayout.RequirementsRoot, domain, $"{artifactId}.json");
    }

    private string BuildJsonSpec(SpecEditorInput input, string targetPath)
    {
        var data = BuildJsonSpecObject(input, ResolveSpecArtifactId(input));
        var json = JsonWriter.Serialize(data);
        var errors = SchemaValidationService.ValidateCanonicalArtifactJson(RepoRoot, targetPath, json);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        return json;
    }

    private static Dictionary<string, object?> BuildJsonSpecObject(SpecEditorInput input, string artifactId)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ApplyExtensionJson(data, input.ExtensionJson, specTopLevelKeys);

        SetOrRemove(data, "$schema", input.SchemaReference);
        data["artifact_id"] = artifactId;
        data["artifact_type"] = "specification";
        data["title"] = RequireNonEmpty(input.Title, "Title");
        data["domain"] = NormalizeJsonDomain(input.Domain);
        data["capability"] = RequireNonEmpty(input.Capability, "Capability");
        data["status"] = RequireNonEmpty(input.Status, "Status");
        data["owner"] = RequireNonEmpty(input.Owner, "Owner");
        data["purpose"] = RequireNonEmpty(input.Purpose, "Purpose");
        SetOrRemove(data, "scope", input.Scope);
        SetOrRemove(data, "context", input.Context);
        SetOrRemoveList(data, "tags", ParseLineList(input.TagsText));
        SetOrRemoveList(data, "related_artifacts", ParseLineList(input.RelatedArtifactsText));
        SetOrRemoveList(data, "open_questions", ParseLineList(input.OpenQuestionsText));
        SetOrRemoveObjectList(
            data,
            "supplemental_sections",
            input.SupplementalSections
                .Where(section => !string.IsNullOrWhiteSpace(section.Heading) || !string.IsNullOrWhiteSpace(section.Content))
                .Select(BuildSupplementalSectionObject)
                .ToList());
        data["requirements"] = input.Requirements
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Id) ||
                                  !string.IsNullOrWhiteSpace(requirement.Title) ||
                                  !string.IsNullOrWhiteSpace(requirement.Statement) ||
                                  !string.IsNullOrWhiteSpace(requirement.NotesText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.SatisfiedByText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.ImplementedByText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.VerifiedByText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.DerivedFromText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.SupersedesText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.UpstreamRefsText) ||
                                  !string.IsNullOrWhiteSpace(requirement.Trace.RelatedText))
            .Select(BuildRequirementObject)
            .Cast<object?>()
            .ToList();

        return data;
    }

    private static Dictionary<string, object?> BuildRequirementObject(SpecRequirementEditorInput requirement)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ApplyExtensionJson(data, requirement.ExtensionJson, requirementKeys);
        data["id"] = RequireNonEmpty(requirement.Id, "Requirement ID");
        data["title"] = RequireNonEmpty(requirement.Title, "Requirement title");
        data["statement"] = RequireNonEmpty(requirement.Statement, "Requirement statement");
        SetOrRemoveList(data, "notes", ParseLineList(requirement.NotesText));

        var trace = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ApplyExtensionJson(trace, requirement.Trace.ExtensionJson, requirementTraceKeys);
        SetOrRemoveList(trace, "satisfied_by", ParseLineList(requirement.Trace.SatisfiedByText));
        SetOrRemoveList(trace, "implemented_by", ParseLineList(requirement.Trace.ImplementedByText));
        SetOrRemoveList(trace, "verified_by", ParseLineList(requirement.Trace.VerifiedByText));
        SetOrRemoveList(trace, "derived_from", ParseLineList(requirement.Trace.DerivedFromText));
        SetOrRemoveList(trace, "supersedes", ParseLineList(requirement.Trace.SupersedesText));
        SetOrRemoveList(trace, "upstream_refs", ParseLineList(requirement.Trace.UpstreamRefsText));
        SetOrRemoveList(trace, "related", ParseLineList(requirement.Trace.RelatedText));
        SetOrRemoveObject(data, "trace", trace);
        return data;
    }

    private static Dictionary<string, object?> BuildSupplementalSectionObject(SpecSupplementalSectionEditorInput section)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        ApplyExtensionJson(data, section.ExtensionJson, supplementalSectionKeys);
        data["heading"] = RequireNonEmpty(section.Heading, "Supplemental section heading");
        data["content"] = RequireNonEmpty(section.Content, "Supplemental section content");
        return data;
    }

    private void SyncSpecWorkItemLinks(RepoDocDetail? previousDoc, IReadOnlyList<string> currentWorkItems, string savedPath)
    {
        var previousWorkItems = previousDoc is null ? [] : FilterWorkItemArtifactIds(previousDoc.Summary.RelatedArtifacts);
        var relativeSpecPath = string.Concat(
            Path.AltDirectorySeparatorChar,
            Path.GetRelativePath(RepoRoot, savedPath).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (var workItemId in currentWorkItems.Except(previousWorkItems, StringComparer.OrdinalIgnoreCase))
        {
            WorkItemService.AddRelatedLink(WorkItemService.GetItemPathById(RepoRoot, Config, workItemId), "specs", relativeSpecPath);
        }

        foreach (var workItemId in previousWorkItems.Except(currentWorkItems, StringComparer.OrdinalIgnoreCase))
        {
            WorkItemService.RemoveRelatedLink(WorkItemService.GetItemPathById(RepoRoot, Config, workItemId), "specs", relativeSpecPath);
        }
    }

    private static SpecRequirementEditorInput CreateRequirementEditorInputFromJson(Dictionary<string, object?> data)
    {
        var trace = GetDocNestedMap(data, "trace");
        return new SpecRequirementEditorInput
        {
            Id = GetDocString(data, "id") ?? string.Empty,
            Title = GetDocString(data, "title") ?? string.Empty,
            Statement = GetDocString(data, "statement") ?? string.Empty,
            NotesText = string.Join(Environment.NewLine, GetDocStringList(data, "notes")),
            ExtensionJson = SerializeExtensionFields(data, requirementKeys),
            Trace = new SpecRequirementTraceEditorInput
            {
                SatisfiedByText = string.Join(Environment.NewLine, GetDocStringList(trace, "satisfied_by")),
                ImplementedByText = string.Join(Environment.NewLine, GetDocStringList(trace, "implemented_by")),
                VerifiedByText = string.Join(Environment.NewLine, GetDocStringList(trace, "verified_by")),
                DerivedFromText = string.Join(Environment.NewLine, GetDocStringList(trace, "derived_from")),
                SupersedesText = string.Join(Environment.NewLine, GetDocStringList(trace, "supersedes")),
                UpstreamRefsText = string.Join(Environment.NewLine, GetDocStringList(trace, "upstream_refs")),
                RelatedText = string.Join(Environment.NewLine, GetDocStringList(trace, "related")),
                ExtensionJson = SerializeExtensionFields(trace, requirementTraceKeys)
            }
        };
    }

    private static SpecRequirementEditorInput CreateRequirementEditorInputFromMarkdown(SpecTraceMarkdown.RequirementClause clause)
    {
        var trace = clause.Trace ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        return new SpecRequirementEditorInput
        {
            Id = clause.RequirementId,
            Title = clause.Title,
            Statement = clause.Clause,
            NotesText = string.Join(Environment.NewLine, clause.Notes ?? Array.Empty<string>()),
            Trace = new SpecRequirementTraceEditorInput
            {
                SatisfiedByText = JoinTraceValues(trace, "Satisfied By"),
                ImplementedByText = JoinTraceValues(trace, "Implemented By"),
                VerifiedByText = JoinTraceValues(trace, "Verified By"),
                DerivedFromText = JoinTraceValues(trace, "Derived From"),
                SupersedesText = JoinTraceValues(trace, "Supersedes"),
                UpstreamRefsText = JoinTraceValues(trace, "Source Refs"),
                RelatedText = JoinTraceValues(trace, "Related")
            }
        };
    }

    private static SpecSupplementalSectionEditorInput CreateSupplementalSectionEditorInput(Dictionary<string, object?> data)
    {
        return new SpecSupplementalSectionEditorInput
        {
            Heading = GetDocString(data, "heading") ?? string.Empty,
            Content = GetDocString(data, "content") ?? string.Empty,
            ExtensionJson = SerializeExtensionFields(data, supplementalSectionKeys)
        };
    }

    private static string BuildSpecBody(SpecEditorInput input)
    {
        var requirementBlocks = BuildMarkdownRequirementBlocks(input.Requirements);
        var body = SpecTraceMarkdown.BuildSpecificationBody(
            input.Title.Trim(),
            input.Purpose,
            input.Scope,
            input.Context,
            requirementBlocks,
            string.IsNullOrWhiteSpace(input.ArtifactId) ? null : input.ArtifactId.Trim());

        var trailingSections = new List<string>();
        var openQuestions = ParseLineList(input.OpenQuestionsText);
        if (openQuestions.Count > 0)
        {
            trailingSections.Add("## Open Questions");
            trailingSections.Add(string.Empty);
            trailingSections.AddRange(openQuestions.Select(question => $"- {question}"));
        }

        foreach (var section in input.SupplementalSections.Where(section => !string.IsNullOrWhiteSpace(section.Heading) && !string.IsNullOrWhiteSpace(section.Content)))
        {
            if (trailingSections.Count > 0)
            {
                trailingSections.Add(string.Empty);
            }

            trailingSections.Add($"## {section.Heading.Trim()}");
            trailingSections.Add(string.Empty);
            trailingSections.Add(section.Content.Trim());
        }

        if (trailingSections.Count == 0)
        {
            return body;
        }

        return body.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, trailingSections).TrimEnd() + Environment.NewLine;
    }

    private static string BuildMarkdownRequirementBlocks(IEnumerable<SpecRequirementEditorInput> requirements)
    {
        var blocks = requirements
            .Where(HasMeaningfulRequirementContent)
            .Select(BuildMarkdownRequirementSection)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        return blocks.Count == 0
            ? SpecTraceMarkdown.BuildRequirementSkeleton()
            : string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    private static string BuildMarkdownRequirementSection(SpecRequirementEditorInput requirement)
    {
        var trace = BuildMarkdownRequirementTrace(requirement.Trace);
        var notes = ParseLineList(requirement.NotesText);
        var clause = new SpecTraceMarkdown.RequirementClause(
            RequireNonEmpty(requirement.Id, "Requirement ID"),
            RequireNonEmpty(requirement.Title, "Requirement title"),
            RequireNonEmpty(requirement.Statement, "Requirement statement"),
            ExtractNormativeKeyword(requirement.Statement),
            trace.Count == 0 ? null : trace,
            notes.Count == 0 ? null : notes);

        return SpecTraceMarkdown.BuildRequirementSection(clause);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildMarkdownRequirementTrace(SpecRequirementTraceEditorInput trace)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        AddTraceValues(map, "Satisfied By", ParseLineList(trace.SatisfiedByText));
        AddTraceValues(map, "Implemented By", ParseLineList(trace.ImplementedByText));
        AddTraceValues(map, "Verified By", ParseLineList(trace.VerifiedByText));
        AddTraceValues(map, "Derived From", ParseLineList(trace.DerivedFromText));
        AddTraceValues(map, "Supersedes", ParseLineList(trace.SupersedesText));
        AddTraceValues(map, "Source Refs", ParseLineList(trace.UpstreamRefsText));
        AddTraceValues(map, "Related", ParseLineList(trace.RelatedText));

        return map;
    }

    private static void AddTraceValues(IDictionary<string, IReadOnlyList<string>> output, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            output[label] = values;
        }
    }

    private static string ExtractNormativeKeyword(string? statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return string.Empty;
        }

        foreach (var keyword in new[] { "MUST NOT", "SHALL NOT", "SHOULD NOT", "MUST", "SHALL", "SHOULD", "MAY" })
        {
            if (statement.Contains(keyword, StringComparison.Ordinal))
            {
                return keyword;
            }
        }

        return string.Empty;
    }

    private static string JoinTraceValues(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? trace,
        string label)
    {
        if (trace is null || !trace.TryGetValue(label, out var values) || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, values);
    }

    private static void ValidateSpecEditorInput(SpecEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new InvalidOperationException("Specification title is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Domain))
        {
            throw new InvalidOperationException("Specification domain is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Capability))
        {
            throw new InvalidOperationException("Specification capability is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Status))
        {
            throw new InvalidOperationException("Specification status is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Owner))
        {
            throw new InvalidOperationException("Specification owner is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Purpose))
        {
            throw new InvalidOperationException("Specification purpose is required.");
        }

        var populatedRequirements = input.Requirements.Where(HasMeaningfulRequirementContent).ToList();
        if (populatedRequirements.Count == 0)
        {
            throw new InvalidOperationException("At least one requirement is required.");
        }

        foreach (var requirement in populatedRequirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.Id))
            {
                throw new InvalidOperationException("Each requirement needs an ID.");
            }

            if (string.IsNullOrWhiteSpace(requirement.Title))
            {
                throw new InvalidOperationException($"Requirement '{requirement.Id}' needs a title.");
            }

            if (string.IsNullOrWhiteSpace(requirement.Statement))
            {
                throw new InvalidOperationException($"Requirement '{requirement.Id}' needs a statement.");
            }

            var clause = new SpecTraceMarkdown.RequirementClause(
                requirement.Id.Trim(),
                requirement.Title.Trim(),
                requirement.Statement.Trim(),
                ExtractNormativeKeyword(requirement.Statement),
                null,
                null);
            var errors = SchemaValidationService.ValidateRequirementClause(string.Empty, requirement.Id, clause);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }
        }
    }

    private static bool HasMeaningfulRequirementContent(SpecRequirementEditorInput requirement)
    {
        return !string.IsNullOrWhiteSpace(requirement.Id) ||
            !string.IsNullOrWhiteSpace(requirement.Title) ||
            !string.IsNullOrWhiteSpace(requirement.Statement) ||
            !string.IsNullOrWhiteSpace(requirement.NotesText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.SatisfiedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.ImplementedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.VerifiedByText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.DerivedFromText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.SupersedesText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.UpstreamRefsText) ||
            !string.IsNullOrWhiteSpace(requirement.Trace.RelatedText);
    }

    private static List<string> ParseWorkItemIds(IEnumerable<string> artifactIds)
    {
        return artifactIds
            .Where(entry => entry.StartsWith("WI-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RequireNonEmpty(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return value.Trim();
    }

    private string ResolveSpecArtifactId(SpecEditorInput input)
    {
        var artifactId = DocService.NormalizeArtifactId(input.ArtifactId);
        if (!string.IsNullOrWhiteSpace(artifactId))
        {
            input.ArtifactId = artifactId;
            return artifactId;
        }

        artifactId = DocService.TryGenerateArtifactId(RepoRoot, Config, "specification", input.Title, input.Domain, input.Capability);
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            throw new InvalidOperationException("Artifact ID is required for specification editing.");
        }

        input.ArtifactId = artifactId;
        return artifactId;
    }

    private static string NormalizeJsonDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new List<char>(value.Length);
        var previousDash = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                normalized.Add(ch);
                previousDash = false;
            }
            else if (!previousDash && normalized.Count > 0)
            {
                normalized.Add('-');
                previousDash = true;
            }
        }

        while (normalized.Count > 0 && normalized[^1] == '-')
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return new string(normalized.ToArray());
    }

    private static string EnsureJsonExtension(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : Path.ChangeExtension(path, ".json");
    }

    private static void SetOrRemove(IDictionary<string, object?> data, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = data.Remove(key);
            return;
        }

        data[key] = value.Trim();
    }

    private static void SetOrRemoveList(IDictionary<string, object?> data, string key, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            _ = data.Remove(key);
            return;
        }

        data[key] = values.Cast<object?>().ToList();
    }

    private static void SetOrRemoveObject(IDictionary<string, object?> data, string key, IReadOnlyDictionary<string, object?> value)
    {
        if (value.Count == 0)
        {
            _ = data.Remove(key);
            return;
        }

        data[key] = new Dictionary<string, object?>(value, StringComparer.OrdinalIgnoreCase);
    }

    private static void SetOrRemoveObjectList(IDictionary<string, object?> data, string key, IReadOnlyList<Dictionary<string, object?>> values)
    {
        if (values.Count == 0)
        {
            _ = data.Remove(key);
            return;
        }

        data[key] = values.Cast<object?>().ToList();
    }

    private static void ApplyExtensionJson(IDictionary<string, object?> target, string extensionJson, IEnumerable<string> reservedKeys)
    {
        if (string.IsNullOrWhiteSpace(extensionJson))
        {
            return;
        }

        Dictionary<string, object?>? data;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, object?>>(extensionJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Extension JSON must be a valid JSON object. {ex.Message}", ex);
        }

        if (data is null)
        {
            return;
        }

        var reserved = new HashSet<string>(reservedKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in data)
        {
            if (!key.StartsWith("x_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Extension JSON key '{key}' must start with 'x_'.");
            }

            if (reserved.Contains(key))
            {
                throw new InvalidOperationException($"Extension JSON key '{key}' conflicts with a schema field.");
            }

            target[key] = value;
        }
    }

    private static string SerializeExtensionFields(IReadOnlyDictionary<string, object?> data, IEnumerable<string> schemaKeys)
    {
        var reserved = new HashSet<string>(schemaKeys, StringComparer.OrdinalIgnoreCase);
        var extensions = data
            .Where(entry => entry.Key.StartsWith("x_", StringComparison.OrdinalIgnoreCase) && !reserved.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        return extensions.Count == 0 ? string.Empty : JsonWriter.Serialize(extensions);
    }

    private static Dictionary<string, object?> GetDocNestedMap(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return ConvertToDictionary(value);
    }

    private static string? GetDocString(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }

    private static List<string> GetDocStringList(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return ConvertToStringList(value);
    }

    private static List<Dictionary<string, object?>> GetDocObjectList(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText()) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<Dictionary<string, object?>>();
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                list.Add(ConvertToDictionary(item));
            }

            return list;
        }

        return [];
    }

    private static List<string> ConvertToStringList(object value)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToList();
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>()
                .Select(item => item is JsonElement json ? json.ToString() : item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToList();
        }

        return [];
    }

    private static Dictionary<string, object?> ConvertToDictionary(object value)
    {
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

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText())
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        throw new InvalidOperationException("Expected a JSON object.");
    }
}
