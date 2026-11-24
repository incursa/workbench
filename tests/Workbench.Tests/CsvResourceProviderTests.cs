using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Workbench.Core;
using Workbench.CsvProvider;

namespace Workbench.Tests;

[TestClass]
public sealed class CsvResourceProviderTests
{
    private static readonly DateTimeOffset now = new(2026, 11, 22, 8, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Authority_PlanApplyAndRetry_PreservesSpreadsheetContentAndRejectsStaleEditsAsync()
    {
        using TempDirectory directory = new();
        CsvResourceProvider provider = CreateAuthority(directory.Path);
        JsonElement content = JsonSerializer.SerializeToElement(new
        {
            id = "WI-WB-1001",
            title = "Unicode résumé, quoted \"title\"",
            status = "ready",
            type = "feature",
            description = "Line one\nLine two",
            metadata = new { team = "platform" },
            csvExtensions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["spreadsheet_note"] = "Preserve me, including \"quotes\" and Ω."
            }
        });
        ResourceChange create = CreateChange(ResourceChangeKind.Create, CsvResourceTypes.WorkItem, "WI-WB-1001", content);

        ProviderPlanChangesResponse plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            "epoch-1",
            [create]));

        Assert.HasCount(1, plan.Changes);
        Assert.AreEqual(ProviderChangeDisposition.Ready, plan.Changes[0].Disposition);
        Assert.IsTrue(plan.RequiresExplicitApproval);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "work-items.v1.csv")), "Planning must not mutate CSV content.");

        ProviderApplyChangesRequest applyRequest = new(
            CsvResourceTypes.WorkItem,
            "roadmap",
            plan.PlanId,
            plan.PlanDigest,
            "apply-create-1001",
            "epoch-1",
            new ProviderApproval("samuel", "review:1001", now));
        ProviderApplyChangesResponse applied = await provider.ApplyChangesAsync(applyRequest);

        Assert.AreEqual(ProviderApplyStatus.Applied, applied.Status);
        Assert.IsNotNull(applied.Snapshot);
        Assert.HasCount(1, applied.Snapshot.Resources);
        string csvPath = Path.Combine(directory.Path, "work-items.v1.csv");
        byte[] csvBytes = await File.ReadAllBytesAsync(csvPath);
        Assert.IsTrue(csvBytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "CSV should carry a UTF-8 BOM for spreadsheet compatibility.");
        string csvText = Encoding.UTF8.GetString(csvBytes);
        Assert.IsTrue(csvText.Contains("\r\n", StringComparison.Ordinal));
        Assert.IsTrue(csvText.Contains("Unicode résumé", StringComparison.Ordinal));
        Assert.IsTrue(csvText.Contains("Line one\nLine two", StringComparison.Ordinal));
        Assert.IsTrue(csvText.Contains("spreadsheet_note", StringComparison.Ordinal));

        ProviderApplyChangesResponse retry = await provider.ApplyChangesAsync(applyRequest);
        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, retry.Status);
        Assert.AreEqual(applied.Snapshot.PayloadSha256, retry.Snapshot?.PayloadSha256);

        ProviderQueryResponse query = await provider.QueryAsync(new ProviderQueryRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            JsonSerializer.SerializeToElement(new { status = "ready" }),
            10,
            null));
        Assert.HasCount(1, query.Resources);

        RewriteCsv(csvPath, rows =>
        {
            rows[0]["title"] = "Edited outside plan/apply";
            return rows;
        });
        ResourceProviderProtocolException exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.QueryAsync(new ProviderQueryRequest(CsvResourceTypes.WorkItem, "roadmap", null, 10, null)));
        Assert.AreEqual("csv.authority.content-unplanned", exception.Error.Code);
    }

    [TestMethod]
    public async Task Specification_Planning_EnforcesExactlyOneApprovedNormativeKeywordAsync()
    {
        using TempDirectory directory = new();
        CsvResourceProvider provider = CreateAuthority(directory.Path, "requirements");
        JsonElement missingKeyword = Specification("REQ-CSV-0001", "The provider validates each row.", "");
        JsonElement duplicateKeyword = Specification(
            "REQ-CSV-0002",
            "The provider MUST validate each row and SHOULD report each failure.",
            "MUST");
        JsonElement valid = Specification(
            "REQ-CSV-0003",
            "The provider MUST preserve Unicode. A reviewer can still prefer one sentence.",
            "MUST");

        ProviderPlanChangesResponse plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            CsvResourceTypes.Specification,
            "requirements",
            "epoch-1",
            [
                CreateChange(ResourceChangeKind.Create, CsvResourceTypes.Specification, "REQ-CSV-0001", missingKeyword),
                CreateChange(ResourceChangeKind.Create, CsvResourceTypes.Specification, "REQ-CSV-0002", duplicateKeyword),
                CreateChange(ResourceChangeKind.Create, CsvResourceTypes.Specification, "REQ-CSV-0003", valid)
            ]));

        Assert.AreEqual(ProviderChangeDisposition.Invalid, plan.Changes[0].Disposition);
        Assert.AreEqual("csv.specification.normative-keyword.count", plan.Changes[0].Diagnostics[0].Code);
        Assert.AreEqual(ProviderChangeDisposition.Invalid, plan.Changes[1].Disposition);
        Assert.AreEqual(ProviderChangeDisposition.Ready, plan.Changes[2].Disposition);
        Assert.IsTrue(plan.Changes[2].Diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Code, "csv.specification.statement.multiple-sentences", StringComparison.Ordinal)
            && diagnostic.Severity == ProviderDiagnosticSeverity.Warning));
    }

    [TestMethod]
    public void Projection_RoundTripsAcrossSpreadsheetConventions_AndRequiresExplicitOperations()
    {
        using TempDirectory directory = new();
        ResourceDocument first = CreateDocument(
            CsvResourceTypes.Specification,
            "requirements",
            Specification(
                "REQ-CSV-0100",
                "The adapter MUST preserve commas, \"quotes\", Unicode Ω, and embedded\nnewlines.",
                "MUST",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["owner_column"] = "François" }),
            "external-v1");
        ResourceDocument second = CreateDocument(
            CsvResourceTypes.Specification,
            "requirements",
            Specification("REQ-CSV-0101", "The adapter MUST require explicit deletion.", "MUST"),
            "external-v2");
        ResourceSnapshot baseline = CreateSnapshot([first, second]);
        string csvPath = CsvProjectionReconciler.Export(baseline, directory.Path);

        RewriteCsv(csvPath, rows =>
        {
            rows[0]["operation"] = "upsert";
            rows[0]["statement"] = "The adapter MUST preserve edited commas, \"quotes\", Unicode 漢字, and embedded\nnewlines.";
            rows.RemoveAt(1);
            return rows;
        }, emitBom: false, newLine: "\n");

        CsvReconciliationPreview preview = CsvProjectionReconciler.Preview(baseline, directory.Path);

        Assert.IsTrue(preview.CanPlan);
        Assert.IsNotNull(preview.PlanRequest);
        Assert.HasCount(1, preview.PlanRequest.Changes);
        Assert.AreEqual(ResourceChangeKind.Update, preview.PlanRequest.Changes[0].Kind);
        Assert.AreEqual("external-v1", preview.PlanRequest.Changes[0].ExpectedVersionToken);
        Assert.IsTrue(preview.Diagnostics.Any(diagnostic =>
            string.Equals(diagnostic.Code, "csv.projection.rows.omitted", StringComparison.Ordinal)));
        JsonElement? proposedContent = preview.PlanRequest.Changes[0].Content;
        Assert.IsTrue(proposedContent.HasValue);
        JsonElement extension = proposedContent.GetValueOrDefault()
            .GetProperty("csvExtensions")
            .GetProperty("owner_column");
        Assert.AreEqual("François", extension.GetString());
    }

    [TestMethod]
    public void Projection_RejectsStaleCoordinatesAndDoesNotInferEditsAsync()
    {
        using TempDirectory directory = new();
        ResourceDocument resource = CreateDocument(
            CsvResourceTypes.Specification,
            "requirements",
            Specification("REQ-CSV-0200", "The adapter MUST detect stale edits.", "MUST"),
            "external-v1");
        ResourceSnapshot baseline = CreateSnapshot([resource]);
        string csvPath = CsvProjectionReconciler.Export(baseline, directory.Path);

        RewriteCsv(csvPath, rows =>
        {
            rows[0]["statement"] = "The adapter MUST detect every stale edit.";
            return rows;
        });
        CsvReconciliationPreview unmarked = CsvProjectionReconciler.Preview(baseline, directory.Path);
        Assert.IsFalse(unmarked.CanPlan);
        Assert.AreEqual("csv.projection.operation.required", unmarked.Changes[0].Diagnostics[0].Code);

        RewriteCsv(csvPath, rows =>
        {
            rows[0]["operation"] = "upsert";
            rows[0]["base_version_token"] = "stale-version";
            return rows;
        });
        CsvReconciliationPreview stale = CsvProjectionReconciler.Preview(baseline, directory.Path);
        Assert.IsFalse(stale.CanPlan);
        Assert.AreEqual(ProviderChangeDisposition.Conflict, stale.Changes[0].Disposition);
        Assert.AreEqual("csv.projection.base.stale", stale.Changes[0].Diagnostics[0].Code);
    }

    [TestMethod]
    public async Task Authority_RetryAfterLaterApply_ReturnsOriginalPersistedSnapshotAsync()
    {
        using TempDirectory directory = new();
        CsvResourceProvider provider = CreateAuthority(directory.Path);
        JsonElement firstContent = JsonSerializer.SerializeToElement(new
        {
            id = "WI-WB-1100",
            title = "First",
            status = "ready"
        });
        ResourceChange firstChange = CreateChange(
            ResourceChangeKind.Create,
            CsvResourceTypes.WorkItem,
            "WI-WB-1100",
            firstContent);
        ProviderPlanChangesResponse firstPlan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            "epoch-1",
            [firstChange]));
        ProviderApplyChangesRequest firstApply = new(
            CsvResourceTypes.WorkItem,
            "roadmap",
            firstPlan.PlanId,
            firstPlan.PlanDigest,
            "apply-first",
            "epoch-1",
            new ProviderApproval("samuel", "review:first", now));
        ProviderApplyChangesResponse first = await provider.ApplyChangesAsync(firstApply);

        JsonElement secondContent = JsonSerializer.SerializeToElement(new
        {
            id = "WI-WB-1101",
            title = "Second",
            status = "ready"
        });
        ResourceChange secondChange = CreateChange(
            ResourceChangeKind.Create,
            CsvResourceTypes.WorkItem,
            "WI-WB-1101",
            secondContent);
        ProviderPlanChangesResponse secondPlan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            "epoch-1",
            [secondChange]));
        _ = await provider.ApplyChangesAsync(new ProviderApplyChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            secondPlan.PlanId,
            secondPlan.PlanDigest,
            "apply-second",
            "epoch-1",
            new ProviderApproval("samuel", "review:second", now)));

        ProviderApplyChangesResponse retry = await provider.ApplyChangesAsync(firstApply);

        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, retry.Status);
        Assert.AreEqual(first.Snapshot?.SnapshotId, retry.Snapshot?.SnapshotId);
        Assert.AreEqual(first.Snapshot?.PayloadSha256, retry.Snapshot?.PayloadSha256);
        Assert.HasCount(1, retry.Snapshot!.Resources);
    }

    [TestMethod]
    public async Task Authority_RejectsTamperedPersistedPlanAsync()
    {
        using TempDirectory directory = new();
        CsvResourceProvider provider = CreateAuthority(directory.Path);
        JsonElement content = JsonSerializer.SerializeToElement(new
        {
            id = "WI-WB-1200",
            title = "Original title",
            status = "ready"
        });
        ProviderPlanChangesResponse plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            "epoch-1",
            [CreateChange(ResourceChangeKind.Create, CsvResourceTypes.WorkItem, "WI-WB-1200", content)]));
        string planPath = Path.Combine(
            directory.Path,
            ".workbench-csv-provider",
            "plans",
            $"{plan.PlanId}.json");
        string persisted = await File.ReadAllTextAsync(planPath);
        await File.WriteAllTextAsync(
            planPath,
            persisted.Replace("Original title", "Tampered title", StringComparison.Ordinal));

        ProviderApplyChangesResponse response = await provider.ApplyChangesAsync(new ProviderApplyChangesRequest(
            CsvResourceTypes.WorkItem,
            "roadmap",
            plan.PlanId,
            plan.PlanDigest,
            "apply-tampered-plan",
            "epoch-1",
            new ProviderApproval("samuel", "review:tampered", now)));

        Assert.AreEqual(ProviderApplyStatus.Conflict, response.Status);
        Assert.AreEqual("csv.plan.integrity.invalid", response.Diagnostics.Single().Code);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "work-items.v1.csv")));
    }

    private static CsvResourceProvider CreateAuthority(string root, string resourceSet = "roadmap")
    {
        return new CsvResourceProvider(new CsvProviderOptions
        {
            RootDirectory = root,
            ResourceSet = resourceSet,
            ProviderId = "csv-authority",
            AuthorityProviderId = "csv-authority",
            AuthorityEpoch = "epoch-1",
            Role = ResourceProviderRole.Authority
        }, new FixedTimeProvider(now));
    }

    private static ResourceChange CreateChange(
        ResourceChangeKind kind,
        string resourceType,
        string id,
        JsonElement content,
        string? expectedVersion = null)
    {
        string digest = Digest(content);
        return new ResourceChange(
            $"change-{id}",
            kind,
            new ResourceAddress(
                resourceType,
                string.Equals(resourceType, CsvResourceTypes.WorkItem, StringComparison.Ordinal) ? "roadmap" : "requirements",
                id),
            content,
            expectedVersion,
            digest,
            new ResourceProvenance("test", now, "samuel", "test-source", digest, null, null));
    }

    private static JsonElement Specification(
        string id,
        string statement,
        string normativeKeyword,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        Dictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["title"] = $"Requirement {id}",
            ["status"] = "draft",
            ["statement"] = statement,
            ["normativeKeyword"] = normativeKeyword,
            ["notes"] = "Spreadsheet note\nSecond line",
            ["metadata"] = new { source = "test" }
        };
        if (extensions is not null)
        {
            content["csvExtensions"] = extensions;
        }

        return JsonSerializer.SerializeToElement(content);
    }

    private static ResourceDocument CreateDocument(
        string resourceType,
        string resourceSet,
        JsonElement content,
        string versionToken)
    {
        string digest = Digest(content);
        return new ResourceDocument(
            new ResourceAddress(resourceType, resourceSet, content.GetProperty("id").GetString()!),
            content,
            versionToken,
            digest,
            null,
            new ResourceProvenance("external-authority", now, "owner", "revision-1", digest, null, null));
    }

    private static ResourceSnapshot CreateSnapshot(IReadOnlyList<ResourceDocument> resources)
    {
        string resourceType = resources[0].Address.ResourceType;
        string resourceSet = resources[0].Address.ResourceSet;
        string digest = SnapshotDigest(resources);
        ResourceAuthorityDeclaration authority = new(
            resourceType,
            resourceSet,
            "external-authority",
            "external-epoch-1",
            now,
            null);
        return new ResourceSnapshot(
            "snapshot-external-1",
            resourceType,
            resourceSet,
            now,
            digest,
            true,
            authority,
            null,
            resources,
            new ResourceProvenance("external-authority", now, "owner", "revision-1", digest, null, null));
    }

    private static string SnapshotDigest(IEnumerable<ResourceDocument> resources)
    {
        JsonElement coordinates = JsonSerializer.SerializeToElement(resources
            .OrderBy(resource => resource.Address.ResourceId, StringComparer.Ordinal)
            .Select(resource => new
            {
                resource.Address.ResourceId,
                resource.VersionToken,
                resource.PayloadSha256
            }));
        return Digest(coordinates);
    }

    private static string Digest(JsonElement element)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            WriteCanonical(writer, element);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }

            writer.WriteEndObject();
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray())
            {
                WriteCanonical(writer, item);
            }

            writer.WriteEndArray();
            return;
        }

        element.WriteTo(writer);
    }

    private static void RewriteCsv(
        string path,
        Func<List<Dictionary<string, string>>, List<Dictionary<string, string>>> update,
        bool emitBom = true,
        string newLine = "\r\n")
    {
        List<string> headers;
        List<Dictionary<string, string>> rows = [];
        using (StreamReader reader = new(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        using (CsvReader csv = new(reader, CultureInfo.InvariantCulture))
        {
            Assert.IsTrue(csv.Read());
            csv.ReadHeader();
            headers = [.. csv.HeaderRecord ?? []];
            while (csv.Read())
            {
                Dictionary<string, string> row = new(StringComparer.Ordinal);
                foreach (string header in headers)
                {
                    row[header] = csv.GetField(header) ?? string.Empty;
                }

                rows.Add(row);
            }
        }

        rows = update(rows);
        CsvConfiguration configuration = new(CultureInfo.InvariantCulture)
        {
            NewLine = newLine,
            ShouldQuote = _ => true
        };
        using StreamWriter writer = new(path, append: false, new UTF8Encoding(emitBom))
        {
            NewLine = newLine
        };
        using CsvWriter csvWriter = new(writer, configuration);
        foreach (string header in headers)
        {
            csvWriter.WriteField(header);
        }

        csvWriter.NextRecord();
        foreach (IReadOnlyDictionary<string, string> row in rows)
        {
            foreach (string header in headers)
            {
                csvWriter.WriteField(row.GetValueOrDefault(header, string.Empty));
            }

            csvWriter.NextRecord();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"workbench-csv-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
