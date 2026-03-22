// Constructs front matter payloads for generated docs.
// Canonical artifact front matter follows SPEC-STD, SPEC-ID, SPEC-LAY, SPEC-TPL, and SPEC-SCH.
// Invariants: timestamps use ISO-8601 UTC; path is repo-relative with forward slashes.
namespace Workbench.Core;

public static class DocFrontMatterBuilder
{
    public static FrontMatter BuildGeneratedDocFrontMatter(
        string repoRoot,
        string docPath,
        string docType,
        string title,
        string body,
        string? artifactId,
        string? domain,
        string? capability,
        IList<string> workItems,
        IList<string> codeRefs,
        IList<string> tags,
        IList<string> related,
        string? status,
        string? owner,
        DocSourceInfo? source,
        DateTimeOffset now,
        IList<string>? satisfies = null,
        IList<string>? verifies = null,
        IList<string>? relatedArtifacts = null)
    {
        var canonicalType = SpecTraceMarkdown.GetCanonicalArtifactType(docType);
        if (canonicalType is not null)
        {
            return BuildCanonicalDocFrontMatter(
                canonicalType,
                title,
                body,
                artifactId,
                domain,
                capability,
                tags,
                status,
                owner,
                satisfies,
                verifies,
                relatedArtifacts);
        }

        return BuildLegacyDocFrontMatter(
            repoRoot,
            docPath,
            docType,
            title,
            body,
            artifactId,
            domain,
            capability,
            workItems,
            codeRefs,
            tags,
            related,
            status,
            owner,
            source,
            now);
    }

    private static FrontMatter BuildCanonicalDocFrontMatter(
        string canonicalType,
        string title,
        string body,
        string? artifactId,
        string? domain,
        string? capability,
        IList<string> tags,
        string? status,
        string? owner,
        IList<string>? satisfies,
        IList<string>? verifies,
        IList<string>? relatedArtifacts)
    {
        var resolvedArtifactId = artifactId?.Trim();
        if (string.IsNullOrWhiteSpace(resolvedArtifactId))
        {
            throw new InvalidOperationException($"Canonical {canonicalType} docs require an artifact_id.");
        }

        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["artifact_id"] = resolvedArtifactId,
            ["artifact_type"] = canonicalType,
            ["title"] = title.Trim(),
            ["domain"] = NormalizeOrFallback(domain, title),
            ["status"] = string.IsNullOrWhiteSpace(status)
                ? GetDefaultStatus(canonicalType)
                : status.Trim(),
            ["owner"] = NormalizeOwner(owner)
        };

        if (canonicalType.Equals("specification", StringComparison.OrdinalIgnoreCase))
        {
            data["capability"] = NormalizeOrFallback(capability, title);
            if (tags.Count > 0)
            {
                data["tags"] = Deduplicate(tags);
            }
        }
        else if (canonicalType.Equals("architecture", StringComparison.OrdinalIgnoreCase))
        {
            data["satisfies"] = NormalizeCanonicalLinks(satisfies, "- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
        }
        else if (canonicalType.Equals("work_item", StringComparison.OrdinalIgnoreCase))
        {
            data["addresses"] = NormalizeCanonicalLinks(satisfies, "REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");
            data["design_links"] = NormalizeCanonicalLinks(verifies, "ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>");

            var relatedArtifactList = Deduplicate(relatedArtifacts ?? Array.Empty<string>());
            if (relatedArtifactList.Count == 0)
            {
                relatedArtifactList = ["SPEC-<DOMAIN>[-<GROUPING>...]"];
            }

            data["related_artifacts"] = relatedArtifactList;
        }
        if (!canonicalType.Equals("work_item", StringComparison.OrdinalIgnoreCase))
        {
            var relatedArtifactList = Deduplicate(relatedArtifacts ?? Array.Empty<string>());
            if (relatedArtifactList.Count > 0)
            {
                data["related_artifacts"] = relatedArtifactList;
            }
        }

        return new FrontMatter(data, body);
    }

    private static FrontMatter BuildLegacyDocFrontMatter(
        string repoRoot,
        string docPath,
        string docType,
        string title,
        string body,
        string? artifactId,
        string? domain,
        string? capability,
        IList<string> workItems,
        IList<string> codeRefs,
        IList<string> tags,
        IList<string> related,
        string? status,
        string? owner,
        DocSourceInfo? source,
        DateTimeOffset now)
    {
        var relative = "/" + Path.GetRelativePath(repoRoot, docPath).Replace('\\', '/');
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["type"] = docType,
            ["created_utc"] = now.ToString("o", CultureInfo.InvariantCulture),
            ["updated_utc"] = now.ToString("o", CultureInfo.InvariantCulture),
            ["tags"] = Deduplicate(tags).Cast<object?>().ToList(),
            ["related"] = Deduplicate(related).Cast<object?>().ToList(),
            ["workbench"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = docType,
                ["workItems"] = Deduplicate(workItems).Cast<object?>().ToList(),
                ["codeRefs"] = Deduplicate(codeRefs).Cast<object?>().ToList(),
                ["path"] = relative,
                ["pathHistory"] = new List<string>()
            }
        };

        if (!string.IsNullOrWhiteSpace(artifactId))
        {
            data["artifact_id"] = artifactId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(domain))
        {
            data["domain"] = domain.Trim();
        }

        if (!string.IsNullOrWhiteSpace(capability))
        {
            data["capability"] = capability.Trim();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            data["status"] = status;
        }

        if (!string.IsNullOrWhiteSpace(owner))
        {
            data["owner"] = owner.Trim();
        }

        if (source is not null)
        {
            var sourceMap = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = source.Kind,
                ["audio"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["sample_rate_hz"] = source.Audio.SampleRateHz,
                    ["channels"] = source.Audio.Channels,
                    ["format"] = source.Audio.Format
                }
            };

            if (!string.IsNullOrWhiteSpace(source.Transcript))
            {
                sourceMap["transcript"] = source.Transcript;
            }

            data["source"] = sourceMap;
        }

        return new FrontMatter(data, body);
    }

    private static List<string> NormalizeCanonicalLinks(IEnumerable<string>? values, string placeholder)
    {
        var items = Deduplicate(values ?? Array.Empty<string>());
        if (items.Count == 0)
        {
            items.Add(placeholder);
        }

        return items;
    }

    private static List<string> Deduplicate(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            if (seen.Add(trimmed))
            {
                items.Add(trimmed);
            }
        }

        return items;
    }

    private static string NormalizeOrFallback(string? value, string fallback)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return ArtifactIdPolicy.NormalizeToken(fallback);
    }

    private static string NormalizeOwner(string? owner)
    {
        var normalized = owner?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "platform" : normalized;
    }

    private static string GetDefaultStatus(string canonicalType)
    {
        return canonicalType.ToLowerInvariant() switch
        {
            "work_item" => "planned",
            _ => "draft"
        };
    }

    public static string BuildTranscriptExcerpt(string transcript, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(transcript) || maxChars <= 0)
        {
            return string.Empty;
        }

        var trimmed = transcript.Trim();
        if (trimmed.Length <= maxChars)
        {
            return trimmed;
        }

        return trimmed[..maxChars].TrimEnd() + "...";
    }
}
