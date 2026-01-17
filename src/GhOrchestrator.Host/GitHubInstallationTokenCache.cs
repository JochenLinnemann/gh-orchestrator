using System.Collections.Concurrent;

namespace GhOrchestrator.Host;

public sealed class GitHubInstallationTokenCache
{
    private readonly ConcurrentDictionary<long, CachedInstallationToken> _cache = new();
    private readonly TimeSpan _refreshSkew = TimeSpan.FromMinutes(1);

    public bool TryGetValidToken(long installationId, DateTimeOffset now, out string token)
    {
        token = string.Empty;

        if (!_cache.TryGetValue(installationId, out var cached))
            return false;

        if (cached.ExpiresAt <= now.Add(_refreshSkew))
            return false;

        token = cached.Token;
        return true;
    }

    public void StoreToken(long installationId, string token, DateTimeOffset expiresAt)
    {
        _cache[installationId] = new CachedInstallationToken(token, expiresAt);
    }

    private sealed record CachedInstallationToken(string Token, DateTimeOffset ExpiresAt);
}
