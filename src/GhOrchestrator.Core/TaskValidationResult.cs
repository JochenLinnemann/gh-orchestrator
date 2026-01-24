namespace GhOrchestrator.Core;

/// <summary>
/// Combined validation result for task quality gate and preflight checks.
/// </summary>
public record TaskValidationResult(
    ValidationResult TaskQualityGateResult,
    PreflightValidationResult? PreflightResult,
    TaskSpec? Task = null
)
{
    public bool IsValid => TaskQualityGateResult.IsValid && (PreflightResult?.IsValid ?? true);

    public bool NeedsHumanConfirmation => PreflightResult?.NeedsHumanConfirmation ?? false;

    public string? ErrorMessage =>
        !TaskQualityGateResult.IsValid
            ? TaskQualityGateResult.ErrorMessage
            : PreflightResult?.ErrorMessage;

    public static TaskValidationResult FromTaskQualityGateFailure(ValidationResult result) =>
        new(result, null, null);

    public static TaskValidationResult FromPreflight(ValidationResult taskQualityGateResult, PreflightValidationResult preflightResult, TaskSpec? task = null) =>
        new(taskQualityGateResult, preflightResult, task);
}
