namespace Workbench
{
    public sealed record ItemRenameData(
        [property: JsonPropertyName("item")] WorkItemPayload Item);
}
