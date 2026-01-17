namespace GhOrchestrator.Core;

public record TaskClaimResult(
    bool IsValid,
    bool IsAlreadyClaimed,
    IReadOnlyCollection<ProjectFieldUpdate> Updates,
    string? ErrorMessage)
{
    public static TaskClaimResult Success(IReadOnlyCollection<ProjectFieldUpdate> updates) =>
        new(true, false, updates, null);

    public static TaskClaimResult AlreadyClaimed() =>
        new(true, true, Array.Empty<ProjectFieldUpdate>(), null);

    public static TaskClaimResult Failure(string errorMessage) =>
        new(false, false, Array.Empty<ProjectFieldUpdate>(), errorMessage);
}
