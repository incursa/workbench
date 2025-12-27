namespace Workbench
{
    public sealed record PrData(
        [property: JsonPropertyName("pr")] string Pr,
        [property: JsonPropertyName("item")] string Item);
}