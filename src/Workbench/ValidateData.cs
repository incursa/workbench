namespace Workbench
{
    public sealed record ValidateData(
        [property: JsonPropertyName("errors")] IList<string> Errors,
        [property: JsonPropertyName("warnings")] IList<string> Warnings,
        [property: JsonPropertyName("counts")] ValidateCounts Counts);
}
