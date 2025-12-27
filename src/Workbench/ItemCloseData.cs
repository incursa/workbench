namespace Workbench
{
    public sealed record ItemCloseData(
        [property: JsonPropertyName("item")] WorkItemPayload Item,
        [property: JsonPropertyName("moved")] bool Moved);
}