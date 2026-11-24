namespace Workbench.Providers.ClickUp.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    private int requestCount;

    public List<(HttpMethod Method, Uri Uri, string? Body, string? Authorization)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        this.Requests.Add((
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.Authorization?.ToString()));
        var count = Interlocked.Increment(ref this.requestCount);
        return responseFactory(request, count);
    }
}
