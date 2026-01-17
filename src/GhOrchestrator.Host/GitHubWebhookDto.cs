using System.Text.Json.Serialization;

namespace GhOrchestrator.Host;

// DTOs to match GitHub's webhook JSON structure
public record GitHubRepository(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName);

public record GitHubUser(
    [property: JsonPropertyName("login")] string Login);

public record GitHubIssue(
    [property: JsonPropertyName("number")] int Number);

public record GitHubComment(
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("user")] GitHubUser User);

public record GitHubIssueCommentWebhookPayload(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("repository")] GitHubRepository Repository,
    [property: JsonPropertyName("issue")] GitHubIssue Issue,
    [property: JsonPropertyName("comment")] GitHubComment Comment);
