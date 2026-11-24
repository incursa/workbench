using System.Text.Json.Serialization;
using Workbench.Core;

#pragma warning disable MA0048 // ClickUp wire and persistence DTOs are intentionally colocated.

namespace Workbench.Providers.ClickUp;

[JsonConverter(typeof(JsonStringEnumConverter<ClickUpPriority>))]
public enum ClickUpPriority
{
    [JsonStringEnumMemberName("urgent")]
    Urgent,
    [JsonStringEnumMemberName("high")]
    High,
    [JsonStringEnumMemberName("normal")]
    Normal,
    [JsonStringEnumMemberName("low")]
    Low
}

/// <summary>
/// Complete ClickUp-owned execution resource. Description and Workbench-owned trace/evidence links are intentionally absent.
/// </summary>
public sealed record ClickUpWorkItemExecutionContent(
    string Title,
    string OperationalStatus,
    ClickUpPriority? Priority,
    IReadOnlyList<string> AssigneeIds,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? DueAtUtc,
    bool Archived,
    string RemoteTaskId,
    string Url,
    string? LocalReference,
    string SourceRevision);

internal sealed record ClickUpTask(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("status")]
    ClickUpStatus Status,
    [property: JsonPropertyName("priority")]
    ClickUpPriorityValue? Priority,
    [property: JsonPropertyName("assignees")]
    IReadOnlyList<ClickUpUser>? Assignees,
    [property: JsonPropertyName("start_date")]
    string? StartDate,
    [property: JsonPropertyName("due_date")]
    string? DueDate,
    [property: JsonPropertyName("date_updated")]
    string DateUpdated,
    [property: JsonPropertyName("archived")]
    bool Archived,
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("list")]
    ClickUpList? List,
    [property: JsonPropertyName("custom_fields")]
    IReadOnlyList<ClickUpCustomField>? CustomFields);

internal sealed record ClickUpList([property: JsonPropertyName("id")] string Id);

internal sealed record ClickUpStatus([property: JsonPropertyName("status")] string Status);

internal sealed record ClickUpPriorityValue(
    [property: JsonPropertyName("priority")] string? Priority,
    [property: JsonPropertyName("id")] string? Id);

internal sealed record ClickUpUser([property: JsonPropertyName("id")] JsonElement Id);

internal sealed record ClickUpCustomField(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("value")] JsonElement? Value);

internal sealed record ClickUpTaskPage(
    [property: JsonPropertyName("tasks")]
    IReadOnlyList<ClickUpTask> Tasks,
    [property: JsonPropertyName("last_page")]
    bool? LastPage);

internal sealed record ClickUpCustomFieldPage(
    [property: JsonPropertyName("fields")]
    IReadOnlyList<ClickUpCustomFieldDefinition> Fields);

internal sealed record ClickUpCustomFieldDefinition(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("applied_objects")]
    IReadOnlyList<ClickUpAppliedObject>? AppliedObjects);

internal sealed record ClickUpAppliedObject(
    [property: JsonPropertyName("object_type")]
    JsonElement ObjectType,
    [property: JsonPropertyName("object_id")]
    JsonElement ObjectId);

internal sealed record ClickUpApiError(
    [property: JsonPropertyName("err")]
    string? Err,
    [property: JsonPropertyName("ECODE")]
    string? Ecode);

internal sealed record StoredPlan(
    string PlanId,
    string PlanDigest,
    string AuthorityEpoch,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    ResourceChange Change,
    ProviderPlannedChange PlannedChange);

internal sealed record StoredReceipt(
    string RequestFingerprint,
    ProviderApplyChangesResponse Response);

internal sealed record StoredPendingCreate(
    string RequestFingerprint,
    string LocalReference,
    string? RemoteTaskId);

internal sealed record QueryContinuation(int Page, int Offset, bool Archived);

#pragma warning restore MA0048
