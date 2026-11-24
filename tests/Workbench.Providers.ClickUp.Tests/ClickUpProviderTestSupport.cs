using System.Net;
using System.Text;
using Workbench.Core;

namespace Workbench.Providers.ClickUp.Tests;

internal static class ClickUpProviderTestSupport
{
    public static ClickUpProviderOptions Options(
        string? stateDirectory = null,
        ClickUpProviderMode mode = ClickUpProviderMode.ReadOnly,
        int maximumHttpAttempts = 3)
    {
        return new ClickUpProviderOptions
        {
            ListId = "list-42",
            AuthorityEpoch = "epoch-1",
            Mode = mode,
            StateDirectory = stateDirectory,
            LocalReferenceCustomFieldId = "local-reference-field",
            ApiBaseUri = new Uri("https://mock.clickup.invalid/api/v2/"),
            MaximumHttpAttempts = maximumHttpAttempts
        };
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public static string TaskJson(
        string id = "task-1",
        string status = "in progress",
        string priorityId = "2",
        string dateUpdated = "1700000000000",
        bool archived = false,
        string localReference = "WI-WB-0042")
    {
        return $$"""
            {
              "id": "{{id}}",
              "name": "Ship provider",
              "description": "must stay outside the execution resource",
              "status": { "status": "{{status}}" },
              "priority": { "priority": "high", "id": "{{priorityId}}" },
              "assignees": [ { "id": 27 }, { "id": 11 } ],
              "start_date": "1699900000000",
              "due_date": "1700500000000",
              "date_updated": "{{dateUpdated}}",
              "archived": {{archived.ToString().ToLowerInvariant()}},
              "url": "https://app.clickup.com/t/{{id}}",
              "list": { "id": "list-42" },
              "custom_fields": [
                { "id": "local-reference-field", "value": "{{localReference}}" }
              ]
            }
            """;
    }

    public static string CustomFieldsJson()
    {
        return """
            {
              "fields": [
                {
                  "id": "local-reference-field",
                  "type": "short_text",
                  "applied_objects": [ { "object_type": 19, "object_id": 0 } ]
                }
              ]
            }
            """;
    }

    public static ResourceProvenance Provenance(string revision = "local-1")
    {
        return new ResourceProvenance(
            "workbench",
            new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero),
            "test",
            revision,
            new string('a', 64),
            TransferId: null,
            ApprovalReference: null);
    }

    public static JsonElement Content(
        string status = "done",
        bool archived = false,
        string remoteTaskId = "task-1")
    {
        var content = new ClickUpWorkItemExecutionContent(
            "Local bootstrap title",
            status,
            ClickUpPriority.Normal,
            ["11", "27"],
            DateTimeOffset.FromUnixTimeMilliseconds(1699900000000),
            DateTimeOffset.FromUnixTimeMilliseconds(1700500000000),
            archived,
            remoteTaskId,
            $"https://app.clickup.com/t/{remoteTaskId}",
            "WI-WB-0042",
            "1700000000000");
        return JsonSerializer.SerializeToElement(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
