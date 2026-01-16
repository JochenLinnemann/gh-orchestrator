namespace GhOrchestrator.Core;

public record GitHubIssue(
    int IssueNumber,
    string Body,
    bool IsOpen,
    string? Url = null);
