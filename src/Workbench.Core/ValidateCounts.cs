namespace Workbench
{
    public sealed record ValidateCounts(
        [property: JsonPropertyName("errors")] int Errors,
        [property: JsonPropertyName("warnings")] int Warnings,
        [property: JsonPropertyName("workItems")] int WorkItems,
        [property: JsonPropertyName("markdownFiles")] int MarkdownFiles);
}
