namespace Workbench
{
    public sealed record PrOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] PrData Data);
}