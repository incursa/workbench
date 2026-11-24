using System.Text.Json.Serialization;

namespace Workbench.Core;

/// <summary>Source-generated JSON contract for the provider protocol wire format.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    RespectRequiredConstructorParameters = true,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(ProviderRequestEnvelope))]
[JsonSerializable(typeof(ProviderResponseEnvelope))]
[JsonSerializable(typeof(ProviderHandshakeRequest))]
[JsonSerializable(typeof(ProviderHandshakeResponse))]
[JsonSerializable(typeof(ProviderQueryRequest))]
[JsonSerializable(typeof(ProviderQueryResponse))]
[JsonSerializable(typeof(ProviderGetRequest))]
[JsonSerializable(typeof(ProviderGetResponse))]
[JsonSerializable(typeof(ProviderValidateRequest))]
[JsonSerializable(typeof(ProviderValidateResponse))]
[JsonSerializable(typeof(ProviderPlanChangesRequest))]
[JsonSerializable(typeof(ProviderPlanChangesResponse))]
[JsonSerializable(typeof(ProviderApplyChangesRequest))]
[JsonSerializable(typeof(ProviderApplyChangesResponse))]
[JsonSerializable(typeof(ProviderHealthRequest))]
[JsonSerializable(typeof(ProviderHealthResponse))]
[JsonSerializable(typeof(ResourceBaseline))]
[JsonSerializable(typeof(ResourcePublicationReceipt))]
public partial class ProviderProtocolJsonContext : JsonSerializerContext
{
}
