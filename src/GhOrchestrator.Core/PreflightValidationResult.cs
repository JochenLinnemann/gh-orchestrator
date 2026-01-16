namespace GhOrchestrator.Core;

/// <summary>
/// Structured result from preflight (outer-layer) validation.
/// Complements TaskQualityGate (pure task spec checks).
/// </summary>
public record PreflightValidationResult(
    bool IsValid,
    bool NeedsHumanConfirmation = false,
    PreflightFailureReason FailureReason = PreflightFailureReason.Unknown,
    string? ErrorMessage = null
)
{
    public static PreflightValidationResult Success() => 
        new(true);

    public static PreflightValidationResult Failure(PreflightFailureReason reason, string message) => 
        new(false, false, reason, message);

    public static PreflightValidationResult EscalationRequired(string message) => 
        new(false, true, PreflightFailureReason.DestructiveIntentDetected, message);
}
