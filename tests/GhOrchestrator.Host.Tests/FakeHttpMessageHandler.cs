using System.Net;

namespace GhOrchestrator.Host.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> RequestBodies { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);
        }

        Requests.Add(request);
        RequestBodies.Add(body);
        var response = _handler(request);
        if (response.RequestMessage is null)
            response.RequestMessage = request;

        return Task.FromResult(response);
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
