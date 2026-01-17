using System.Text.Json;

namespace GhOrchestrator.Core;

public class IssueCommentWebhookHandler
{
    public WebhookHandlingResult Handle(string payload, string? signatureHeader, string webhookSecret)
    {
        if (!GitHubWebhookSignatureVerifier.IsValid(payload, signatureHeader, webhookSecret))
            return WebhookHandlingResult.Failure("Webhook signature validation failed");

        return ParsePayloadInternal(payload);
    }

    public WebhookHandlingResult ParsePayload(string payload)
    {
        return ParsePayloadInternal(payload);
    }

    private static WebhookHandlingResult ParsePayloadInternal(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (!root.TryGetProperty("repository", out var repository))
                return WebhookHandlingResult.Failure("Missing repository data");

            if (!repository.TryGetProperty("full_name", out var repositoryName))
                return WebhookHandlingResult.Failure("Missing repository full_name");

            if (!root.TryGetProperty("issue", out var issue))
                return WebhookHandlingResult.Failure("Missing issue data");

            if (!issue.TryGetProperty("number", out var issueNumberElement) || !issueNumberElement.TryGetInt32(out var issueNumber))
                return WebhookHandlingResult.Failure("Missing issue number");

            if (!root.TryGetProperty("comment", out var comment))
                return WebhookHandlingResult.Failure("Missing comment data");

            if (!comment.TryGetProperty("body", out var commentBody))
                return WebhookHandlingResult.Failure("Missing comment body");

            var author = ExtractAuthor(comment);

            var issueCommentEvent = new IssueCommentEvent(
                repositoryName.GetString() ?? string.Empty,
                issueNumber,
                commentBody.GetString() ?? string.Empty,
                author);

            if (string.IsNullOrWhiteSpace(issueCommentEvent.Repository))
                return WebhookHandlingResult.Failure("Repository name is empty");

            return WebhookHandlingResult.Success(issueCommentEvent);
        }
        catch (JsonException)
        {
            return WebhookHandlingResult.Failure("Invalid JSON payload");
        }
    }

    private static string? ExtractAuthor(JsonElement comment)
    {
        if (!comment.TryGetProperty("user", out var user))
            return null;

        if (!user.TryGetProperty("login", out var login))
            return null;

        return login.GetString();
    }
}
