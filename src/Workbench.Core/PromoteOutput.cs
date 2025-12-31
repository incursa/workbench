namespace Workbench
{
    public sealed record PromoteOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] PromoteData Data);
}
