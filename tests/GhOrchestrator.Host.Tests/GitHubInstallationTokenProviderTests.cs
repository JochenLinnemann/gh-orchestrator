using System.Net;
using GhOrchestrator.Host;

namespace GhOrchestrator.Host.Tests;

public class GitHubInstallationTokenProviderTests
{
    [Fact]
    public async Task GetInstallationToken_CachesUntilExpiry()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/repos/octo/demo/installation")
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{\"id\":123}");

            if (path == "/app/installations/123/access_tokens")
                return FakeHttpMessageHandler.Json(HttpStatusCode.Created, "{\"token\":\"token-1\",\"expires_at\":\"2026-01-01T00:00:00Z\"}");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var jwtProvider = new GitHubAppJwtProvider(1, TestKeys.PrivateKeyPem, () => new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var cache = new GitHubInstallationTokenCache();
        var provider = new GitHubInstallationTokenProvider(httpClient, jwtProvider, cache, () => new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

        var first = await provider.GetInstallationToken("octo/demo");
        var second = await provider.GetInstallationToken("octo/demo");

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal(2, handler.Requests.Count(request => request.RequestUri?.AbsolutePath == "/repos/octo/demo/installation"));
        Assert.Equal(1, handler.Requests.Count(request => request.RequestUri?.AbsolutePath == "/app/installations/123/access_tokens"));
    }
}
