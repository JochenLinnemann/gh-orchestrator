namespace GhOrchestrator.Core;

public record PullRequestRequest(
    string Title,
    string Body,
    string HeadBranch,
    string BaseBranch);
