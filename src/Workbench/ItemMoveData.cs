namespace Workbench
{
    public sealed record ItemMoveData(
        [property: JsonPropertyName("item")] WorkItemPayload Item);
}