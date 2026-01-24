namespace GhOrchestrator.Core;

/// <summary>
/// Represents supported step types for a task run plan.
/// </summary>
public enum TaskRunStepType
{
    CreateBranch,
    ExecuteTask,
    OpenPullRequest
}
