namespace GhOrchestrator.Core;

public record GitHubIssue(
    int IssueNumber,
    string Title,
    string Body,
    bool IsOpen,
    string? Url = null);
