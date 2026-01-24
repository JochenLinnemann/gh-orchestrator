using System.Net.Http.Headers;
using System.Text.Json;

namespace GhOrchestrator.Host;

public sealed class GitHubInstallationTokenProvider : IGitHubInstallationTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly GitHubAppJwtProvider _jwtProvider;
    private readonly GitHubInstallationTokenCache _cache;
    private readonly Func<DateTimeOffset> _nowProvider;

    public GitHubInstallationTokenProvider(
        HttpClient httpClient,
        GitHubAppJwtProvider jwtProvider,
        GitHubInstallationTokenCache cache,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jwtProvider = jwtProvider ?? throw new ArgumentNullException(nameof(jwtProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri("https://api.github.com/");

        EnsureUserAgent();
    }

    public async Task<string> GetInstallationToken(string repository, CancellationToken cancellationToken = default)
    {
        var (owner, name) = ParseRepository(repository);
        var installationId = await GetInstallationId(owner, name, cancellationToken);
        if (_cache.TryGetValidToken(installationId, _nowProvider(), out var token))
            return token;

        var newToken = await RequestInstallationToken(installationId, cancellationToken);
        _cache.StoreToken(installationId, newToken.Token, newToken.ExpiresAt);
        return newToken.Token;
    }

    private async Task<long> GetInstallationId(string owner, string name, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{owner}/{name}/installation");
        AddAppHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("id").GetInt64();
    }

    private async Task<InstallationToken> RequestInstallationToken(long installationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"app/installations/{installationId}/access_tokens");
        AddAppHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = document.RootElement.GetProperty("token").GetString();
        var expiresAtRaw = document.RootElement.GetProperty("expires_at").GetString();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("GitHub installation token response missing token");
        if (string.IsNullOrWhiteSpace(expiresAtRaw))
            throw new InvalidOperationException("GitHub installation token response missing expires_at");

        if (!DateTimeOffset.TryParse(expiresAtRaw, out var expiresAt))
            throw new InvalidOperationException("GitHub installation token response has invalid expires_at");

        return new InstallationToken(token, expiresAt);
    }

    private void AddAppHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtProvider.CreateJwt());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    private void EnsureUserAgent()
    {
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GhOrchestrator", "0.1"));
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"GitHub App token request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
    }

    private static (string Owner, string Name) ParseRepository(string repository)
    {
        if (string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException("Repository is required", nameof(repository));

        var parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new ArgumentException("Repository must be in the format owner/name", nameof(repository));

        return (parts[0], parts[1]);
    }

    private sealed record InstallationToken(string Token, DateTimeOffset ExpiresAt);
}
