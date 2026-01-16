namespace GhOrchestrator.Core;

public record IssueCommentEvent(
    string Repository,
    int IssueNumber,
    string CommentBody,
    string? CommentAuthor = null);
