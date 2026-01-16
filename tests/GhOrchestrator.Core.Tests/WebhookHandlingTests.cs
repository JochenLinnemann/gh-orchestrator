namespace GhOrchestrator.Core.Tests;

public class WebhookHandlingTests
{
    [Fact]
    public void GitHubWebhookSignatureVerifier_WithValidSignature_ReturnsTrue()
    {
        var payload = "{\"test\":true}";
        var secret = "super-secret";
        var signature = GitHubWebhookSignatureVerifier.ComputeSignature(payload, secret);

        var isValid = GitHubWebhookSignatureVerifier.IsValid(payload, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void GitHubWebhookSignatureVerifier_WithInvalidSignature_ReturnsFalse()
    {
        var payload = "{\"test\":true}";
        var secret = "super-secret";
        var signature = "sha256=deadbeef";

        var isValid = GitHubWebhookSignatureVerifier.IsValid(payload, signature, secret);

        Assert.False(isValid);
    }

    [Fact]
    public void IssueCommentWebhookHandler_WithValidPayload_ReturnsEvent()
    {
        var payload = """
        {
          "repository": { "full_name": "octo-org/octo-repo" },
          "issue": { "number": 42 },
          "comment": {
            "body": "/ai start\nShip it",
            "user": { "login": "octocat" }
          }
        }
        """;
        var secret = "webhook-secret";
        var signature = GitHubWebhookSignatureVerifier.ComputeSignature(payload, secret);
        var handler = new IssueCommentWebhookHandler();

        var result = handler.Handle(payload, signature, secret);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Event);
        Assert.Equal("octo-org/octo-repo", result.Event!.Repository);
        Assert.Equal(42, result.Event.IssueNumber);
        Assert.Equal("/ai start\nShip it", result.Event.CommentBody);
        Assert.Equal("octocat", result.Event.CommentAuthor);
    }

    [Fact]
    public void IssueCommentWebhookHandler_WithInvalidSignature_ReturnsFailure()
    {
        var payload = """
        {
          "repository": { "full_name": "octo-org/octo-repo" },
          "issue": { "number": 1 },
          "comment": { "body": "hello" }
        }
        """;
        var handler = new IssueCommentWebhookHandler();

        var result = handler.Handle(payload, "sha256=badsignature", "webhook-secret");

        Assert.False(result.IsValid);
        Assert.Equal("Webhook signature validation failed", result.ErrorMessage);
    }

    [Fact]
    public void GitHubAppConfiguration_WithEnvironmentVariables_LoadsSuccessfully()
    {
        using var environment = new EnvironmentScope(
            ("GH_APP_ID", "12345"),
            ("GH_APP_PRIVATE_KEY", "---BEGIN PRIVATE KEY---"),
            ("GH_WEBHOOK_SECRET", "secret"));

        var validation = GitHubAppConfiguration.TryLoadFromEnvironment(out var config);

        Assert.True(validation.IsValid);
        Assert.NotNull(config);
        Assert.Equal(12345, config!.AppId);
        Assert.Equal("---BEGIN PRIVATE KEY---", config.PrivateKeyPem);
        Assert.Equal("secret", config.WebhookSecret);
    }

    [Fact]
    public void GitHubAppConfiguration_MissingAppId_ReturnsFailure()
    {
        using var environment = new EnvironmentScope(
            ("GH_APP_ID", null),
            ("GH_APP_PRIVATE_KEY", "key"),
            ("GH_WEBHOOK_SECRET", "secret"));

        var validation = GitHubAppConfiguration.TryLoadFromEnvironment(out var config);

        Assert.False(validation.IsValid);
        Assert.Null(config);
        Assert.Equal("GH_APP_ID environment variable is required", validation.ErrorMessage);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.OrdinalIgnoreCase);

        public EnvironmentScope(params (string Name, string? Value)[] variables)
        {
            foreach (var (name, value) in variables)
            {
                _originalValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
