namespace GhOrchestrator.Core;

/// <summary>
/// Builds project field updates to mark a task as awaiting review after PRs are opened.
/// 
/// According to PLAYBOOK §3.4.7:
/// - Set AI = blocked (until PRs are reviewed)
/// - Keep Status = In Progress (Status → Done only after merge, outside v0 scope)
/// 
/// Pure function: no external I/O.
/// </summary>
public static class TaskCompletionPlanner
{
    private const string AiBlocked = "blocked";

    /// <summary>
    /// Plan field updates to transition task to "blocked" state after successful PR creation.
    /// </summary>
    /// <param name="current">Current project task state.</param>
    /// <returns>Result with field updates to apply.</returns>
    public static TaskCompletionResult Plan(ProjectTaskState current)
    {
        if (string.IsNullOrWhiteSpace(current.RunId))
            return TaskCompletionResult.Failure("Run ID is required to complete a task.");

        // If already blocked, no update needed
        if (string.Equals(current.AiStatus, AiBlocked, StringComparison.OrdinalIgnoreCase))
            return TaskCompletionResult.AlreadyCompleted();

        var updates = new List<ProjectFieldUpdate>(capacity: 1);

        if (!string.Equals(current.AiStatus, AiBlocked, StringComparison.OrdinalIgnoreCase))
            updates.Add(new ProjectFieldUpdate(ProjectFieldNames.Ai, AiBlocked));

        return TaskCompletionResult.Success(updates);
    }
}
