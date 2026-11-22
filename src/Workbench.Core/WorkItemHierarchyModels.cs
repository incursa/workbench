using System.Text.Json.Serialization;

#pragma warning disable MA0048 // Hierarchy DTOs form one versioned serialization contract.

namespace Workbench.Core;

public sealed record WorkItemHierarchyData(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("workItemId")] string WorkItemId,
    [property: JsonPropertyName("parentRef")] string? ParentRef,
    [property: JsonPropertyName("initiativeRefs")] IList<string> InitiativeRefs,
    [property: JsonPropertyName("childRefs")] IList<string> ChildRefs);

public sealed record WorkItemHierarchyOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] WorkItemHierarchyData Data);

#pragma warning restore MA0048
