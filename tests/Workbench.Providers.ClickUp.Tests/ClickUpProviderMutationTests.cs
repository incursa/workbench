using System.Net;
using Workbench.Core;

namespace Workbench.Providers.ClickUp.Tests;

[TestClass]
public sealed class ClickUpProviderMutationTests
{
    [TestMethod]
    public async Task AmbiguousCreateNeverIssuesSecondPostForSameKey()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.CustomFieldsJson()),
            2 or 3 or 4 or 5 or 7 or 8 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                """{ "tasks": [], "last_page": true }"""),
            6 when request.Method == HttpMethod.Post => throw new HttpRequestException("connection closed after send"),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var change = new ResourceChange(
            "ambiguous-create",
            ResourceChangeKind.Create,
            new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "WI-WB-0042"),
            ClickUpProviderTestSupport.Content(remoteTaskId: string.Empty),
            ExpectedVersionToken: null,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var request = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "ambiguous-create-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-create", DateTimeOffset.UtcNow));

        var first = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.ApplyChangesAsync(request));
        var retry = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.ApplyChangesAsync(request));

        Assert.AreEqual("clickup.write.outcome-unknown", first.Error.Code);
        Assert.AreEqual("clickup.create.outcome-unknown", retry.Error.Code);
        Assert.HasCount(1, handler.Requests.Where(sent => sent.Method == HttpMethod.Post));
    }

    [TestMethod]
    public async Task ApplyRejectsTamperedStoredPlanBeforeRemoteMutation()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((_, count) => count is 1 or 2
            ? ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson())
            : throw new AssertFailedException("Tampered plan must fail before another ClickUp request."));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var change = new ResourceChange(
            "tamper-change",
            ResourceChangeKind.Update,
            new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1"),
            ClickUpProviderTestSupport.Content(),
            ExpectedVersionToken: null,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var current = await provider.GetAsync(new ProviderGetRequest([change.Address]));
        change = change with { ExpectedVersionToken = current.Resources[0].VersionToken };
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var planPath = Directory.GetFiles(System.IO.Path.Combine(temporary.Path, "plans"), "*.json").Single();
        var planNode = JsonNode.Parse(await File.ReadAllTextAsync(planPath))!.AsObject();
        planNode["expiresAtUtc"] = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await File.WriteAllTextAsync(planPath, planNode.ToJsonString());

        var result = await provider.ApplyChangesAsync(new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "tamper-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-tamper", DateTimeOffset.UtcNow)));

        Assert.AreEqual(ProviderApplyStatus.Conflict, result.Status);
        Assert.AreEqual("clickup.plan.coordinates.conflict", result.Diagnostics[0].Code);
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task AmbiguousPutIsNotRetriedAndSameKeyReconciles()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 or 2 or 3 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()),
            4 when request.Method == HttpMethod.Put => throw new HttpRequestException("connection closed after send"),
            5 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson(status: "done", priorityId: "3", dateUpdated: "1700000000300")),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var change = new ResourceChange(
            "ambiguous-change",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            ClickUpProviderTestSupport.Content(),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var request = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "ambiguous-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-ambiguous", DateTimeOffset.UtcNow));

        var exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.ApplyChangesAsync(request));
        var reconciled = await provider.ApplyChangesAsync(request);

        Assert.AreEqual("clickup.write.outcome-unknown", exception.Error.Code);
        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, reconciled.Status);
        Assert.HasCount(5, handler.Requests);
        Assert.HasCount(1, handler.Requests.Where(sent => sent.Method == HttpMethod.Put));
    }

    [TestMethod]
    public async Task SuccessfulWriteWithInvalidResponseIsOutcomeUnknownAndReconciles()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 or 2 or 3 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()),
            4 when request.Method == HttpMethod.Put => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, "not-json"),
            5 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson(status: "done", priorityId: "3", dateUpdated: "1700000000400")),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var change = new ResourceChange(
            "invalid-response-change",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            ClickUpProviderTestSupport.Content(),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var request = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "invalid-response-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-invalid-response", DateTimeOffset.UtcNow));

        var exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            provider.ApplyChangesAsync(request));
        var reconciled = await provider.ApplyChangesAsync(request);

        Assert.AreEqual("clickup.write.outcome-unknown", exception.Error.Code);
        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, reconciled.Status);
        Assert.HasCount(1, handler.Requests.Where(sent => sent.Method == HttpMethod.Put));
    }

    [TestMethod]
    public async Task TamperedReceiptFailsClosedBeforeReturningAlreadyApplied()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 or 2 or 3 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()),
            4 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson(status: "done", priorityId: "3", dateUpdated: "1700000000500")),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var change = new ResourceChange(
            "receipt-change",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            ClickUpProviderTestSupport.Content(),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var request = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "receipt-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-receipt", DateTimeOffset.UtcNow));
        var applied = await provider.ApplyChangesAsync(request);
        Assert.AreEqual(ProviderApplyStatus.Applied, applied.Status);
        var receiptPath = Directory.GetFiles(System.IO.Path.Combine(temporary.Path, "receipts"), "*.json").Single();
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath))!.AsObject();
        receipt["response"]!["authorityEpoch"] = "tampered";
        await File.WriteAllTextAsync(receiptPath, receipt.ToJsonString());

        var retried = await provider.ApplyChangesAsync(request);

        Assert.AreEqual(ProviderApplyStatus.Conflict, retried.Status);
        Assert.AreEqual("clickup.idempotency.receipt.invalid", retried.Diagnostics[0].Code);
        Assert.HasCount(4, handler.Requests);
    }

    [TestMethod]
    public async Task CreateUsesLocalReferenceAndDoesNotBlindlyRetry()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.CustomFieldsJson()),
            2 or 3 or 4 or 5 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                """{ "tasks": [], "last_page": true }"""),
            6 when request.Method == HttpMethod.Post => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson()),
            7 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson()),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var change = new ResourceChange(
            "create-1",
            ResourceChangeKind.Create,
            new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "WI-WB-0042"),
            ClickUpProviderTestSupport.Content(remoteTaskId: string.Empty),
            ExpectedVersionToken: null,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        var request = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "create-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-create", DateTimeOffset.UtcNow));

        var applied = await provider.ApplyChangesAsync(request);
        var retry = await provider.ApplyChangesAsync(request);

        Assert.AreEqual(ProviderApplyStatus.Applied, applied.Status);
        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, retry.Status);
        Assert.HasCount(7, handler.Requests);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[5].Method);
        Assert.DoesNotContain("description", handler.Requests[5].Body!, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(handler.Requests[5].Body!, "\"name\":\"Local bootstrap title\"", StringComparison.Ordinal);
        StringAssert.Contains(handler.Requests[5].Body!, "\"id\":\"local-reference-field\"", StringComparison.Ordinal);
        StringAssert.Contains(handler.Requests[5].Body!, "\"value\":\"WI-WB-0042\"", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task PlanApplyAndRetryUpdateExecutionFieldsOnly()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 or 2 or 3 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()),
            4 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson(status: "done", priorityId: "3", dateUpdated: "1700000000100")),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token",
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)));
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var change = new ResourceChange(
            "change-1",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            ClickUpProviderTestSupport.Content(),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());

        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));
        Assert.AreEqual(ProviderChangeDisposition.Ready, plan.Changes[0].Disposition);
        Assert.IsTrue(plan.RequiresExplicitApproval);

        var applyRequest = new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "idempotency-1",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-42", new DateTimeOffset(2026, 11, 22, 12, 1, 0, TimeSpan.Zero)));
        var applied = await provider.ApplyChangesAsync(applyRequest);
        var retried = await provider.ApplyChangesAsync(applyRequest);

        Assert.AreEqual(ProviderApplyStatus.Applied, applied.Status);
        Assert.AreEqual(ProviderApplyStatus.AlreadyApplied, retried.Status);
        Assert.IsNotNull(applied.Snapshot);
        Assert.HasCount(4, handler.Requests);
        Assert.AreEqual(HttpMethod.Put, handler.Requests[3].Method);
        Assert.DoesNotContain("description", handler.Requests[3].Body!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name", handler.Requests[3].Body!, StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(handler.Requests[3].Body!, "\"status\":\"done\"", StringComparison.Ordinal);
        StringAssert.Contains(handler.Requests[3].Body!, "\"priority\":3", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task DeleteChangeArchivesAndNeverUsesRemoteDelete()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((request, count) => count switch
        {
            1 or 2 or 3 => ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()),
            4 => ClickUpProviderTestSupport.Json(
                HttpStatusCode.OK,
                ClickUpProviderTestSupport.TaskJson(dateUpdated: "1700000000200", archived: true)),
            _ => throw new AssertFailedException($"Unexpected ClickUp request {count}: {request.Method} {request.RequestUri}")
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Apply),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var change = new ResourceChange(
            "archive-1",
            ResourceChangeKind.Delete,
            current.Resources[0].Address,
            Content: null,
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());
        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));

        StringAssert.Contains(plan.Diagnostics[0].Code, "maps-to-archive", StringComparison.Ordinal);
        var result = await provider.ApplyChangesAsync(new ProviderApplyChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            plan.PlanId,
            plan.PlanDigest,
            "archive-key",
            "epoch-1",
            new ProviderApproval("reviewer", "approval-archive", DateTimeOffset.UtcNow)));

        Assert.AreEqual(ProviderApplyStatus.Applied, result.Status);
        Assert.IsFalse(handler.Requests.Any(request => request.Method == HttpMethod.Delete));
        Assert.AreEqual(HttpMethod.Put, handler.Requests[3].Method);
        StringAssert.Contains(handler.Requests[3].Body!, "\"archived\":true", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task StaleVersionProducesConflictWithoutMutation()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Plan),
            httpClient,
            "token");
        var change = new ResourceChange(
            "change-stale",
            ResourceChangeKind.Update,
            new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1"),
            ClickUpProviderTestSupport.Content(),
            "clickup:stale:token",
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());

        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));

        Assert.AreEqual(ProviderChangeDisposition.Conflict, plan.Changes[0].Disposition);
        Assert.AreEqual("clickup.version-token.conflict", plan.Changes[0].Diagnostics[0].Code);
        Assert.IsTrue(handler.Requests.All(request => request.Method == HttpMethod.Get));
    }

    [TestMethod]
    public async Task PlanRejectsUnknownDescriptionFieldInsteadOfSilentlyIgnoringIt()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Plan),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var content = JsonNode.Parse(ClickUpProviderTestSupport.Content().GetRawText())!.AsObject();
        content["description"] = "must not be synchronized";
        var change = new ResourceChange(
            "unknown-field",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            JsonSerializer.SerializeToElement(content),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());

        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));

        Assert.AreEqual(ProviderChangeDisposition.Invalid, plan.Changes[0].Disposition);
        Assert.AreEqual("clickup.content.invalid", plan.Changes[0].Diagnostics[0].Code);
        Assert.IsTrue(handler.Requests.All(request => request.Method == HttpMethod.Get));
    }

    [TestMethod]
    public async Task PlanRejectsMissingRequiredAssigneeCollection()
    {
        using var temporary = new TemporaryDirectory();
        var handler = new StubHttpMessageHandler((_, _) =>
            ClickUpProviderTestSupport.Json(HttpStatusCode.OK, ClickUpProviderTestSupport.TaskJson()));
        using var httpClient = new HttpClient(handler);
        var provider = new ClickUpResourceProvider(
            ClickUpProviderTestSupport.Options(temporary.Path, ClickUpProviderMode.Plan),
            httpClient,
            "token");
        var current = await provider.GetAsync(new ProviderGetRequest(
            [new ResourceAddress(ClickUpResourceProvider.ResourceType, "list-42", "task-1")]));
        var content = JsonNode.Parse(ClickUpProviderTestSupport.Content().GetRawText())!.AsObject();
        _ = content.Remove("assigneeIds");
        var change = new ResourceChange(
            "missing-assignees",
            ResourceChangeKind.Update,
            current.Resources[0].Address,
            JsonSerializer.SerializeToElement(content),
            current.Resources[0].VersionToken,
            PayloadSha256: null,
            ClickUpProviderTestSupport.Provenance());

        var plan = await provider.PlanChangesAsync(new ProviderPlanChangesRequest(
            ClickUpResourceProvider.ResourceType,
            "list-42",
            "epoch-1",
            [change]));

        Assert.AreEqual(ProviderChangeDisposition.Invalid, plan.Changes[0].Disposition);
        Assert.AreEqual("clickup.content.invalid", plan.Changes[0].Diagnostics[0].Code);
    }
}
