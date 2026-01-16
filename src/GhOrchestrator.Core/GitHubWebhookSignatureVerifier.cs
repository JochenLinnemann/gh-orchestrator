using System.Security.Cryptography;
using System.Text;

namespace GhOrchestrator.Core;

public static class GitHubWebhookSignatureVerifier
{
    public static bool IsValid(string payload, string? signatureHeader, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        if (string.IsNullOrWhiteSpace(webhookSecret))
            return false;

        var expectedSignature = ComputeSignature(payload, webhookSecret);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var providedBytes = Encoding.UTF8.GetBytes(signatureHeader.Trim());

        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public static string ComputeSignature(string payload, string webhookSecret)
    {
        var key = Encoding.UTF8.GetBytes(webhookSecret);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
