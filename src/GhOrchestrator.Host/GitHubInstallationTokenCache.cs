using System.Collections.Concurrent;

namespace GhOrchestrator.Host;

public sealed class GitHubInstallationTokenCache
{
    private readonly ConcurrentDictionary<long, CachedInstallationToken> _cache = new();
    private readonly TimeSpan _refreshSkew = TimeSpan.FromMinutes(1);
    private readonly Func<DateTimeOffset> _nowProvider;

    public GitHubInstallationTokenCache(Func<DateTimeOffset>? nowProvider = null)
    {
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryGetValidToken(long installationId, DateTimeOffset now, out string token)
    {
        token = string.Empty;

        if (!_cache.TryGetValue(installationId, out var cached))
            return false;

        if (cached.ExpiresAt <= now.Add(_refreshSkew))
        {
            _cache.TryRemove(installationId, out _);
            return false;
        }

        token = cached.Token;
        return true;
    }

    public void StoreToken(long installationId, string token, DateTimeOffset expiresAt)
    {
        _cache[installationId] = new CachedInstallationToken(token, expiresAt);
        CleanupExpired(_nowProvider());
    }

    private sealed record CachedInstallationToken(string Token, DateTimeOffset ExpiresAt);

    private void CleanupExpired(DateTimeOffset now)
    {
        foreach (var entry in _cache)
        {
            if (entry.Value.ExpiresAt <= now)
                _cache.TryRemove(entry.Key, out _);
        }
    }
}
