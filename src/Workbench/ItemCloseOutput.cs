namespace Workbench
{
    public sealed record ItemCloseOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemCloseData Data);
}