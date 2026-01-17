namespace GhOrchestrator.Host;

public record GitHubHostConfiguration(long AppId, string? PrivateKeyPath, string? PrivateKeyPem, string WebhookSecret, string AllowedOrg)
{
    public string ReadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(PrivateKeyPem))
            return PrivateKeyPem;

        if (string.IsNullOrWhiteSpace(PrivateKeyPath))
            throw new InvalidOperationException("GH_APP_PRIVATE_KEY_PATH or GH_APP_PRIVATE_KEY must be set");

        return File.ReadAllText(PrivateKeyPath);
    }

    public static GitHubHostConfiguration Load(IConfiguration configuration)
    {
        var appIdRaw = configuration["GH_APP_ID"] ?? throw new InvalidOperationException("GH_APP_ID not set");
        if (!long.TryParse(appIdRaw, out var appId))
            throw new InvalidOperationException("GH_APP_ID must be a valid integer");

        var privateKeyPath = configuration["GH_APP_PRIVATE_KEY_PATH"];
        var privateKeyPem = configuration["GH_APP_PRIVATE_KEY"];

        if (string.IsNullOrWhiteSpace(privateKeyPath) && string.IsNullOrWhiteSpace(privateKeyPem))
            throw new InvalidOperationException("GH_APP_PRIVATE_KEY_PATH or GH_APP_PRIVATE_KEY must be set");

        if (!string.IsNullOrWhiteSpace(privateKeyPath) && !File.Exists(privateKeyPath))
            throw new InvalidOperationException($"GH_APP_PRIVATE_KEY_PATH does not exist: {privateKeyPath}");

        var webhookSecret = configuration["GH_WEBHOOK_SECRET"] ?? throw new InvalidOperationException("GH_WEBHOOK_SECRET not set");
        var allowedOrg = configuration["GH_ALLOWED_ORG"] ?? throw new InvalidOperationException("GH_ALLOWED_ORG not set");

        return new GitHubHostConfiguration(appId, privateKeyPath, privateKeyPem, webhookSecret, allowedOrg);
    }
}
