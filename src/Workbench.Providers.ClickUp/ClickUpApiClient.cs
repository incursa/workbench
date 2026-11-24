using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Workbench.Core;

namespace Workbench.Providers.ClickUp;

internal sealed class ClickUpApiClient
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly ClickUpProviderOptions options;
    private readonly string token;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public ClickUpApiClient(
        HttpClient httpClient,
        ClickUpProviderOptions options,
        string token,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.token = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("ClickUp token is required.", nameof(token)) : token;
        this.delay = delay ?? Task.Delay;
    }

    public async Task<ClickUpTaskPage> GetTasksAsync(
        int page,
        bool archived,
        CancellationToken cancellationToken)
    {
        var path = $"list/{Uri.EscapeDataString(this.options.ListId)}/task?page={page.ToString(CultureInfo.InvariantCulture)}"
            + $"&archived={archived.ToString().ToLowerInvariant()}&include_closed=true&subtasks=true&order_by=id";
        using var response = await this.SendAsync(HttpMethod.Get, path, content: null, retrySafe: true, cancellationToken)
            .ConfigureAwait(false);
        return await ReadRequiredAsync<ClickUpTaskPage>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClickUpTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var path = $"task/{Uri.EscapeDataString(taskId)}";
        using var response = await this.SendAsync(
            HttpMethod.Get,
            path,
            content: null,
            retrySafe: true,
            cancellationToken,
            allowNotFound: true).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await ReadRequiredAsync<ClickUpTask>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task CheckListAsync(CancellationToken cancellationToken)
    {
        var path = $"list/{Uri.EscapeDataString(this.options.ListId)}";
        using var response = await this.SendAsync(HttpMethod.Get, path, content: null, retrySafe: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClickUpCustomFieldDefinition>> GetCustomFieldsAsync(
        CancellationToken cancellationToken)
    {
        var path = $"list/{Uri.EscapeDataString(this.options.ListId)}/field?include_applied_objects=true";
        using var response = await this.SendAsync(HttpMethod.Get, path, content: null, retrySafe: true, cancellationToken)
            .ConfigureAwait(false);
        var page = await ReadRequiredAsync<ClickUpCustomFieldPage>(response, cancellationToken).ConfigureAwait(false);
        return page.Fields;
    }

    public async Task<ClickUpTask> CreateTaskAsync(JsonElement body, CancellationToken cancellationToken)
    {
        var path = $"list/{Uri.EscapeDataString(this.options.ListId)}/task";
        using var content = JsonContent.Create(body, options: jsonOptions);
        using var response = await this.SendAsync(
            HttpMethod.Post,
            path,
            content,
            retrySafe: false,
            cancellationToken,
            ambiguousWrite: true)
            .ConfigureAwait(false);
        return await ReadRequiredAsync<ClickUpTask>(response, cancellationToken, ambiguousWrite: true)
            .ConfigureAwait(false);
    }

    public async Task<ClickUpTask> UpdateTaskAsync(
        string taskId,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        var path = $"task/{Uri.EscapeDataString(taskId)}";
        using var content = JsonContent.Create(body, options: jsonOptions);
        using var response = await this.SendAsync(
            HttpMethod.Put,
            path,
            content,
            retrySafe: false,
            cancellationToken,
            ambiguousWrite: true)
            .ConfigureAwait(false);
        return await ReadRequiredAsync<ClickUpTask>(response, cancellationToken, ambiguousWrite: true)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        bool retrySafe,
        CancellationToken cancellationToken,
        bool allowNotFound = false,
        bool ambiguousWrite = false)
    {
        var contentBytes = content is null ? null : await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = content?.Headers.ContentType?.ToString();
        for (var attempt = 1; attempt <= this.options.MaximumHttpAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, new Uri(this.options.ApiBaseUri, relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue(this.token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (contentBytes is not null)
            {
                request.Content = new ByteArrayContent(contentBytes);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException) when (retrySafe && attempt < this.options.MaximumHttpAttempts)
            {
                await this.delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (HttpRequestException exception)
            {
                var code = ambiguousWrite ? "clickup.write.outcome-unknown" : "clickup.http.unavailable";
                var message = ambiguousWrite
                    ? "ClickUp write outcome is unknown; retry the same approved apply and idempotency key."
                    : $"ClickUp request could not be completed ({exception.GetType().Name}).";
                throw Failure(
                    code,
                    message,
                    retryable: true);
            }

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return response;
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;
            if (retrySafe
                && response.StatusCode != HttpStatusCode.TooManyRequests
                && retryable
                && attempt < this.options.MaximumHttpAttempts)
            {
                response.Dispose();
                await this.delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var failure = await CreateHttpFailureAsync(response, retryable, ambiguousWrite, cancellationToken)
                .ConfigureAwait(false);
            response.Dispose();
            throw failure;
        }

        throw Failure("clickup.http.unavailable", "ClickUp request attempts were exhausted.", retryable: true);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        bool ambiguousWrite = false)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(jsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw ResponseFailure(ambiguousWrite, "ClickUp returned an empty response.");
        }
        catch (JsonException)
        {
            throw ResponseFailure(ambiguousWrite, "ClickUp returned an invalid JSON response.");
        }
    }

    private static ResourceProviderProtocolException ResponseFailure(bool ambiguousWrite, string message)
    {
        return ambiguousWrite
            ? Failure(
                "clickup.write.outcome-unknown",
                message + " The write may have succeeded; retry the same approved apply and idempotency key.",
                retryable: true)
            : Failure("clickup.response.invalid", message, retryable: false);
    }

    private static async Task<ResourceProviderProtocolException> CreateHttpFailureAsync(
        HttpResponseMessage response,
        bool retryable,
        bool ambiguousWrite,
        CancellationToken cancellationToken)
    {
        string? clickUpCode = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ClickUpApiError>(jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            clickUpCode = error?.Ecode;
        }
        catch (JsonException)
        {
            // Do not echo provider response bodies; they may contain user data.
        }

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["httpStatus"] = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)
        };
        CopyHeader(response, "X-RateLimit-Limit", data, "rateLimit");
        CopyHeader(response, "X-RateLimit-Remaining", data, "rateLimitRemaining");
        CopyHeader(response, "X-RateLimit-Reset", data, "rateLimitResetUnix");
        CopyHeader(response, "Retry-After", data, "retryAfter");
        if (!string.IsNullOrWhiteSpace(clickUpCode))
        {
            data["clickUpCode"] = clickUpCode;
        }

        string code;
        string message;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            code = "clickup.rate-limit.exceeded";
            message = "ClickUp rate limit was exceeded.";
        }
        else if (ambiguousWrite && (int)response.StatusCode >= 500)
        {
            code = "clickup.write.outcome-unknown";
            message = "ClickUp write outcome is unknown; retry the same approved apply and idempotency key.";
        }
        else
        {
            code = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "clickup.authentication.failed",
                HttpStatusCode.NotFound => "clickup.resource.not-found",
                _ when (int)response.StatusCode >= 500 => "clickup.http.transient",
                _ => "clickup.http.rejected"
            };
            message = $"ClickUp rejected the request with HTTP {(int)response.StatusCode}.";
        }
        var diagnostic = new ProviderDiagnostic(
            code,
            ProviderDiagnosticSeverity.Error,
            message,
            Retryable: retryable,
            Data: data);
        return new ResourceProviderProtocolException(new ProviderError(code, message, retryable), [diagnostic]);
    }

    private static ResourceProviderProtocolException Failure(string code, string message, bool retryable)
    {
        return new ResourceProviderProtocolException(
            new ProviderError(code, message, retryable),
            [new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, Retryable: retryable)]);
    }

    private static void CopyHeader(
        HttpResponseMessage response,
        string header,
        IDictionary<string, string> data,
        string key)
    {
        if (response.Headers.TryGetValues(header, out var values))
        {
            data[key] = string.Join(",", values);
        }
    }

    private static TimeSpan Backoff(int attempt)
    {
        return TimeSpan.FromMilliseconds(Math.Min(2000, 100 * (1 << (attempt - 1))));
    }

}
