namespace Workbench
{
    public sealed record DocSyncOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] DocSyncData Data);
}
