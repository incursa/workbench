namespace Workbench
{
    public sealed record DocCreateOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] DocCreateData Data);
}
