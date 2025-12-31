using System.Text.Json.Serialization;

namespace Workbench;

public sealed record DoctorOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] DoctorData Data);
