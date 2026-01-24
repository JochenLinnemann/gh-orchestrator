namespace GhOrchestrator.Core;

public record WebhookHandlingResult(bool IsValid, IssueCommentEvent? Event, string? ErrorMessage)
{
    public static WebhookHandlingResult Success(IssueCommentEvent issueCommentEvent)
        => new(true, issueCommentEvent, null);

    public static WebhookHandlingResult Failure(string errorMessage)
        => new(false, null, errorMessage);
}
