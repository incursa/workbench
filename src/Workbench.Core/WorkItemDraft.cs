using System.Text.Json.Serialization;

namespace Workbench;

public sealed record WorkItemDraft(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("acceptanceCriteria")] IList<string>? AcceptanceCriteria,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("tags")] IList<string>? Tags);
