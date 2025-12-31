namespace Workbench
{
    public sealed record ItemNormalizeOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemNormalizeData Data);
}
