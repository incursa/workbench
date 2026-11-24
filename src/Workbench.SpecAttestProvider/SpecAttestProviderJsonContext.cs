using System.Text.Json.Serialization;
using Workbench.Core;

namespace Workbench.SpecAttestProvider;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(ResourceCompatibilityPin))]
[JsonSerializable(typeof(SpecAttestInterchangePackage))]
[JsonSerializable(typeof(SpecAttestInterchangeManifest))]
[JsonSerializable(typeof(SpecAttestInterchangeEntry))]
[JsonSerializable(typeof(IReadOnlyList<SpecAttestInterchangeEntry>))]
[JsonSerializable(typeof(IReadOnlyList<SpecAttestAuditReference>))]
public partial class SpecAttestProviderJsonContext : JsonSerializerContext
{
}
