namespace Workbench
{
    public sealed record ItemStatusData(
        [property: JsonPropertyName("item")] WorkItemPayload Item);
}
