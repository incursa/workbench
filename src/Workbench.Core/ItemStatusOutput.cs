namespace Workbench
{
    public sealed record ItemStatusOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemStatusData Data);
}
