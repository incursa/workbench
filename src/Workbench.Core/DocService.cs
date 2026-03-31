// Documentation creation and backlink synchronization logic.
// Canonical spec-trace artifacts follow SPEC-STD, SPEC-ID, SPEC-LAY, SPEC-TPL, and SPEC-SCH.
// Invariants: doc types are limited to allowedTypes; front matter is the single source of link truth.
using System.Collections;

namespace Workbench.Core;

public static class DocService
{
    /// <summary>
    /// Result payload returned by doc creation operations.
    /// </summary>
    /// <param name="Path">Absolute path to the created doc.</param>
    /// <param name="ArtifactId">Resolved artifact identifier if one was supplied or inferred.</param>
    /// <param name="Type">Document type.</param>
    /// <param name="WorkItems">Linked work item IDs.</param>
    public sealed record DocCreateResult(string Path, string? ArtifactId, string Type, IList<string> WorkItems);

    /// <summary>
    /// Result payload returned by doc/work item sync operations.
    /// </summary>
    /// <param name="DocsUpdated">Number of docs updated.</param>
    /// <param name="ItemsUpdated">Number of work items updated.</param>
    /// <param name="MissingDocs">Missing doc references.</param>
    /// <param name="MissingItems">Missing work item references.</param>
    public sealed record DocSyncResult(
        int DocsUpdated,
        int ItemsUpdated,
        IList<string> MissingDocs,
        IList<string> MissingItems);

    private static readonly HashSet<string> allowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "spec",
        "specification",
        "architecture",
        "verification",
        "runbook",
        "doc",
        "work_item"
    };

    public static DocCreateResult CreateDoc(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? path,
        IList<string> workItems,
        IList<string> codeRefs,
        bool force,
        string? artifactId = null,
        string? domain = null,
        string? capability = null)
    {
        if (!allowedTypes.Contains(type))
        {
            throw new InvalidOperationException($"Invalid doc type '{type}'.");
        }

        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(type);
        var resolvedArtifactId = NormalizeArtifactId(artifactId)
            ?? TryGenerateDocArtifactId(repoRoot, config, type, title, domain, capability);

        if (canonicalType is not null && string.IsNullOrWhiteSpace(resolvedArtifactId))
        {
            throw new InvalidOperationException(
                $"Unable to generate an artifact ID for the canonical {canonicalType} document. Provide --artifact-id or the metadata required by the configured artifact ID policy.");
        }

        var docPath = ResolveDocPath(repoRoot, config, type, title, path, resolvedArtifactId, domain);
        if (File.Exists(docPath) && !force)
        {
            throw new InvalidOperationException($"Doc already exists: {docPath}");
        }

        var relative = "/" + Path.GetRelativePath(repoRoot, docPath).Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(resolvedArtifactId) && RequiresArtifactId(type))
        {
            throw new InvalidOperationException(
                $"Unable to generate an artifact ID for '{docPath}'. Provide --artifact-id or the metadata required by the configured artifact ID policy.");
        }

        if (canonicalType is not null)
        {
            var canonicalRelatedArtifacts = canonicalType.Equals("work_item", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : workItems;
            var canonicalBody = BuildBody(type, title);
            var canonicalFrontMatter = DocFrontMatterBuilder.BuildGeneratedDocFrontMatter(
                repoRoot,
                docPath,
                type,
                title,
                canonicalBody,
                resolvedArtifactId,
                domain,
                capability,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                workItems,
                status: null,
                owner: null,
                source: null,
                DateTimeOffset.UtcNow,
                relatedArtifacts: canonicalRelatedArtifacts);

            Directory.CreateDirectory(Path.GetDirectoryName(docPath) ?? repoRoot);
            File.WriteAllText(docPath, canonicalFrontMatter.Serialize());

            foreach (var workItemId in workItems)
            {
                var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
                WorkItemService.AddRelatedLink(itemPath, DocTypeToRelatedKey(type), relative);
            }

            return new DocCreateResult(docPath, resolvedArtifactId, type, workItems);
        }

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["type"] = type,
            ["workbench"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["workItems"] = workItems.Cast<object?>().ToList(),
                ["codeRefs"] = codeRefs.Cast<object?>().ToList(),
                ["path"] = relative,
                ["pathHistory"] = new List<string>()
            }
        };

        if (!string.IsNullOrWhiteSpace(resolvedArtifactId))
        {
            data["artifact_id"] = resolvedArtifactId;
        }

        if (!string.IsNullOrWhiteSpace(domain))
        {
            data["domain"] = domain.Trim();
        }

        if (!string.IsNullOrWhiteSpace(capability))
        {
            data["capability"] = capability.Trim();
        }

        var body = BuildBody(type, title);
        var frontMatter = new FrontMatter(data, body);
        Directory.CreateDirectory(Path.GetDirectoryName(docPath) ?? repoRoot);
        File.WriteAllText(docPath, frontMatter.Serialize());

        foreach (var workItemId in workItems)
        {
            var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
            WorkItemService.AddRelatedLink(itemPath, DocTypeToRelatedKey(type), relative);
        }

        return new DocCreateResult(docPath, resolvedArtifactId, type, workItems);
    }

    public static DocCreateResult CreateGeneratedDoc(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string body,
        string? path,
        IList<string> workItems,
        IList<string> codeRefs,
        IList<string> tags,
        IList<string> related,
        string? status,
        DocSourceInfo? source,
        bool force,
        string? artifactId = null,
        string? domain = null,
        string? capability = null,
        string? owner = null)
    {
        if (!allowedTypes.Contains(type))
        {
            throw new InvalidOperationException($"Invalid doc type '{type}'.");
        }

        var normalizedBody = DocBodyBuilder.EnsureTitle(body, title);
        var now = DateTimeOffset.UtcNow;
        var resolvedArtifactId = NormalizeArtifactId(artifactId)
            ?? TryGenerateDocArtifactId(repoRoot, config, type, title, domain, capability);

        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(type);
        if (canonicalType is not null && string.IsNullOrWhiteSpace(resolvedArtifactId))
        {
            throw new InvalidOperationException(
                $"Unable to generate an artifact ID for the canonical {canonicalType} document. Provide --artifact-id or the metadata required by the configured artifact ID policy.");
        }

        var docPath = ResolveDocPath(repoRoot, config, type, title, path, resolvedArtifactId, domain);
        if (File.Exists(docPath) && !force)
        {
            throw new InvalidOperationException($"Doc already exists: {docPath}");
        }

        if (string.IsNullOrWhiteSpace(resolvedArtifactId) && RequiresArtifactId(type))
        {
            throw new InvalidOperationException(
                $"Unable to generate an artifact ID for '{docPath}'. Provide --artifact-id or the metadata required by the configured artifact ID policy.");
        }
        var frontMatter = DocFrontMatterBuilder.BuildGeneratedDocFrontMatter(
            repoRoot,
            docPath,
            type,
            title,
            normalizedBody,
            resolvedArtifactId,
            domain,
            capability,
            canonicalType is not null ? Array.Empty<string>() : workItems,
            canonicalType is not null ? Array.Empty<string>() : codeRefs,
            tags,
            related,
            status,
            owner,
            source,
            now,
            relatedArtifacts: GetCanonicalRelatedArtifacts(canonicalType, workItems, related));

        Directory.CreateDirectory(Path.GetDirectoryName(docPath) ?? repoRoot);
        File.WriteAllText(docPath, frontMatter.Serialize());

        var relative = "/" + Path.GetRelativePath(repoRoot, docPath).Replace('\\', '/');
        foreach (var workItemId in workItems)
        {
            var itemPath = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
            WorkItemService.AddRelatedLink(itemPath, DocTypeToRelatedKey(type), relative);
        }

        return new DocCreateResult(docPath, resolvedArtifactId, type, workItems);
    }

    /// <summary>
    /// Result payload returned by doc edit operations.
    /// </summary>
    /// <param name="Path">Absolute path to the edited doc.</param>
    /// <param name="ArtifactId">Resolved artifact identifier after the edit.</param>
    /// <param name="ArtifactIdUpdated">True when the artifact identifier changed.</param>
    /// <param name="TitleUpdated">True when the title changed.</param>
    /// <param name="StatusUpdated">True when the status changed.</param>
    /// <param name="OwnerUpdated">True when the owner changed.</param>
    /// <param name="BodyUpdated">True when the Markdown body changed.</param>
    /// <param name="WorkItemsUpdated">True when linked work items changed.</param>
    /// <param name="CodeRefsUpdated">True when code refs changed.</param>
    public sealed record DocEditResult(
        string Path,
        string? ArtifactId,
        bool ArtifactIdUpdated,
        bool TitleUpdated,
        bool StatusUpdated,
        bool OwnerUpdated,
        bool DomainUpdated,
        bool CapabilityUpdated,
        bool BodyUpdated,
        bool WorkItemsUpdated,
        bool CodeRefsUpdated);

    public static DocEditResult EditDoc(
        string repoRoot,
        WorkbenchConfig config,
        string reference,
        string? artifactId,
        string? title,
        string? status,
        string? owner,
        string? domain,
        string? capability,
        string? body,
        IList<string>? workItems,
        IList<string>? codeRefs)
    {
        if (!TryResolveDocPath(repoRoot, config, reference, out var docPath))
        {
            throw new InvalidOperationException($"Doc not found: {reference}");
        }

        if (docPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Editing canonical JSON artifacts through `workbench doc/spec edit` is not supported. Edit the JSON file directly.");
        }

        var content = File.ReadAllText(docPath);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        var data = frontMatter!.Data;
        var originalArtifactId = NormalizeArtifactId(GetString(data, "artifact_id") ?? GetString(data, "artifactId"));
        var artifactIdUpdated = false;
        var titleUpdated = false;
        var statusUpdated = false;
        var ownerUpdated = false;
        var domainUpdated = false;
        var capabilityUpdated = false;
        var bodyUpdated = false;
        var artifactType = GetString(data, "artifact_type");
        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(artifactType ?? string.Empty);
        var currentTitle = GetString(data, "title") ?? ExtractTitle(frontMatter.Body) ?? Path.GetFileNameWithoutExtension(docPath);
        var currentStatus = GetString(data, "status");
        var currentOwner = GetString(data, "owner");
        var currentDomain = GetString(data, "domain");
        var currentCapability = GetString(data, "capability");

        if (artifactId is not null)
        {
            var normalizedArtifactId = NormalizeArtifactId(artifactId);
            if (string.IsNullOrWhiteSpace(normalizedArtifactId))
            {
                var generationTitle = title ?? currentTitle;
                var generatedArtifactId = TryGenerateDocArtifactId(
                    repoRoot,
                    config,
                    InferDocType(docPath),
                    generationTitle,
                    domain ?? currentDomain,
                    capability ?? currentCapability);

                if (!string.IsNullOrWhiteSpace(generatedArtifactId))
                {
                    data["artifact_id"] = generatedArtifactId;
                    artifactIdUpdated = true;
                }
                else if (RequiresArtifactId(InferDocType(docPath)))
                {
                    throw new InvalidOperationException(
                        $"Unable to generate an artifact ID for '{docPath}'. Provide --artifact-id or the metadata required by the configured artifact ID policy.");
                }
                else if (data.Remove("artifact_id") || data.Remove("artifactId"))
                {
                    artifactIdUpdated = true;
                }
            }
            else if (!string.Equals(originalArtifactId, normalizedArtifactId, StringComparison.Ordinal))
            {
                data["artifact_id"] = normalizedArtifactId;
                artifactIdUpdated = true;
            }
        }

        if (domain is not null)
        {
            var normalizedDomain = domain.Trim();
            if (string.IsNullOrWhiteSpace(normalizedDomain))
            {
                if (data.Remove("domain"))
                {
                    domainUpdated = true;
                }
            }
            else if (!string.Equals(currentDomain, normalizedDomain, StringComparison.Ordinal))
            {
                data["domain"] = normalizedDomain;
                domainUpdated = true;
            }
        }

        if (capability is not null)
        {
            var normalizedCapability = capability.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCapability))
            {
                if (data.Remove("capability"))
                {
                    capabilityUpdated = true;
                }
            }
            else if (!string.Equals(currentCapability, normalizedCapability, StringComparison.Ordinal))
            {
                data["capability"] = normalizedCapability;
                capabilityUpdated = true;
            }
        }

        if (title is not null)
        {
            var normalizedTitle = title.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                throw new InvalidOperationException("Doc title cannot be empty.");
            }

            if (!string.Equals(currentTitle, normalizedTitle, StringComparison.Ordinal))
            {
                data["title"] = normalizedTitle;
                titleUpdated = true;
            }
        }

        if (status is not null)
        {
            var normalizedStatus = status.Trim();
            if (string.IsNullOrWhiteSpace(normalizedStatus))
            {
                if (data.Remove("status"))
                {
                    statusUpdated = true;
                }
            }
            else if (!string.Equals(currentStatus, normalizedStatus, StringComparison.Ordinal))
            {
                data["status"] = normalizedStatus;
                statusUpdated = true;
            }
        }

        if (owner is not null)
        {
            var normalizedOwner = owner.Trim();
            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                if (data.Remove("owner"))
                {
                    ownerUpdated = true;
                }
            }
            else if (!string.Equals(currentOwner, normalizedOwner, StringComparison.Ordinal))
            {
                data["owner"] = normalizedOwner;
                ownerUpdated = true;
            }
        }

        if (canonicalType is not null)
        {
            var relatedArtifacts = GetStringList(data, "related_artifacts");
            var relatedArtifactsListChanged = false;

            if (workItems is not null)
            {
                var normalizedRelatedArtifacts = workItems
                    .Select(entry => entry.Trim())
                    .Where(entry => entry.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (normalizedRelatedArtifacts.Count > 0 &&
                    !relatedArtifacts.SequenceEqual(normalizedRelatedArtifacts, StringComparer.OrdinalIgnoreCase))
                {
                    data["related_artifacts"] = normalizedRelatedArtifacts;
                    relatedArtifactsListChanged = true;
                }
            }

            var canonicalEffectiveBody = frontMatter.Body;
            if (body is not null)
            {
                canonicalEffectiveBody = body.TrimEnd() + "\n";
                bodyUpdated = !string.Equals(frontMatter.Body, canonicalEffectiveBody, StringComparison.Ordinal);
            }

            if (artifactIdUpdated || titleUpdated || statusUpdated || ownerUpdated || domainUpdated || capabilityUpdated || body is not null || relatedArtifactsListChanged)
            {
                frontMatter = new FrontMatter(data, canonicalEffectiveBody);
                File.WriteAllText(docPath, frontMatter.Serialize());
            }

            return new DocEditResult(
                docPath,
                NormalizeArtifactId(GetString(data, "artifact_id") ?? GetString(data, "artifactId")) ?? originalArtifactId,
                artifactIdUpdated,
                titleUpdated,
                statusUpdated,
                ownerUpdated,
                domainUpdated,
                capabilityUpdated,
                bodyUpdated,
                relatedArtifactsListChanged,
                false);
        }

        var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var workbenchChanged);
        var resolvedWorkItems = EnsureStringList(workbench, "workItems", out var workItemsListChanged);
        var resolvedCodeRefs = EnsureStringList(workbench, "codeRefs", out var codeRefsListChanged);

        if (workItems is not null)
        {
            var normalizedWorkItems = workItems
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!resolvedWorkItems.SequenceEqual(normalizedWorkItems, StringComparer.OrdinalIgnoreCase))
            {
                workbench["workItems"] = normalizedWorkItems;
                workItemsListChanged = true;
            }
        }

        if (codeRefs is not null)
        {
            var normalizedCodeRefs = codeRefs
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!resolvedCodeRefs.SequenceEqual(normalizedCodeRefs, StringComparer.OrdinalIgnoreCase))
            {
                workbench["codeRefs"] = normalizedCodeRefs;
                codeRefsListChanged = true;
            }
        }

        var effectiveBody = frontMatter.Body;
        if (body is not null)
        {
            effectiveBody = body.TrimEnd() + "\n";
            bodyUpdated = !string.Equals(frontMatter.Body, effectiveBody, StringComparison.Ordinal);
        }

        if (data.ContainsKey("updated_utc"))
        {
            data["updated_utc"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }
        else if (data.ContainsKey("updated"))
        {
            data["updated"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (artifactIdUpdated || titleUpdated || statusUpdated || ownerUpdated || domainUpdated || capabilityUpdated || body is not null || workItemsListChanged || codeRefsListChanged || workbenchChanged)
        {
            frontMatter = new FrontMatter(data, effectiveBody);
            File.WriteAllText(docPath, frontMatter.Serialize());
        }

        return new DocEditResult(
            docPath,
            NormalizeArtifactId(GetString(data, "artifact_id") ?? GetString(data, "artifactId")) ?? originalArtifactId,
            artifactIdUpdated,
            titleUpdated,
            statusUpdated,
            ownerUpdated,
            domainUpdated,
            capabilityUpdated,
            bodyUpdated,
            workItemsListChanged,
            codeRefsListChanged);
    }

    public static DocShowData GetDocShowData(
        string repoRoot,
        WorkbenchConfig config,
        string reference)
    {
        if (!TryResolveDocPath(repoRoot, config, reference, out var docPath))
        {
            throw new InvalidOperationException($"Doc not found: {reference}");
        }

        return LoadDocShowData(docPath);
    }

    public static async Task<DocSyncResult> SyncLinksAsync(string repoRoot, WorkbenchConfig config, bool includeAllDocs, bool syncIssues, bool includeDone, bool dryRun)
    {
        var itemsUpdated = 0;
        var docsUpdated = 0;
        var missingDocs = new List<string>();
        var missingItems = new List<string>();
        var docPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemsById = LoadItems(repoRoot, config);
        var referencedDocs = BuildReferencedDocSet(repoRoot, itemsById.Values);

        var docsRoots = new[]
        {
            (Root: Path.Combine(repoRoot, "runbooks"), Search: SearchOption.AllDirectories),
            (Root: Path.Combine(repoRoot, "tracking"), Search: SearchOption.AllDirectories),
            (Root: Path.Combine(repoRoot, config.Paths.SpecsRoot, "requirements"), Search: SearchOption.AllDirectories),
            (Root: Path.Combine(repoRoot, config.Paths.ArchitectureDir), Search: SearchOption.AllDirectories),
            (Root: Path.Combine(repoRoot, config.Paths.SpecsRoot, "verification"), Search: SearchOption.AllDirectories)
        };

        if (!docsRoots.Any(root => Directory.Exists(root.Root)))
        {
            return new DocSyncResult(0, 0, missingDocs, missingItems);
        }

        foreach (var docsRoot in docsRoots)
        {
            if (!Directory.Exists(docsRoot.Root))
            {
                continue;
            }

            foreach (var docPath in Directory.EnumerateFiles(docsRoot.Root, "*.md", docsRoot.Search))
            {
                if (IsWorkItemDocumentPath(repoRoot, config, docPath))
                {
                    continue;
                }

                if (!TryLoadOrCreateFrontMatter(
                        docPath,
                        includeAllDocs,
                        referencedDocs,
                        out var frontMatter,
                        out var createdFrontMatter))
                {
                    continue;
                }

                var artifactType = GetString(frontMatter!.Data, "artifact_type");
                if (SpecTraceMarkdown.GetCanonicalArtifactType(artifactType ?? string.Empty) is not null)
                {
                    continue;
                }

                var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var docChanged);
                var docType = GetString(workbench, "type") ?? InferDocType(docPath);
                var workItems = EnsureStringList(workbench, "workItems", out var workItemsChanged);
                _ = EnsureStringList(workbench, "codeRefs", out var codeRefsChanged);
                var pathChanged = EnsureDocPathMetadata(workbench, repoRoot, docPath, out var relative, out var pathHistory);

                foreach (var workItemId in workItems)
                {
                    if (!itemsById.TryGetValue(workItemId, out var item))
                    {
                        missingItems.Add($"{docPath}: {workItemId}");
                        continue;
                    }
                    if (NeedsBacklink(item, docType, relative) &&
                        WorkItemService.AddRelatedLink(item.Path, DocTypeToRelatedKey(docType), relative, apply: !dryRun))
                    {
                        itemsUpdated++;
                    }
                }

                if (pathHistory.Count > 0)
                {
                    foreach (var entry in pathHistory)
                    {
                        if (entry.Equals(relative, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (!docPathMap.ContainsKey(entry))
                        {
                            docPathMap[entry] = relative;
                        }
                    }
                }

                if ((createdFrontMatter || docChanged || workItemsChanged || codeRefsChanged || pathChanged) && !dryRun)
                {
                    await File.WriteAllTextAsync(docPath, frontMatter!.Serialize()).ConfigureAwait(false);
                    docsUpdated++;
                }
            }
        }

        if (docPathMap.Count > 0)
        {
            foreach (var item in itemsById.Values)
            {
                if (WorkItemService.ReplaceRelatedLinks(item.Path, docPathMap, apply: !dryRun))
                {
                    itemsUpdated++;
                    var refreshed = WorkItemService.LoadItem(item.Path);
                    if (refreshed is not null)
                    {
                        itemsById[item.Id] = refreshed;
                    }
                }
            }
        }

        foreach (var item in itemsById.Values)
        {
            if (!includeDone && IsTerminalStatus(item.Status))
            {
                continue;
            }
            docsUpdated += SyncDocLinksForItem(repoRoot, item, missingDocs, dryRun);
        }

        if (syncIssues)
        {
            itemsUpdated += await WorkItemService.SyncIssueLinksAsync(repoRoot, config, itemsById.Values, dryRun).ConfigureAwait(false);
        }

        return new DocSyncResult(docsUpdated, itemsUpdated, missingDocs, missingItems);
    }

    public static bool TryUpdateDocWorkItemLink(
        string repoRoot,
        WorkbenchConfig config,
        string link,
        string workItemId,
        bool add,
        bool apply = true)
    {
        var docPath = ResolveDocPath(repoRoot, link);
        if (!File.Exists(docPath))
        {
            return false;
        }

        var fullDocPath = Path.GetFullPath(docPath);
        var allowedRoots = new[]
        {
            Path.GetFullPath(Path.Combine(repoRoot, "runbooks")),
            Path.GetFullPath(Path.Combine(repoRoot, "tracking")),
            Path.GetFullPath(Path.Combine(repoRoot, config.Paths.SpecsRoot)),
            Path.GetFullPath(Path.Combine(repoRoot, config.Paths.SpecsRoot, "requirements")),
            Path.GetFullPath(Path.Combine(repoRoot, config.Paths.ArchitectureDir))
        };

        if (!allowedRoots.Any(root =>
                fullDocPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullDocPath, root, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        if (IsWorkItemDocumentPath(repoRoot, config, fullDocPath))
        {
            return false;
        }

        if (docPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var document = CanonicalArtifactJsonLoader.LoadDocument(repoRoot, docPath);
            var data = new Dictionary<string, object?>(document.Data, StringComparer.OrdinalIgnoreCase);
            var relatedArtifacts = GetStringList(data, "related_artifacts");
            var changed = false;

            if (add)
            {
                if (!relatedArtifacts.Contains(workItemId, StringComparer.OrdinalIgnoreCase))
                {
                    relatedArtifacts.Add(workItemId);
                    changed = true;
                }
            }
            else
            {
                var before = relatedArtifacts.Count;
                relatedArtifacts.RemoveAll(entry => entry.Equals(workItemId, StringComparison.OrdinalIgnoreCase));
                changed = relatedArtifacts.Count != before;
            }

            if (changed && apply)
            {
                if (relatedArtifacts.Count > 0)
                {
                    data["related_artifacts"] = relatedArtifacts;
                }
                else
                {
                    _ = data.Remove("related_artifacts");
                }

                File.WriteAllText(docPath, JsonWriter.Serialize(data));
            }

            return changed;
        }

        if (!docPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryLoadOrCreateFrontMatter(
                docPath,
                includeAllDocs: true,
                referencedDocs: null,
                out var frontMatter,
                out var createdFrontMatter))
        {
            return false;
        }

        var artifactType = GetString(frontMatter!.Data, "artifact_type");
        if (SpecTraceMarkdown.GetCanonicalArtifactType(artifactType ?? string.Empty) is not null)
        {
            var relatedArtifacts = GetStringList(frontMatter.Data, "related_artifacts");
            var relatedArtifactsChanged = false;

            if (add)
            {
                if (!relatedArtifacts.Contains(workItemId, StringComparer.OrdinalIgnoreCase))
                {
                    relatedArtifacts.Add(workItemId);
                    relatedArtifactsChanged = true;
                }
            }
            else
            {
                var before = relatedArtifacts.Count;
                relatedArtifacts.RemoveAll(entry => entry.Equals(workItemId, StringComparison.OrdinalIgnoreCase));
                if (relatedArtifacts.Count != before)
                {
                    relatedArtifactsChanged = true;
                }
            }

            if (createdFrontMatter || relatedArtifactsChanged)
            {
                if (apply)
                {
                    frontMatter!.Data["related_artifacts"] = relatedArtifacts;
                    File.WriteAllText(docPath, frontMatter.Serialize());
                }

                return true;
            }

            return false;
        }

        var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var docChanged);
        var workItems = EnsureStringList(workbench, "workItems", out var listChanged);
        var pathChanged = EnsureDocPathMetadata(workbench, repoRoot, docPath, out _, out _);
        var updated = false;

        if (add)
        {
            if (!workItems.Contains(workItemId, StringComparer.OrdinalIgnoreCase))
            {
                workItems.Add(workItemId);
                listChanged = true;
            }
        }
        else
        {
            var before = workItems.Count;
            workItems.RemoveAll(entry => entry.Equals(workItemId, StringComparison.OrdinalIgnoreCase));
            if (workItems.Count != before)
            {
                listChanged = true;
            }
        }

        if (createdFrontMatter || docChanged || listChanged || pathChanged)
        {
            if (apply)
            {
                File.WriteAllText(docPath, frontMatter!.Serialize());
            }
            updated = true;
        }

        return updated;
    }

    public static int NormalizeDocs(string repoRoot, WorkbenchConfig config, bool includeAllDocs, bool dryRun)
    {
        var updated = 0;
        var docsRoots = new[]
        {
            Path.Combine(repoRoot, "runbooks"),
            Path.Combine(repoRoot, "tracking"),
            Path.Combine(repoRoot, config.Paths.SpecsRoot, "requirements"),
            Path.Combine(repoRoot, config.Paths.ArchitectureDir),
            Path.Combine(repoRoot, config.Paths.SpecsRoot, "verification")
        };

        if (!docsRoots.Any(Directory.Exists))
        {
            return updated;
        }

        HashSet<string>? referencedDocs = null;
        if (!includeAllDocs)
        {
            var itemsById = LoadItems(repoRoot, config);
            referencedDocs = BuildReferencedDocSet(repoRoot, itemsById.Values);
        }

        foreach (var docsRoot in docsRoots)
        {
            if (!Directory.Exists(docsRoot))
            {
                continue;
            }

            foreach (var docPath in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories))
            {
                if (IsWorkItemDocumentPath(repoRoot, config, docPath))
                {
                    continue;
                }

                if (!TryLoadOrCreateFrontMatter(
                        docPath,
                        includeAllDocs,
                        referencedDocs,
                        out var frontMatter,
                        out var createdFrontMatter))
                {
                    continue;
                }

                var artifactType = GetString(frontMatter!.Data, "artifact_type");
                if (SpecTraceMarkdown.GetCanonicalArtifactType(artifactType ?? string.Empty) is not null)
                {
                    continue;
                }

                var workbench = EnsureWorkbench(frontMatter!, InferDocType(docPath), out var docChanged);
                _ = EnsureStringList(workbench, "workItems", out var workItemsChanged);
                _ = EnsureStringList(workbench, "codeRefs", out var codeRefsChanged);
                var pathChanged = EnsureDocPathMetadata(workbench, repoRoot, docPath, out _, out _);

                if ((createdFrontMatter || docChanged || workItemsChanged || codeRefsChanged || pathChanged) && !dryRun)
                {
                    File.WriteAllText(docPath, frontMatter!.Serialize());
                    updated++;
                }
            }
        }

        return updated;
    }

    private static int SyncDocLinksForItem(string repoRoot, WorkItem item, List<string> missingDocs, bool dryRun)
    {
        var updates = 0;
        updates += SyncDocList(repoRoot, item, item.Related.Specs, "spec", missingDocs, dryRun);
        updates += SyncDocList(repoRoot, item, item.Related.Files, "doc", missingDocs, dryRun);
        return updates;
    }

    private static bool IsTerminalStatus(string status)
    {
        return string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "superseded", StringComparison.OrdinalIgnoreCase);
    }

    private static int SyncDocList(
        string repoRoot,
        WorkItem item,
        IList<string> docs,
        string docType,
        IList<string> missingDocs,
        bool dryRun)
    {
        var updated = 0;
        foreach (var link in docs)
        {
            var docPath = ResolveDocPath(repoRoot, link);
            if (!File.Exists(docPath))
            {
                missingDocs.Add($"{item.Id}: {link}");
                continue;
            }
            if (!TryLoadOrCreateFrontMatter(
                    docPath,
                    includeAllDocs: true,
                    referencedDocs: null,
                    out var frontMatter,
                    out var createdFrontMatter))
            {
                continue;
            }

            var artifactType = GetString(frontMatter!.Data, "artifact_type");
            if (SpecTraceMarkdown.GetCanonicalArtifactType(artifactType ?? string.Empty) is not null)
            {
                continue;
            }

            var workbench = EnsureWorkbench(frontMatter!, docType, out var docChanged);
            var workItems = EnsureStringList(workbench, "workItems", out var listChanged);
            var pathChanged = EnsureDocPathMetadata(workbench, repoRoot, docPath, out _, out _);
            var currentType = GetString(workbench, "type") ?? docType;
            if (!currentType.Equals(docType, StringComparison.OrdinalIgnoreCase))
            {
                workbench["type"] = docType;
                docChanged = true;
            }

            if (!workItems.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            {
                workItems.Add(item.Id);
                listChanged = true;
            }

            if ((createdFrontMatter || docChanged || listChanged || pathChanged) && !dryRun)
            {
                File.WriteAllText(docPath, frontMatter!.Serialize());
                updated++;
            }
        }

        return updated;
    }

    private static bool NeedsBacklink(WorkItem item, string docType, string docPath)
    {
        var key = DocTypeToRelatedKey(docType);
        var list = key switch
        {
            "specs" => item.Related.Specs,
            "files" => item.Related.Files,
            _ => item.Related.Specs
        };
        return !list.Any(link => link.Equals(docPath, StringComparison.OrdinalIgnoreCase));
    }

    public static string DocTypeToRelatedKey(string docType)
    {
        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(docType);
        if (canonicalType is "specification")
        {
            return "specs";
        }
        if (docType.Equals("spec", StringComparison.OrdinalIgnoreCase))
        {
            return "specs";
        }
        if (docType.Equals("specification", StringComparison.OrdinalIgnoreCase))
        {
            return "specs";
        }
        if (canonicalType is "architecture" or "verification" or "work_item")
        {
            return "files";
        }
        if (docType.Equals("architecture", StringComparison.OrdinalIgnoreCase) ||
            docType.Equals("verification", StringComparison.OrdinalIgnoreCase) ||
            docType.Equals("runbook", StringComparison.OrdinalIgnoreCase) ||
            docType.Equals("doc", StringComparison.OrdinalIgnoreCase))
        {
            return "files";
        }
        return "files";
    }

    public static string? NormalizeArtifactId(string? artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
        {
            return null;
        }

        return artifactId.Trim();
    }

    public static bool TryResolveDocPathByArtifactId(
        string repoRoot,
        WorkbenchConfig config,
        string artifactId,
        out string docPath)
    {
        docPath = string.Empty;
        var normalizedArtifactId = NormalizeArtifactId(artifactId);
        if (string.IsNullOrWhiteSpace(normalizedArtifactId))
        {
            return false;
        }

        foreach (var source in CanonicalArtifactDiscovery.EnumerateCanonicalSources(repoRoot, config)
                     .Where(source => string.Equals(source.Format, "json", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryGetDocumentArtifactId(source.SourcePath, out var currentArtifactId) &&
                string.Equals(currentArtifactId, normalizedArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                docPath = source.SourcePath;
                return true;
            }

            var stem = Path.GetFileNameWithoutExtension(source.SourcePath);
            if (stem.Equals(normalizedArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                docPath = source.SourcePath;
                return true;
            }
        }

        foreach (var rootInfo in new[]
                 {
                      (Root: Path.Combine(repoRoot, config.Paths.SpecsRoot), Search: SearchOption.TopDirectoryOnly),
                      (Root: Path.Combine(repoRoot, "runbooks"), Search: SearchOption.AllDirectories),
                      (Root: Path.Combine(repoRoot, "tracking"), Search: SearchOption.AllDirectories),
                      (Root: Path.Combine(repoRoot, config.Paths.SpecsRoot, "requirements"), Search: SearchOption.AllDirectories),
                      (Root: Path.Combine(repoRoot, config.Paths.ArchitectureDir), Search: SearchOption.AllDirectories),
                      (Root: Path.Combine(repoRoot, config.Paths.SpecsRoot, "verification"), Search: SearchOption.AllDirectories)
                  })
        {
            if (!Directory.Exists(rootInfo.Root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(rootInfo.Root, "*.md", rootInfo.Search))
            {
                if (IsWorkItemDocumentPath(repoRoot, config, file))
                {
                    continue;
                }

                if (TryGetDocumentArtifactId(file, out var currentArtifactId) &&
                    string.Equals(currentArtifactId, normalizedArtifactId, StringComparison.OrdinalIgnoreCase))
                {
                    docPath = file;
                    return true;
                }

                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.Equals(normalizedArtifactId, StringComparison.OrdinalIgnoreCase))
                {
                    docPath = file;
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryResolveDocPath(string repoRoot, WorkbenchConfig config, string reference, out string docPath)
    {
        docPath = ResolveDocPath(repoRoot, reference);
        if (File.Exists(docPath))
        {
            return true;
        }

        return TryResolveDocPathByArtifactId(repoRoot, config, reference, out docPath);
    }

    public static bool TryGetDocumentArtifactId(string path, out string? artifactId)
    {
        artifactId = null;
        if (!File.Exists(path))
        {
            return false;
        }

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
#pragma warning disable ERP022
            try
            {
                var repoRoot = FindRepoRootForCanonicalFile(path);
                var document = CanonicalArtifactJsonLoader.LoadDocument(repoRoot, path);
                var data = new Dictionary<string, object?>(document.Data, StringComparer.OrdinalIgnoreCase);
                artifactId = NormalizeArtifactId(GetString(data, "artifact_id") ?? GetString(data, "artifactId"));
            }
            catch
            {
                artifactId = null;
            }
#pragma warning restore ERP022

            if (string.IsNullOrWhiteSpace(artifactId))
            {
                artifactId = Path.GetFileNameWithoutExtension(path);
            }

            return true;
        }

        var content = File.ReadAllText(path);
        if (FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            artifactId = NormalizeArtifactId(GetString(frontMatter!.Data, "artifact_id") ?? GetString(frontMatter.Data, "artifactId"));
        }

        if (string.IsNullOrWhiteSpace(artifactId))
        {
            artifactId = Path.GetFileNameWithoutExtension(path);
        }

        return true;
    }

    private static bool TryGetExplicitDocumentArtifactId(string path, out string? artifactId)
    {
        artifactId = null;
        if (!File.Exists(path))
        {
            return false;
        }

        var content = File.ReadAllText(path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out _))
        {
            return false;
        }

        artifactId = NormalizeArtifactId(GetString(frontMatter!.Data, "artifact_id") ?? GetString(frontMatter.Data, "artifactId"));
        return !string.IsNullOrWhiteSpace(artifactId);
    }

    private static string? TryGenerateDocArtifactId(
        string repoRoot,
        WorkbenchConfig config,
        string docType,
        string title,
        string? domain,
        string? capability)
    {
        var policy = ArtifactIdPolicy.Load(repoRoot);
        var template = policy.GetTemplateForDocType(docType);
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var policyPath = Path.Combine(repoRoot, "artifact-id-policy.json");
        var policyEnabled = File.Exists(policyPath);
        var requiresDomain = template.Contains("{domain}", StringComparison.OrdinalIgnoreCase);
        var requiresGrouping = template.Contains("{grouping}", StringComparison.OrdinalIgnoreCase);
        if (policyEnabled)
        {
            if (requiresDomain && string.IsNullOrWhiteSpace(domain))
            {
                return null;
            }

            if (requiresGrouping && string.IsNullOrWhiteSpace(capability))
            {
                return null;
            }
        }

        var normalizedDomain = ArtifactIdPolicy.NormalizeToken(domain);
        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            normalizedDomain = SpecTraceLayout.GetDefaultDomain(repoRoot);
        }
        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            normalizedDomain = ArtifactIdPolicy.NormalizeToken(title);
        }

        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            return null;
        }

        var prefix = policy.BuildArtifactIdPrefix(docType, normalizedDomain, capability);
        var sequence = GetNextArtifactSequence(repoRoot, config, prefix);
        return policy.BuildArtifactId(docType, normalizedDomain, capability, sequence);
    }

    private static bool RequiresArtifactId(string docType)
    {
        if (SpecTraceMarkdown.GetCanonicalArtifactType(docType) is not null)
        {
            return true;
        }

        if (string.Equals(docType, "spec", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int GetNextArtifactSequence(string repoRoot, WorkbenchConfig config, string prefix)
    {
        var max = 0;
        foreach (var configuredRoot in new[]
                 {
                      "runbooks",
                      "tracking",
                      Path.Combine(config.Paths.SpecsRoot, "requirements"),
                      config.Paths.ArchitectureDir,
                      Path.Combine(config.Paths.SpecsRoot, "verification"),
                      Path.Combine(config.Paths.SpecsRoot, "work-items")
                  })
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                continue;
            }

            var root = Path.Combine(repoRoot, configuredRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                if (IsWorkItemDocumentPath(repoRoot, config, file))
                {
                    continue;
                }

                if (!TryGetExplicitDocumentArtifactId(file, out var currentArtifactId) || currentArtifactId is null)
                {
                    continue;
                }

                if (!currentArtifactId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var suffix = currentArtifactId[prefix.Length..];
                if (ArtifactIdPolicy.TryParseSequence(suffix, out var sequence))
                {
                    max = Math.Max(max, sequence);
                }
            }
        }

        return max + 1;
    }

    private static Dictionary<string, WorkItem> LoadItems(string repoRoot, WorkbenchConfig config)
    {
        var items = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var includeDone in new[] { false, true })
        {
            var list = WorkItemService.ListItems(repoRoot, config, includeDone);
            foreach (var item in list.Items)
            {
                items[item.Id] = item;
            }
        }
        return items;
    }

    private static bool TryLoadOrCreateFrontMatter(
        string path,
        bool includeAllDocs,
        HashSet<string>? referencedDocs,
        out FrontMatter? frontMatter,
        out bool created)
    {
        frontMatter = null;
        created = false;
        var content = File.ReadAllText(path);
        if (FrontMatter.TryParse(content, out frontMatter, out _))
        {
            return true;
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("---\n", StringComparison.Ordinal) ||
            trimmed.StartsWith("---\r\n", StringComparison.Ordinal))
        {
            return false;
        }

        if (!includeAllDocs)
        {
            var relative = "/" + path.Replace('\\', '/');
            if (referencedDocs is null || !referencedDocs.Contains(relative))
            {
                return false;
            }
        }

        frontMatter = new FrontMatter(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            content.TrimStart('\r', '\n'));
        created = true;
        return true;
    }

    private static Dictionary<string, object?> EnsureWorkbench(FrontMatter frontMatter, string docType, out bool changed)
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

        var resolvedType = docType;
        var existingType = GetString(workbench, "type");
        if (string.IsNullOrWhiteSpace(existingType))
        {
            workbench["type"] = resolvedType;
            changed = true;
        }

        return workbench;
    }

    private static bool EnsureDocPathMetadata(
        Dictionary<string, object?> workbench,
        string repoRoot,
        string docPath,
        out string currentPath,
        out List<string> pathHistory)
    {
        var changed = false;
        var relativePath = Path.GetRelativePath(repoRoot, docPath)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        currentPath = string.Concat(Path.AltDirectorySeparatorChar, relativePath);

        var existingPath = GetString(workbench, "path");
        if (!string.IsNullOrWhiteSpace(existingPath))
        {
            var normalizedExisting = NormalizeDocPathValue(repoRoot, existingPath);
            if (!string.Equals(existingPath, normalizedExisting, StringComparison.Ordinal))
            {
                workbench["path"] = normalizedExisting;
                existingPath = normalizedExisting;
                changed = true;
            }
        }

        pathHistory = EnsureStringList(workbench, "pathHistory", out var historyChanged);
        if (historyChanged)
        {
            changed = true;
        }

        var normalizedHistory = NormalizePathHistory(repoRoot, pathHistory, out var historyUpdated);
        if (historyUpdated)
        {
            pathHistory = normalizedHistory;
            workbench["pathHistory"] = pathHistory;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(existingPath) &&
            !string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase) &&
            !pathHistory.Any(entry => entry.Equals(existingPath, StringComparison.OrdinalIgnoreCase)))
        {
            pathHistory.Add(existingPath);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existingPath) ||
            !string.Equals(existingPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            workbench["path"] = currentPath;
            changed = true;
        }

        return changed;
    }

    private static List<string> NormalizePathHistory(string repoRoot, List<string> history, out bool changed)
    {
        changed = false;
        if (history.Count == 0)
        {
            return history;
        }

        var normalized = history
            .Select(entry => NormalizeDocPathValue(repoRoot, entry))
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!normalized.SequenceEqual(history, StringComparer.Ordinal))
        {
            changed = true;
        }

        return normalized;
    }

    private static string NormalizeDocPathValue(string repoRoot, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        if (LooksLikeRepoRelativeDocPath(trimmed))
        {
            trimmed = trimmed.Replace('\\', '/');
            return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed.TrimStart('/');
        }

        if (Path.IsPathRooted(trimmed))
        {
            var full = Path.GetFullPath(trimmed);
            var repoFull = Path.GetFullPath(repoRoot);
            if (full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "/" + Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
            }
            else
            {
                trimmed = full.Replace('\\', '/');
            }
        }
        else
        {
            trimmed = trimmed.Replace('\\', '/');
            trimmed = trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed.TrimStart('/');
        }

        return trimmed;
    }

    private static bool LooksLikeRepoRelativeDocPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Length < 3
            || normalized[2] != ':'
            || !char.IsLetter(normalized[1]);
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

    private static string? GetString(IDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }
        return value.ToString();
    }

    internal static string ResolveDocPath(
        string repoRoot,
        WorkbenchConfig config,
        string type,
        string title,
        string? path,
        string? artifactId = null,
        string? domain = null)
    {
        var slug = WorkItemService.Slugify(title);
        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(type);

        if (!string.IsNullOrWhiteSpace(path))
        {
            var target = Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
            if (!target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                target += ".md";
            }
            target = Path.GetFullPath(target);
            if (string.Equals(canonicalType, "specification", StringComparison.OrdinalIgnoreCase))
            {
                var specsRoot = Path.GetFullPath(Path.Combine(repoRoot, config.Paths.SpecsRoot));
                if (!SpecTraceLayout.IsDirectChildPath(target, specsRoot) ||
                    Path.GetFileName(target).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Specification paths must live directly under specs/.");
                }
            }
            else if (string.Equals(canonicalType, "verification", StringComparison.OrdinalIgnoreCase))
            {
                var verificationRoot = Path.GetFullPath(Path.Combine(repoRoot, SpecTraceLayout.VerificationRoot));
                if (!IsChildPath(target, verificationRoot) ||
                    Path.GetFileName(target).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Verification paths must live under specs/verification/.");
                }
            }
            return target;
        }

        var normalizedDomain = ArtifactIdPolicy.NormalizeToken(domain);
        if (string.IsNullOrWhiteSpace(normalizedDomain))
        {
            normalizedDomain = SpecTraceLayout.GetDefaultDomain(repoRoot);
        }
        var dir = canonicalType switch
        {
            "specification" => SpecTraceLayout.GetSpecificationDirectory(repoRoot),
            "architecture" => SpecTraceLayout.GetArchitectureDirectory(repoRoot, normalizedDomain),
            "verification" => SpecTraceLayout.GetVerificationDirectory(repoRoot, normalizedDomain),
            "work_item" => SpecTraceLayout.GetWorkItemDirectory(repoRoot, normalizedDomain),
            _ => type.ToLowerInvariant() switch
            {
                "spec" => Path.Combine(repoRoot, config.Paths.SpecsRoot),
                "architecture" => Path.Combine(repoRoot, config.Paths.ArchitectureDir),
                "runbook" => Path.Combine(repoRoot, "runbooks"),
                "doc" => Path.Combine(repoRoot, "runbooks"),
                _ => Path.Combine(repoRoot, "tracking")
            }
        };
        if (!string.IsNullOrWhiteSpace(artifactId) && canonicalType is not null)
        {
            return canonicalType switch
            {
                "specification" => Path.Combine(dir, $"{artifactId.Trim()}.md"),
                "architecture" or "verification" or "work_item" => Path.Combine(dir, $"{slug}.md"),
                _ => Path.Combine(dir, $"{artifactId.Trim()}.md")
            };
        }

        return Path.Combine(dir, $"{slug}.md");
    }

    public static string ResolveDocPath(string repoRoot, string link)
    {
        var trimmed = link.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        var fullPath = Path.GetFullPath(trimmed);
        var repoFull = Path.GetFullPath(repoRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.IsPathRooted(trimmed) &&
            (string.Equals(fullPath, repoFull, StringComparison.OrdinalIgnoreCase)
             || fullPath.StartsWith(repoFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            return fullPath;
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return Path.Combine(repoRoot, trimmed.TrimStart('/'));
        }

        return Path.Combine(repoRoot, trimmed);
    }

    private static HashSet<string> BuildReferencedDocSet(string repoRoot, IEnumerable<WorkItem> items)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#pragma warning disable S3267
        foreach (var item in items)
#pragma warning restore S3267
        {
            AddLinks(set, repoRoot, item.Related.Specs);
            AddLinks(set, repoRoot, item.Related.Files);
        }
        return set;
    }

    private static void AddLinks(HashSet<string> set, string repoRoot, IEnumerable<string> links)
    {
        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }
            var full = ResolveDocPath(repoRoot, link);
            var normalized = "/" + full.Replace('\\', '/');
            set.Add(normalized);
        }
    }

    private static string InferDocType(string docPath)
    {
        var normalized = docPath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("/specs/work-items/"))
        {
            return "work_item";
        }
        if (normalized.Contains("/specs/architecture/"))
        {
            return "architecture";
        }
        if (normalized.Contains("/specs/verification/"))
        {
            return "verification";
        }
        if (normalized.Contains("/specs/requirements/"))
        {
            return "specification";
        }
        if (normalized.Contains("/runbooks/"))
        {
            return "runbook";
        }
        if (normalized.Contains("/tracking/"))
        {
            return "doc";
        }
        return "doc";
    }

    private static bool IsWorkItemDocumentPath(string repoRoot, WorkbenchConfig config, string docPath)
    {
        var full = Path.GetFullPath(docPath);
        var canonicalRoot = GetConfiguredRoot(repoRoot, config.Paths.SpecsRoot, "work-items");
        return IsChildPath(full, canonicalRoot);
    }

    private static string GetConfiguredRoot(string repoRoot, params string?[] segments)
    {
        if (segments.Any(string.IsNullOrWhiteSpace))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine([repoRoot, .. segments!]));
    }

    private static bool IsChildPath(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, root, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractTitle(string body)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }
        return null;
    }

    private static Dictionary<string, object?> GetNestedMap(IDictionary<string, object?> data, string key)
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

    private static List<string> GetStringList(IDictionary<string, object?> data, string key)
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

    private static DocShowData LoadDocShowData(string docPath)
    {
        Dictionary<string, object?> data;
        string body;
        if (docPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var repoRoot = FindRepoRootForCanonicalFile(docPath);
            var document = CanonicalArtifactJsonLoader.LoadDocument(repoRoot, docPath);
            data = new Dictionary<string, object?>(document.Data, StringComparer.OrdinalIgnoreCase);
            body = document.SourceText;
        }
        else
        {
            var content = File.ReadAllText(docPath);
            data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            body = content;

            if (FrontMatter.TryParse(content, out var frontMatter, out _))
            {
                data = new Dictionary<string, object?>(frontMatter!.Data, StringComparer.OrdinalIgnoreCase);
                body = frontMatter.Body;
            }
        }

        var workbench = GetNestedMap(data, "workbench");
        var title = GetString(data, "title") ?? ExtractTitle(body) ?? Path.GetFileNameWithoutExtension(docPath);
        var type = GetString(data, "artifact_type") ?? GetString(workbench, "type") ?? InferDocType(docPath);
        var artifactId = NormalizeArtifactId(GetString(data, "artifact_id") ?? GetString(data, "artifactId"));
        var domain = GetString(data, "domain");
        var capability = GetString(data, "capability");
        var status = GetString(data, "status") ?? GetString(workbench, "status");
        var owner = GetString(data, "owner");
        var workItems = FilterWorkItemArtifactIds(GetStringList(data, "related_artifacts"));
        if (workItems.Count == 0)
        {
            workItems = GetStringList(workbench, "workItems");
        }
        var codeRefs = GetStringList(workbench, "codeRefs");

        return new DocShowData(
            docPath,
            artifactId,
            domain,
            capability,
            type,
            title,
            status,
            owner,
            workItems,
            codeRefs,
            body);
    }

    private static string FindRepoRootForCanonicalFile(string path)
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetFullPath(path));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Workbench.slnx")) ||
                Directory.Exists(Path.Combine(current.FullName, SpecTraceLayout.SpecsRoot)) ||
                Directory.Exists(Path.Combine(current.FullName, "model")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not resolve a repository root for '{path}'.");
    }

    private static List<string> FilterWorkItemArtifactIds(IEnumerable<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.StartsWith("WI-", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static string BuildBody(string type, string title)
    {
        return DocBodyBuilder.BuildSkeleton(type, title);
    }

    private static IList<string>? GetCanonicalRelatedArtifacts(
        string? canonicalType,
        IList<string> workItems,
        IList<string> related)
    {
        if (canonicalType is null)
        {
            return null;
        }

        if (canonicalType.Equals("work_item", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        return workItems.Concat(related).ToList();
    }
}
