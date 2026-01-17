using System.IdentityModel.Tokens.Jwt;
using GhOrchestrator.Host;

namespace GhOrchestrator.Host.Tests;

public class GitHubAppJwtProviderTests
{
    [Fact]
    public void CreateJwt_IncludesIssuerAndExpiry()
    {
        var now = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);
        var provider = new GitHubAppJwtProvider(12345, TestKeys.PrivateKeyPem, () => now);

        var token = provider.CreateJwt();
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        Assert.Equal("12345", parsed.Issuer);
        Assert.Equal(now.UtcDateTime, parsed.IssuedAt);
        Assert.Equal(now.AddMinutes(10).UtcDateTime, parsed.ValidTo);
    }
}
