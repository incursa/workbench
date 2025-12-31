namespace Workbench
{
    public sealed record ItemNormalizeData(
        [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
        [property: JsonPropertyName("dryRun")] bool DryRun);
}
