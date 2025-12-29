namespace Workbench
{
    public sealed record NormalizeOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] NormalizeData Data);
}
