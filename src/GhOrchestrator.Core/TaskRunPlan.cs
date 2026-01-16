namespace GhOrchestrator.Core;

/// <summary>
/// Represents a planned execution run for a task.
/// </summary>
public record TaskRunPlan(
    string RunId,
    IReadOnlyList<string> Repos,
    IReadOnlyList<TaskRunStep> Steps
);

/// <summary>
/// Represents an execution step within a task run plan.
/// </summary>
public record TaskRunStep(TaskRunStepType StepType, string Repository);

/// <summary>
/// Represents supported step types for a task run plan.
/// </summary>
public enum TaskRunStepType
{
    CreateBranch,
    ExecuteTask,
    OpenPullRequest
}
