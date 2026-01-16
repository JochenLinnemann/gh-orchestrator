namespace GhOrchestrator.Core;

/// <summary>
/// Reason codes for preflight validation failures.
/// Used to distinguish different failure modes.
/// </summary>
public enum PreflightFailureReason
{
    Unknown = 0,
    IssueNotFound = 1,
    IssueClosed = 2,
    DestructiveIntentDetected = 3,
}
