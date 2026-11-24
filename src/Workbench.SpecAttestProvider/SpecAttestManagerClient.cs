using System.Net;
using System.Net.Http.Headers;
using Workbench.Core;

#pragma warning disable MA0048 // Interface and HTTP implementation form one endpoint boundary.

namespace Workbench.SpecAttestProvider;

public interface ISpecAttestManagerClient
{
    Task<SpecAttestManagerExport> GetCanonicalExportAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    Task<SpecAttestManagerStatus> GetStatusAsync(
        bool deep,
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
                using Stream stream = await versionResponse.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (document.RootElement.TryGetProperty("version", out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    version = value.GetString() ?? version;
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
}

#pragma warning restore MA0048
