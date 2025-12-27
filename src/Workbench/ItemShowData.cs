namespace Workbench
{
    public sealed record ItemShowData(
        [property: JsonPropertyName("item")] WorkItemPayload Item);
}