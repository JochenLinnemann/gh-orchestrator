namespace GhOrchestrator.Core;

/// <summary>
/// Combined validation result for task quality gate and preflight checks.
/// </summary>
public record TaskValidationResult(
    ValidationResult TaskQualityGateResult,
    PreflightValidationResult? PreflightResult
)
{
    public bool IsValid => TaskQualityGateResult.IsValid && (PreflightResult?.IsValid ?? true);

    public bool NeedsHumanConfirmation => PreflightResult?.NeedsHumanConfirmation ?? false;

    public string? ErrorMessage =>
        !TaskQualityGateResult.IsValid
            ? TaskQualityGateResult.ErrorMessage
            : PreflightResult?.ErrorMessage;

    public static TaskValidationResult FromTaskQualityGateFailure(ValidationResult result) =>
        new(result, null);

    public static TaskValidationResult FromPreflight(ValidationResult taskQualityGateResult, PreflightValidationResult preflightResult) =>
        new(taskQualityGateResult, preflightResult);
}
