namespace Workbench
{
    public sealed record CredentialUpdateOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] CredentialUpdateData Data);
}
