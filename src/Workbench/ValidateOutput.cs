namespace Workbench
{
    public sealed record ValidateOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ValidateData Data);
}