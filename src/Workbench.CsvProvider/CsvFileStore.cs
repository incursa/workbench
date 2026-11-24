using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Workbench.Core;

namespace Workbench.CsvProvider;

internal sealed class CsvFileStore(CsvProviderOptions options, TimeProvider timeProvider)
{
    public CsvTable Load(string resourceType, CsvRecordMode expectedMode)
    {
        string path = options.GetCsvPath(resourceType);
        if (!File.Exists(path))
        {
            return new CsvTable(CsvContentCodec.GetHeaders(resourceType), [], HashBytes([]));
        }

        byte[] bytes = File.ReadAllBytes(path);
        string fileDigest = HashBytes(bytes);
        using MemoryStream stream = new(bytes);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using CsvReader csv = new(reader, CreateReaderConfiguration());

        try
        {
            if (!csv.Read())
            {
                throw CsvContentCodec.InvalidContent("csv.file.empty", $"CSV file '{path}' is empty.");
            }

            csv.ReadHeader();
            string[] headers = csv.HeaderRecord ?? [];
            ValidateHeaders(resourceType, headers, path);
            List<CsvRowDocument> rows = [];
            HashSet<string> resourceIds = new(StringComparer.Ordinal);

            while (csv.Read())
            {
                Dictionary<string, string> fields = new(StringComparer.Ordinal);
                for (int index = 0; index < headers.Length; index++)
                {
                    fields[headers[index]] = csv.GetField(index) ?? string.Empty;
                }

                CsvRowDocument row = this.ParseRow(resourceType, expectedMode, fields, csv.Context.Parser?.Row ?? 0);
                if (!resourceIds.Add(row.Document.Address.ResourceId))
                {
                    throw CsvContentCodec.InvalidContent(
                        "csv.resource.id.duplicate",
                        $"CSV file '{path}' contains duplicate resource ID '{row.Document.Address.ResourceId}'.");
                }

                rows.Add(row);
            }

            return new CsvTable(headers, rows, fileDigest);
        }
        catch (ResourceProviderProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CsvHelperException or FormatException or InvalidDataException)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.file.invalid",
                $"CSV file '{path}' is invalid: {exception.Message}");
        }
    }

    public void Save(
        string resourceType,
        IEnumerable<IReadOnlyDictionary<string, string>> rows,
        IEnumerable<string>? preservedHeaders = null)
    {
        string path = options.GetCsvPath(resourceType);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        List<IReadOnlyDictionary<string, string>> materializedRows = rows.ToList();
        List<string> headers = [.. CsvContentCodec.GetHeaders(resourceType)];
        foreach (string header in preservedHeaders ?? [])
        {
            if (!headers.Contains(header, StringComparer.Ordinal))
            {
                headers.Add(header);
            }
        }

        foreach (string header in materializedRows
                     .SelectMany(row => row.Keys)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(header => header, StringComparer.Ordinal))
        {
            if (!headers.Contains(header, StringComparer.Ordinal))
            {
                headers.Add(header);
            }
        }

        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
            {
                NewLine = "\r\n"
            };
            using CsvWriter csv = new(writer, CreateWriterConfiguration());
            foreach (string header in headers)
            {
                csv.WriteField(header);
            }

            csv.NextRecord();
            foreach (IReadOnlyDictionary<string, string> row in materializedRows)
            {
                foreach (string header in headers)
                {
                    csv.WriteField(row.TryGetValue(header, out string? value) ? value : string.Empty);
                }

                csv.NextRecord();
            }

            writer.Flush();
            stream.Flush(flushToDisk: true);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ResourceSnapshot CreateSnapshot(string resourceType, CsvTable table, bool authoritative)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        ResourceAuthorityDeclaration authority = new(
            resourceType,
            options.ResourceSet,
            options.AuthorityProviderId,
            options.AuthorityEpoch,
            now,
            DeclarationDigest: null);
        IReadOnlyList<ResourceDocument> resources = table.Rows
            .OrderBy(row => row.Document.Address.ResourceId, StringComparer.Ordinal)
            .Select(row => row.Document)
            .ToList();
        string payloadSha256 = ComputeSnapshotDigest(resources);
        ResourceProvenance provenance = new(
            options.ProviderId,
            now,
            Actor: null,
            table.FileDigest,
            payloadSha256,
            TransferId: null,
            ApprovalReference: null);
        return new ResourceSnapshot(
            $"csv-snapshot-{payloadSha256}",
            resourceType,
            options.ResourceSet,
            now,
            payloadSha256,
            authoritative,
            authority,
            CompatibilityPin: null,
            resources,
            provenance);
    }

    public static string ComputeSnapshotDigest(IEnumerable<ResourceDocument> resources)
    {
        var coordinates = resources
            .OrderBy(resource => resource.Address.ResourceId, StringComparer.Ordinal)
            .Select(resource => new
            {
                resource.Address.ResourceId,
                resource.VersionToken,
                resource.PayloadSha256
            });
        JsonElement json = JsonSerializer.SerializeToElement(coordinates);
        return HashBytes(CsvCanonicalJson.Canonicalize(json));
    }

    public static string HashBytes(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private CsvRowDocument ParseRow(
        string resourceType,
        CsvRecordMode expectedMode,
        IReadOnlyDictionary<string, string> fields,
        int rowNumber)
    {
        string target = $"{Path.GetFileName(options.GetCsvPath(resourceType))}:{rowNumber}";
        string schema = Get(fields, "csv_schema");
        if (!string.Equals(schema, CsvSchemaVersions.ForResourceType(resourceType), StringComparison.Ordinal))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.schema.version.unsupported",
                $"Row {rowNumber} declares unsupported CSV schema '{schema}'.");
        }

        CsvRecordMode mode = ParseMode(Get(fields, "record_mode"), rowNumber);
        if (mode != expectedMode)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.record-mode.mismatch",
                $"Row {rowNumber} uses mode '{mode}' but this file is loaded as '{expectedMode}'.");
        }

        CsvProjectionOperation operation = ParseOperation(Get(fields, "operation"), rowNumber);
        if (mode == CsvRecordMode.Authoritative && operation != CsvProjectionOperation.None)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.authority.operation.invalid",
                $"Authoritative row {rowNumber} cannot carry projection operation '{operation}'.");
        }

        if (!string.Equals(Get(fields, "resource_type"), resourceType, StringComparison.Ordinal)
            || !string.Equals(Get(fields, "resource_set"), options.ResourceSet, StringComparison.Ordinal))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.resource.scope.mismatch",
                $"Row {rowNumber} does not match resource type '{resourceType}' and set '{options.ResourceSet}'.");
        }

        string rowAuthority = Get(fields, "authority_provider_id");
        string rowEpoch = Get(fields, "authority_epoch");
        if (!string.Equals(rowAuthority, options.AuthorityProviderId, StringComparison.Ordinal)
            || !string.Equals(rowEpoch, options.AuthorityEpoch, StringComparison.Ordinal))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.authority.coordinate.mismatch",
                $"Row {rowNumber} authority coordinates are stale or belong to another authority.");
        }

        List<ProviderDiagnostic> diagnostics = [];
        CsvContent content = CsvContentCodec.ParseContent(resourceType, fields, diagnostics, target);
        ProviderDiagnostic? firstError = diagnostics.FirstOrDefault(
            diagnostic => diagnostic.Severity == ProviderDiagnosticSeverity.Error);
        if (firstError is not null)
        {
            throw CsvContentCodec.InvalidContent(firstError.Code, firstError.Message, diagnostics);
        }

        string payloadSha256 = HashBytes(CsvCanonicalJson.Canonicalize(content.Json));
        string versionToken = $"csv-v1:{payloadSha256}";
        string baseVersionToken = Get(fields, "base_version_token");
        string basePayloadSha256 = Get(fields, "base_payload_sha256");
        if (mode == CsvRecordMode.Authoritative
            && (!string.Equals(baseVersionToken, versionToken, StringComparison.Ordinal)
                || !string.Equals(basePayloadSha256, payloadSha256, StringComparison.Ordinal)))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.authority.content-unplanned",
                $"Authoritative row '{content.Id}' was edited without an applied plan; its stored version/digest no longer match its content.",
                [CsvContentCodec.Error(
                    "csv.authority.content-unplanned",
                    "Export an editable projection and reconcile it through validate, plan, approval, and apply.",
                    target)]);
        }

        if (mode == CsvRecordMode.Projection
            && string.IsNullOrEmpty(baseVersionToken) != string.IsNullOrEmpty(basePayloadSha256))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.projection.base.incomplete",
                $"Projection row '{content.Id}' must provide both base version and digest or neither for a new resource.");
        }

        DateTimeOffset recordedAt = ParseTimestamp(Get(fields, "recorded_at_utc"), rowNumber);
        ResourceProvenance provenance = new(
            Get(fields, "provider_id"),
            recordedAt,
            EmptyToNull(Get(fields, "actor")),
            EmptyToNull(Get(fields, "source_revision")),
            payloadSha256,
            EmptyToNull(Get(fields, "transfer_id")),
            EmptyToNull(Get(fields, "approval_reference")));
        ResourceDocument document = new(
            new ResourceAddress(resourceType, options.ResourceSet, content.Id),
            content.Json,
            versionToken,
            payloadSha256,
            CompatibilityPin: null,
            provenance);
        return new CsvRowDocument(document, mode, operation, baseVersionToken, basePayloadSha256, fields);
    }

    private static void ValidateHeaders(string resourceType, IReadOnlyList<string> headers, string path)
    {
        string? duplicate = headers
            .GroupBy(header => header, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicate is not null)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.header.duplicate",
                $"CSV file '{path}' contains duplicate column '{duplicate}'.");
        }

        string? missing = CsvContentCodec.GetHeaders(resourceType)
            .FirstOrDefault(required => !headers.Contains(required, StringComparer.Ordinal));
        if (missing is not null)
        {
            throw CsvContentCodec.InvalidContent(
                "csv.header.required",
                $"CSV file '{path}' is missing required column '{missing}'.");
        }
    }

    private static CsvConfiguration CreateReaderConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            DetectDelimiter = false,
            TrimOptions = TrimOptions.None,
            IgnoreBlankLines = true
        };
    }

    private static CsvConfiguration CreateWriterConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            HasHeaderRecord = true,
            NewLine = "\r\n",
            ShouldQuote = _ => true
        };
    }

    private static CsvRecordMode ParseMode(string value, int rowNumber)
    {
        return value switch
        {
            "authoritative" => CsvRecordMode.Authoritative,
            "projection" => CsvRecordMode.Projection,
            _ => throw CsvContentCodec.InvalidContent(
                "csv.record-mode.unsupported",
                $"Row {rowNumber} has unsupported record_mode '{value}'.")
        };
    }

    private static CsvProjectionOperation ParseOperation(string value, int rowNumber)
    {
        return value switch
        {
            "none" => CsvProjectionOperation.None,
            "upsert" => CsvProjectionOperation.Upsert,
            "delete" => CsvProjectionOperation.Delete,
            _ => throw CsvContentCodec.InvalidContent(
                "csv.projection.operation.unsupported",
                $"Row {rowNumber} has unsupported operation '{value}'.")
        };
    }

    private static DateTimeOffset ParseTimestamp(string value, int rowNumber)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset result))
        {
            throw CsvContentCodec.InvalidContent(
                "csv.provenance.timestamp.invalid",
                $"Row {rowNumber} has invalid recorded_at_utc '{value}'.");
        }

        return result.ToUniversalTime();
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string name)
    {
        return fields.TryGetValue(name, out string? value) ? value : string.Empty;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
