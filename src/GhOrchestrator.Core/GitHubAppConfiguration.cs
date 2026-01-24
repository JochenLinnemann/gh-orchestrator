namespace GhOrchestrator.Core;

public record GitHubAppConfiguration(long AppId, string PrivateKeyPem, string WebhookSecret)
{
    public static ValidationResult TryLoadFromEnvironment(out GitHubAppConfiguration? configuration)
    {
        configuration = null;

        var appIdRaw = Environment.GetEnvironmentVariable("GH_APP_ID");
        if (string.IsNullOrWhiteSpace(appIdRaw))
            return ValidationResult.Failure("GH_APP_ID environment variable is required");

        if (!long.TryParse(appIdRaw, out var appId))
            return ValidationResult.Failure("GH_APP_ID must be a valid integer");

        var privateKeyPem = Environment.GetEnvironmentVariable("GH_APP_PRIVATE_KEY");
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            return ValidationResult.Failure("GH_APP_PRIVATE_KEY environment variable is required");

        var webhookSecret = Environment.GetEnvironmentVariable("GH_WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(webhookSecret))
            return ValidationResult.Failure("GH_WEBHOOK_SECRET environment variable is required");

        configuration = new GitHubAppConfiguration(appId, privateKeyPem, webhookSecret);
        return ValidationResult.Success();
    }
}
