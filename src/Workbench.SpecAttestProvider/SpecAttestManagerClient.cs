using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Workbench.Core;

#pragma warning disable MA0048 // Interface and HTTP implementation form one endpoint boundary.

namespace Workbench.SpecAttestProvider;

public interface ISpecAttestManagerClient
{
    Task VerifyStandardPinAsync(CancellationToken cancellationToken = default);

    Task<SpecAttestSafeMutationCapabilities> GetSafeMutationCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    Task<SpecAttestManagerExport> GetCanonicalExportAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<SpecAttestManagerStatus> GetStatusAsync(
        bool deep,
        CancellationToken cancellationToken = default);

    Task<SpecAttestSafeMutationOperation> StageSafeMutationAsync(
        string projectId,
        JsonElement stageRequest,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<JsonElement> GetIntakePackageAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<SpecAttestSafeMutationOperation> GetSafeMutationAsync(
        string projectId,
        string importRunId,
        CancellationToken cancellationToken = default);

    Task<SpecAttestSafeMutationOperation> ExecuteSafeMutationAsync(
        string projectId,
        string importRunId,
        string operation,
        long expectedProjectVersion,
        long expectedOperationVersion,
        CancellationToken cancellationToken = default);
}

public sealed class SpecAttestManagerClient : ISpecAttestManagerClient
{
    private readonly HttpClient httpClient;
    private readonly SpecAttestProviderOptions options;

    public SpecAttestManagerClient(HttpClient httpClient, SpecAttestProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<SpecAttestManagerExport> GetCanonicalExportAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await this.VerifyStandardPinAsync(cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage request = this.CreateRequest(
            HttpMethod.Get,
            $"api/v1/projects/{Uri.EscapeDataString(projectId)}/canonical-export");
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        string packageVersion = RequiredText(root, "packageVersion");
        JsonElement project = RequiredObject(root, "project");
        string returnedProjectId = RequiredText(project, "projectId");
        long recordVersion = RequiredInt64(project, "recordVersion");
        DateTimeOffset updatedAt = RequiredDateTimeOffset(project, "updatedAt");
        return new SpecAttestManagerExport(
            packageVersion,
            returnedProjectId,
            recordVersion,
            updatedAt,
            root.Clone());
    }

    public async Task VerifyStandardPinAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = this.CreateRequest(HttpMethod.Get, "version");
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("standardPin", out JsonElement pin)
            || pin.ValueKind != JsonValueKind.Object
            || !SpecAttestInterchangeContract.MatchesManagerPin(pin, this.options.StandardPin))
        {
            throw Failure(
                "specattest.manager.standard-pin.conflict",
                "SpecAttest Manager does not advertise the exact configured immutable Standard pin.");
        }
    }

    public async Task<SpecAttestSafeMutationCapabilities> GetSafeMutationCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = this.CreateRequest(HttpMethod.Get, "api/v1/interoperability/specattest");
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("standardPin", out JsonElement pin)
            || !SpecAttestInterchangeContract.MatchesManagerPin(pin, this.options.StandardPin))
        {
            throw Failure(
                "specattest.manager.standard-pin.conflict",
                "SpecAttest safe-mutation capabilities do not match the configured immutable Standard pin.");
        }

        string contract = RequiredText(root, "contract");
        string[] operations = RequiredStringArray(root, "operations");
        SpecAttestSafeMutationCapabilities capabilities = new(
            contract,
            operations,
            RequiredBoolean(root, "idempotencyKeysRequired"),
            RequiredBoolean(root, "expectedVersionsRequired"),
            RequiredBoolean(root, "atomicCommit"),
            RequiredBoolean(root, "automaticMerge"),
            RequiredBoolean(root, "immutableAuditAndVersions"),
            RequiredBoolean(root, "deterministicRetry"));
        if (!string.Equals(contract, SpecAttestInterchangeContract.SafeMutationContract, StringComparison.Ordinal)
            || !operations.SequenceEqual(["stage", "inspect", "commit", "cancel", "resume"], StringComparer.Ordinal)
            || !capabilities.IdempotencyKeysRequired
            || !capabilities.ExpectedVersionsRequired
            || !capabilities.AtomicCommit
            || capabilities.AutomaticMerge
            || !capabilities.ImmutableAuditAndVersions
            || !capabilities.DeterministicRetry)
        {
            throw Failure(
                "specattest.manager.safe-mutation.incompatible",
                "SpecAttest Manager does not advertise the required safe-mutation guarantees.");
        }

        return capabilities;
    }

    public async Task<SpecAttestSafeMutationOperation> StageSafeMutationAsync(
        string projectId,
        JsonElement stageRequest,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));
        }

        await this.GetSafeMutationCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage request = this.CreateRequest(
            HttpMethod.Post,
            $"api/v1/projects/{Uri.EscapeDataString(projectId)}/interoperability/specattest/mutations/stage");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(stageRequest);
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await ParseOperationAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> GetIntakePackageAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await this.VerifyStandardPinAsync(cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage request = this.CreateRequest(
            HttpMethod.Get,
            $"api/v1/projects/{Uri.EscapeDataString(projectId)}/intake-packages/export");
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    public async Task<SpecAttestSafeMutationOperation> GetSafeMutationAsync(
        string projectId,
        string importRunId,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = this.CreateRequest(
            HttpMethod.Get,
            $"api/v1/projects/{Uri.EscapeDataString(projectId)}/interoperability/specattest/mutations/{Uri.EscapeDataString(importRunId)}");
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await ParseOperationAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpecAttestSafeMutationOperation> ExecuteSafeMutationAsync(
        string projectId,
        string importRunId,
        string operation,
        long expectedProjectVersion,
        long expectedOperationVersion,
        CancellationToken cancellationToken = default)
    {
        if (operation is not ("commit" or "cancel" or "resume"))
        {
            throw new ArgumentException("Operation must be commit, cancel, or resume.", nameof(operation));
        }

        JsonObject command = new()
        {
            ["contract"] = SpecAttestInterchangeContract.SafeMutationContract,
            ["standardPin"] = SpecAttestInterchangeContract.ManagerPin(this.options.StandardPin),
            ["expectedProjectVersion"] = expectedProjectVersion,
            ["expectedOperationVersion"] = expectedOperationVersion
        };
        using HttpRequestMessage request = this.CreateRequest(
            HttpMethod.Post,
            $"api/v1/projects/{Uri.EscapeDataString(projectId)}/interoperability/specattest/mutations/{Uri.EscapeDataString(importRunId)}/{operation}");
        request.Content = JsonContent.Create(command);
        using HttpResponseMessage response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await ParseOperationAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpecAttestManagerStatus> GetStatusAsync(
        bool deep,
        CancellationToken cancellationToken = default)
    {
        List<ProviderDiagnostic> diagnostics = [];
        using HttpRequestMessage healthRequest = this.CreateRequest(
            HttpMethod.Get,
            deep ? "health/ready" : "health/live");
        HttpResponseMessage healthResponse;
        try
        {
            healthResponse = await this.SendAsync(healthRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (ResourceProviderProtocolException exception)
        {
            diagnostics.Add(new ProviderDiagnostic(
                exception.Error.Code,
                ProviderDiagnosticSeverity.Error,
                exception.Error.Message,
                this.options.Endpoint.ToString(),
                exception.Error.Retryable));
            return new SpecAttestManagerStatus(ProviderHealthState.Unavailable, "unknown", diagnostics);
        }

        using (healthResponse)
        {
            if (!healthResponse.IsSuccessStatusCode)
            {
                diagnostics.Add(new ProviderDiagnostic(
                    "specattest.endpoint.unavailable",
                    ProviderDiagnosticSeverity.Error,
                    $"SpecAttest health endpoint returned {(int)healthResponse.StatusCode}.",
                    this.options.Endpoint.ToString(),
                    Retryable(healthResponse.StatusCode)));
                return new SpecAttestManagerStatus(ProviderHealthState.Unavailable, "unknown", diagnostics);
            }
        }

        string version = "unknown";
        if (deep)
        {
            using HttpRequestMessage versionRequest = this.CreateRequest(HttpMethod.Get, "version");
            using HttpResponseMessage versionResponse = await this.SendAsync(versionRequest, cancellationToken).ConfigureAwait(false);
            if (versionResponse.IsSuccessStatusCode)
            {
                using JsonDocument document = await ReadJsonAsync(versionResponse, cancellationToken).ConfigureAwait(false);
                if (document.RootElement.TryGetProperty("version", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    version = value.GetString() ?? version;
                }

                if (!document.RootElement.TryGetProperty("standardPin", out JsonElement pin)
                    || !SpecAttestInterchangeContract.MatchesManagerPin(pin, this.options.StandardPin))
                {
                    diagnostics.Add(new ProviderDiagnostic(
                        "specattest.manager.standard-pin.conflict",
                        ProviderDiagnosticSeverity.Error,
                        "SpecAttest Manager does not advertise the configured immutable Standard pin.",
                        this.options.Endpoint.ToString()));
                }
            }
            else
            {
                diagnostics.Add(new ProviderDiagnostic(
                    "specattest.endpoint.version-unavailable",
                    ProviderDiagnosticSeverity.Warning,
                    $"SpecAttest version endpoint returned {(int)versionResponse.StatusCode}.",
                    this.options.Endpoint.ToString(),
                    Retryable(versionResponse.StatusCode)));
            }
        }

        return new SpecAttestManagerStatus(
            diagnostics.Count == 0 ? ProviderHealthState.Healthy : ProviderHealthState.Degraded,
            version,
            diagnostics);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        Uri requestUri = new($"{this.options.Endpoint.AbsoluteUri.TrimEnd('/')}/{relativePath}", UriKind.Absolute);
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(this.options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.options.AccessToken);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await this.httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ResourceProviderProtocolException(
                new ProviderError(
                    "specattest.endpoint.unavailable",
                    $"SpecAttest endpoint is unavailable ({exception.HttpRequestError}).",
                    Retryable: true));
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new ResourceProviderProtocolException(
            new ProviderError(
                response.StatusCode == HttpStatusCode.NotFound
                    ? "specattest.resource.not-found"
                    : "specattest.endpoint.request-failed",
                $"SpecAttest returned {(int)response.StatusCode}.",
                Retryable(response.StatusCode)));
    }

    private static bool Retryable(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || (int)statusCode >= 500;
    }

    private static JsonElement RequiredObject(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Object
            ? property
            : throw new JsonException($"SpecAttest response is missing object '{propertyName}'.");
    }

    private static string RequiredText(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new JsonException($"SpecAttest response is missing string '{propertyName}'.");
    }

    private static long RequiredInt64(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.TryGetInt64(out long result)
            ? result
            : throw new JsonException($"SpecAttest response is missing integer '{propertyName}'.");
    }

    private static DateTimeOffset RequiredDateTimeOffset(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.TryGetDateTimeOffset(out DateTimeOffset result)
            ? result
            : throw new JsonException($"SpecAttest response is missing date-time '{propertyName}'.");
    }

    private static bool RequiredBoolean(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : throw new JsonException($"SpecAttest response is missing boolean '{propertyName}'.");
    }

    private static string[] RequiredStringArray(JsonElement value, string propertyName)
    {
        return value.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.Array
            && property.EnumerateArray().All(static item => item.ValueKind == JsonValueKind.String)
            ? property.EnumerateArray().Select(static item => item.GetString()!).ToArray()
            : throw new JsonException($"SpecAttest response is missing string array '{propertyName}'.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SpecAttestSafeMutationOperation> ParseOperationAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        return new SpecAttestSafeMutationOperation(
            RequiredText(root, "importRunId"),
            RequiredText(root, "projectId"),
            RequiredText(root, "status"),
            RequiredInt64(root, "recordVersion"),
            RequiredInt64(root, "expectedProjectVersion"),
            RequiredText(root, "requestDigest"),
            root.Clone());
    }

    private static ResourceProviderProtocolException Failure(string code, string message)
        => new(new ProviderError(code, message), [new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message)]);
}

#pragma warning restore MA0048
