namespace GhOrchestrator.Core;

/// <summary>
/// Represents contextual metadata about a GitHub Issue.
/// Used by outer-layer preflight validation.
/// </summary>
public record IssueContext(
    bool IssueExists,
    bool IsOpen,
    string? IssueUrl = null
);
