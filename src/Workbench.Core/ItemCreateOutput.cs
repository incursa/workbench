namespace Workbench
{
    public sealed record ItemCreateOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemCreateData Data);
}
