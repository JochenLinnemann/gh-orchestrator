namespace GhOrchestrator.Core;

/// <summary>
/// Represents a bounded task specification extracted from a GitHub Issue.
/// </summary>
public record TaskSpec(
    int IssueNumber,
    string Repository,
    string Description,
    IReadOnlyList<string> Repos,
    string? TriggerUser,
    string? AcceptanceCriteria,
    string? Constraints
);
