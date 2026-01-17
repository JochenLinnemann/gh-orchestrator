namespace GhOrchestrator.Host;

public record GitHubHostConfiguration(long AppId, string PrivateKeyPath, string WebhookSecret, string AllowedOrg)
{
    public static GitHubHostConfiguration Load(IConfiguration configuration)
    {
        var appIdRaw = configuration["GH_APP_ID"] ?? throw new InvalidOperationException("GH_APP_ID not set");
        if (!long.TryParse(appIdRaw, out var appId))
            throw new InvalidOperationException("GH_APP_ID must be a valid integer");

        var privateKeyPath = configuration["GH_APP_PRIVATE_KEY_PATH"] ?? throw new InvalidOperationException("GH_APP_PRIVATE_KEY_PATH not set");
        if (!File.Exists(privateKeyPath))
            throw new InvalidOperationException($"GH_APP_PRIVATE_KEY_PATH does not exist: {privateKeyPath}");

        var webhookSecret = configuration["GH_WEBHOOK_SECRET"] ?? throw new InvalidOperationException("GH_WEBHOOK_SECRET not set");
        var allowedOrg = configuration["GH_ALLOWED_ORG"] ?? throw new InvalidOperationException("GH_ALLOWED_ORG not set");

        return new GitHubHostConfiguration(appId, privateKeyPath, webhookSecret, allowedOrg);
    }
}
