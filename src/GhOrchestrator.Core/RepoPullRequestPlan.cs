namespace GhOrchestrator.Core;

/// <summary>
/// Represents a per-repository pull request plan for a task run.
/// </summary>
public record RepoPullRequestPlan(
    string Repository,
    string BranchName,
    PullRequestRequest PullRequest
);
