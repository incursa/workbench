namespace Workbench
{
    public sealed record DocDeleteData(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated);
}
