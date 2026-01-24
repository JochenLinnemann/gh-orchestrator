namespace GhOrchestrator.Core;

/// <summary>
/// Result of attempting to plan task completion (transition to blocked state).
/// </summary>
public sealed record TaskCompletionResult(
    bool IsValid,
    bool IsAlreadyCompleted,
    IReadOnlyList<ProjectFieldUpdate> Updates,
    string? ErrorMessage)
{
    public static TaskCompletionResult Success(IReadOnlyList<ProjectFieldUpdate> updates) =>
        new(IsValid: true, IsAlreadyCompleted: false, updates, ErrorMessage: null);

    public static TaskCompletionResult AlreadyCompleted() =>
        new(IsValid: true, IsAlreadyCompleted: true, Updates: Array.Empty<ProjectFieldUpdate>(), ErrorMessage: null);

    public static TaskCompletionResult Failure(string errorMessage) =>
        new(IsValid: false, IsAlreadyCompleted: false, Updates: Array.Empty<ProjectFieldUpdate>(), errorMessage);
}
