namespace Workbench
{
    public sealed record NormalizeData(
        [property: JsonPropertyName("itemsUpdated")] int ItemsUpdated,
        [property: JsonPropertyName("docsUpdated")] int DocsUpdated,
        [property: JsonPropertyName("dryRun")] bool DryRun,
        [property: JsonPropertyName("itemsNormalized")] bool ItemsNormalized,
        [property: JsonPropertyName("docsNormalized")] bool DocsNormalized);
}
