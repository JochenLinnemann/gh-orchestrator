namespace GhOrchestrator.Core;

/// <summary>
/// Planning result with structured error information.
/// </summary>
public record TaskRunPlanResult(bool IsValid, TaskRunPlan? Plan, string? ErrorMessage)
{
    public static TaskRunPlanResult Success(TaskRunPlan plan) => new(true, plan, null);
    public static TaskRunPlanResult Failure(string errorMessage) => new(false, null, errorMessage);
}
