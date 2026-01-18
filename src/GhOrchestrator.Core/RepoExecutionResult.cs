namespace GhOrchestrator.Core;

public record RepoExecutionResult(
    string Repository,
    string BranchName,
    string BaseBranch,
    PullRequestLink? PullRequest,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage is null;

    public static RepoExecutionResult Success(
        string repository,
        string branchName,
        string baseBranch,
        PullRequestLink pullRequest) =>
        new(repository, branchName, baseBranch, pullRequest, null);

    public static RepoExecutionResult Failure(
        string repository,
        string branchName,
        string baseBranch,
        string errorMessage) =>
        new(repository, branchName, baseBranch, null, errorMessage);
}
