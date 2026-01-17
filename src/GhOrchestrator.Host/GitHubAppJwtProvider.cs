using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace GhOrchestrator.Host;

public sealed class GitHubAppJwtProvider
{
    private readonly long _appId;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly RsaSecurityKey _signingKey;

    public GitHubAppJwtProvider(long appId, string privateKeyPem, Func<DateTimeOffset>? nowProvider = null)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentException("Private key is required", nameof(privateKeyPem));

        _appId = appId;
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        _signingKey = CreateSigningKey(privateKeyPem);
    }

    public string CreateJwt()
    {
        var now = _nowProvider();
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _appId.ToString(CultureInfo.InvariantCulture),
            NotBefore = now.AddSeconds(-60).UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        };

        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static RsaSecurityKey CreateSigningKey(string privateKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return new RsaSecurityKey(rsa);
    }
}
