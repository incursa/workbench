namespace Workbench
{
    public sealed record RelatedLinksPayload(
        [property: JsonPropertyName("specs")] IList<string> Specs,
        [property: JsonPropertyName("adrs")] IList<string> Adrs,
        [property: JsonPropertyName("files")] IList<string> Files,
        [property: JsonPropertyName("prs")] IList<string> Prs,
        [property: JsonPropertyName("issues")] IList<string> Issues,
        [property: JsonPropertyName("branches")] IList<string> Branches);
}
