namespace GhOrchestrator.Core;

/// <summary>
/// Minimal GitHub boundary for orchestrator operations.
/// Interface-only, no I/O implementation in core.
/// </summary>
public interface IGitHubClient
{
    Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default);

    Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default);

    Task<ProjectTaskStateSnapshot> GetProjectTaskState(
        string repository,
        string projectId,
        int issueNumber,
        CancellationToken cancellationToken = default);

    Task UpdateProjectFields(
        string repository,
        string projectId,
        int issueNumber,
        IReadOnlyCollection<ProjectFieldUpdate> updates,
        CancellationToken cancellationToken = default);

    Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default);

    Task CreateBranch(
        string repository,
        string newBranch,
        string baseBranch,
        CancellationToken cancellationToken = default);

    Task<PullRequestLink> CreatePullRequest(
        string repository,
        PullRequestRequest request,
        CancellationToken cancellationToken = default);
}
