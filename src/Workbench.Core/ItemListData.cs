namespace Workbench
{
    public sealed record ItemListData(
        [property: JsonPropertyName("items")] IList<ItemSummary> Items);
}
