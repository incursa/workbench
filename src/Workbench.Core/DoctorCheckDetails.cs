namespace Workbench
{
    public sealed record DoctorCheckDetails(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("missing")] IList<string>? Missing,
        [property: JsonPropertyName("schemaErrors")] IList<string>? SchemaErrors);
}
