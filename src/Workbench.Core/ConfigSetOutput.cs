namespace Workbench
{
    public sealed record ConfigSetOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ConfigSetData Data);
}
