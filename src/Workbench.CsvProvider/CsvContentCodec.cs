using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Workbench.Core;

namespace Workbench.CsvProvider;

internal static partial class CsvContentCodec
{
    private static readonly IReadOnlyList<string> commonHeaders =
    [
        "csv_schema",
        "record_mode",
        "operation",
        "resource_type",
        "resource_set",
        "resource_id",
        "provider_id",
        "provider_version",
        "authority_provider_id",
        "authority_epoch",
        "base_version_token",
        "base_payload_sha256",
        "recorded_at_utc",
        "actor",
        "source_revision",
        "transfer_id",
        "approval_reference",
        "title",
        "status"
    ];

    private static readonly IReadOnlyList<string> workItemHeaders =
    [
        "type",
        "owner",
        "priority",
        "description",
        "metadata_json"
    ];

    private static readonly IReadOnlyList<string> specificationHeaders =
    [
        "statement",
        "normative_keyword",
        "notes",
        "metadata_json"
    ];

    public static IReadOnlyList<string> GetHeaders(string resourceType)
    {
        return resourceType switch
        {
            CsvResourceTypes.WorkItem => [.. commonHeaders, .. workItemHeaders],
            CsvResourceTypes.Specification => [.. commonHeaders, .. specificationHeaders],
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported CSV resource type.")
        };
    }

    public static CsvContent ParseContent(
        string resourceType,
        IReadOnlyDictionary<string, string> fields,
        ICollection<ProviderDiagnostic> diagnostics,
        string target)
    {
        string id = Value(fields, "resource_id").Trim();
        string title = Value(fields, "title").Trim();
        string status = Value(fields, "status").Trim();
        Dictionary<string, JsonElement> content = new(StringComparer.Ordinal)
        {
            ["id"] = JsonSerializer.SerializeToElement(id),
            ["title"] = JsonSerializer.SerializeToElement(title),
            ["status"] = JsonSerializer.SerializeToElement(status)
        };

        Require(id, "csv.resource.id.required", "Resource ID is required.", target, diagnostics);
        Require(title, "csv.resource.title.required", "Title is required.", target, diagnostics);
        Require(status, "csv.resource.status.required", "Status is required.", target, diagnostics);

        IReadOnlyList<string> typeHeaders;
        if (string.Equals(resourceType, CsvResourceTypes.WorkItem, StringComparison.Ordinal))
        {
            typeHeaders = workItemHeaders;
            AddOptional(content, "type", Value(fields, "type"));
            AddOptional(content, "owner", Value(fields, "owner"));
            AddOptional(content, "priority", Value(fields, "priority"));
            AddOptional(content, "description", Value(fields, "description"));
        }
        else if (string.Equals(resourceType, CsvResourceTypes.Specification, StringComparison.Ordinal))
        {
            typeHeaders = specificationHeaders;
            string statement = Value(fields, "statement").Trim();
            Require(
                statement,
                "csv.specification.statement.required",
                "A specification row requires one atomic normative statement.",
                target,
                diagnostics);
            content["statement"] = JsonSerializer.SerializeToElement(statement);
            string normativeKeyword = ValidateNormativeStatement(statement, diagnostics, target);
            string declaredKeyword = Value(fields, "normative_keyword").Trim();
            if (!string.IsNullOrEmpty(declaredKeyword)
                && !string.Equals(declaredKeyword, normativeKeyword, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "csv.specification.normative-keyword.mismatch",
                    $"Declared normative keyword '{declaredKeyword}' does not match the statement keyword '{normativeKeyword}'.",
                    target));
            }

            if (!string.IsNullOrEmpty(normativeKeyword))
            {
                content["normativeKeyword"] = JsonSerializer.SerializeToElement(normativeKeyword);
            }

            AddOptional(content, "notes", Value(fields, "notes"));
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported CSV resource type.");
        }

        ParseMetadata(fields, content, diagnostics, target);

        HashSet<string> recognized = new(commonHeaders.Concat(typeHeaders), StringComparer.Ordinal);
        Dictionary<string, string> extensions = fields
            .Where(field => !recognized.Contains(field.Key))
            .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
        if (extensions.Count > 0)
        {
            content["csvExtensions"] = JsonSerializer.SerializeToElement(extensions);
        }

        JsonElement json = JsonSerializer.SerializeToElement(content);
        return new CsvContent(id, title, status, content, extensions, json);
    }

    public static IReadOnlyDictionary<string, string> CreateFields(
        string resourceType,
        string resourceSet,
        JsonElement content,
        CsvRecordMode mode,
        CsvProjectionOperation operation,
        string providerId,
        string providerVersion,
        ResourceAuthorityDeclaration authority,
        string baseVersionToken,
        string basePayloadSha256,
        ResourceProvenance provenance,
        IReadOnlyDictionary<string, string>? preservedExtensions = null)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            throw InvalidContent("csv.content.object.required", "CSV resource content must be a JSON object.");
        }

        Dictionary<string, string> fields = GetHeaders(resourceType)
            .ToDictionary(header => header, _ => string.Empty, StringComparer.Ordinal);
        fields["csv_schema"] = CsvSchemaVersions.ForResourceType(resourceType);
        fields["record_mode"] = mode == CsvRecordMode.Authoritative ? "authoritative" : "projection";
        fields["operation"] = operation switch
        {
            CsvProjectionOperation.None => "none",
            CsvProjectionOperation.Upsert => "upsert",
            CsvProjectionOperation.Delete => "delete",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        fields["resource_type"] = resourceType;
        fields["resource_set"] = resourceSet;
        fields["provider_id"] = providerId;
        fields["provider_version"] = providerVersion;
        fields["authority_provider_id"] = authority.AuthorityProviderId;
        fields["authority_epoch"] = authority.AuthorityEpoch;
        fields["base_version_token"] = baseVersionToken;
        fields["base_payload_sha256"] = basePayloadSha256;
        fields["recorded_at_utc"] = provenance.RecordedAtUtc.ToUniversalTime().ToString("O");
        fields["actor"] = provenance.Actor ?? string.Empty;
        fields["source_revision"] = provenance.SourceRevision ?? string.Empty;
        fields["transfer_id"] = provenance.TransferId ?? string.Empty;
        fields["approval_reference"] = provenance.ApprovalReference ?? string.Empty;

        string id = RequiredString(content, "id");
        fields["resource_id"] = id;
        fields["title"] = RequiredString(content, "title");
        fields["status"] = RequiredString(content, "status");

        if (string.Equals(resourceType, CsvResourceTypes.WorkItem, StringComparison.Ordinal))
        {
            CopyOptionalString(content, fields, "type");
            CopyOptionalString(content, fields, "owner");
            CopyOptionalString(content, fields, "priority");
            CopyOptionalString(content, fields, "description");
        }
        else if (string.Equals(resourceType, CsvResourceTypes.Specification, StringComparison.Ordinal))
        {
            fields["statement"] = RequiredString(content, "statement");
            CopyOptionalString(content, fields, "normativeKeyword", "normative_keyword");
            CopyOptionalString(content, fields, "notes");
        }

        if (content.TryGetProperty("metadata", out JsonElement metadata))
        {
            if (metadata.ValueKind != JsonValueKind.Object)
            {
                throw InvalidContent("csv.content.metadata.invalid", "The metadata property must be a JSON object.");
            }

            fields["metadata_json"] = JsonSerializer.Serialize(metadata);
        }

        Dictionary<string, string> extensions = preservedExtensions is null
            ? new(StringComparer.Ordinal)
            : new(preservedExtensions, StringComparer.Ordinal);
        if (content.TryGetProperty("csvExtensions", out JsonElement csvExtensions))
        {
            if (csvExtensions.ValueKind != JsonValueKind.Object)
            {
                throw InvalidContent("csv.content.extensions.invalid", "csvExtensions must be an object of string values.");
            }

            extensions.Clear();
            foreach (JsonProperty property in csvExtensions.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || GetHeaders(resourceType).Contains(property.Name, StringComparer.Ordinal))
                {
                    throw InvalidContent(
                        "csv.content.extensions.invalid",
                        $"CSV extension '{property.Name}' must be an unreserved string column.");
                }

                extensions[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        foreach ((string key, string value) in extensions)
        {
            fields[key] = value;
        }

        List<ProviderDiagnostic> diagnostics = [];
        _ = ParseContent(resourceType, fields, diagnostics, id);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error))
        {
            throw InvalidContent(
                diagnostics[0].Code,
                string.Join(" ", diagnostics.Where(diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error).Select(diagnostic => diagnostic.Message)),
                diagnostics);
        }

        return fields;
    }

    public static string ComputePayloadSha256(JsonElement content)
    {
        return Convert.ToHexString(SHA256.HashData(CsvCanonicalJson.Canonicalize(content))).ToLowerInvariant();
    }

    public static string CreateVersionToken(string payloadSha256)
    {
        return $"csv-v1:{payloadSha256}";
    }

    public static ProviderDiagnostic Error(string code, string message, string target)
    {
        return new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, target);
    }

    public static ResourceProviderProtocolException InvalidContent(
        string code,
        string message,
        IReadOnlyList<ProviderDiagnostic>? diagnostics = null)
    {
        return new ResourceProviderProtocolException(new ProviderError(code, message), diagnostics);
    }

    private static string ValidateNormativeStatement(
        string statement,
        ICollection<ProviderDiagnostic> diagnostics,
        string target)
    {
        MatchCollection matches = NormativeKeywordRegex().Matches(statement);
        if (matches.Count != 1)
        {
            diagnostics.Add(Error(
                "csv.specification.normative-keyword.count",
                "A specification statement must contain exactly one approved uppercase normative keyword: MUST, MUST NOT, SHALL, SHALL NOT, SHOULD, SHOULD NOT, or MAY.",
                target));
            return string.Empty;
        }

        int sentenceCount = SentenceEndingRegex().Matches(statement).Count;
        if (sentenceCount > 1)
        {
            diagnostics.Add(new ProviderDiagnostic(
                "csv.specification.statement.multiple-sentences",
                ProviderDiagnosticSeverity.Warning,
                "The atomic normative statement contains multiple sentence endings; normally use one sentence for one obligation.",
                target));
        }

        return matches[0].Value;
    }

    private static void ParseMetadata(
        IReadOnlyDictionary<string, string> fields,
        IDictionary<string, JsonElement> content,
        ICollection<ProviderDiagnostic> diagnostics,
        string target)
    {
        string metadataJson = Value(fields, "metadata_json").Trim();
        if (string.IsNullOrEmpty(metadataJson))
        {
            return;
        }

        try
        {
            JsonElement metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);
            if (metadata.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Error("csv.metadata.object.required", "metadata_json must contain one JSON object.", target));
                return;
            }

            content["metadata"] = metadata;
        }
        catch (JsonException exception)
        {
            diagnostics.Add(Error("csv.metadata.json.invalid", $"metadata_json is invalid: {exception.Message}", target));
        }
    }

    private static string RequiredString(JsonElement content, string name)
    {
        if (!content.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw InvalidContent("csv.content.field.required", $"Content requires a non-empty string '{name}'.");
        }

        return value.GetString()!;
    }

    private static void CopyOptionalString(
        JsonElement content,
        IDictionary<string, string> fields,
        string propertyName,
        string? columnName = null)
    {
        if (!content.TryGetProperty(propertyName, out JsonElement value))
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw InvalidContent("csv.content.field.invalid", $"Content property '{propertyName}' must be a string.");
        }

        fields[columnName ?? propertyName] = value.GetString() ?? string.Empty;
    }

    private static void AddOptional(IDictionary<string, JsonElement> content, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            content[name] = JsonSerializer.SerializeToElement(value);
        }
    }

    private static void Require(
        string value,
        string code,
        string message,
        string target,
        ICollection<ProviderDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error(code, message, target));
        }
    }

    private static string Value(IReadOnlyDictionary<string, string> fields, string name)
    {
        return fields.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    [GeneratedRegex(@"\b(?:MUST NOT|SHALL NOT|SHOULD NOT|MUST|SHALL|SHOULD|MAY)\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NormativeKeywordRegex();

    [GeneratedRegex(@"[.!?](?:\s|$)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SentenceEndingRegex();
}
