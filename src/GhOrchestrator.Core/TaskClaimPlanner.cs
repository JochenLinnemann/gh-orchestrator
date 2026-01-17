namespace GhOrchestrator.Core;

/// <summary>
/// Builds project field updates to claim a task in the Kanban board.
/// Pure function: no external I/O.
/// </summary>
public static class TaskClaimPlanner
{
    private const string AiRunning = "running";
    private const string StatusInProgress = "In Progress";

    public static TaskClaimResult Plan(ProjectTaskState current, string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return TaskClaimResult.Failure("Run ID is required to claim a task.");

        if (!string.IsNullOrWhiteSpace(current.RunId) &&
            !string.Equals(current.RunId, runId, StringComparison.OrdinalIgnoreCase))
        {
            return TaskClaimResult.Failure($"Task already claimed with Run ID '{current.RunId}'.");
        }

        if (string.Equals(current.AiStatus, AiRunning, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.Status, StatusInProgress, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.RunId, runId, StringComparison.OrdinalIgnoreCase))
        {
            return TaskClaimResult.AlreadyClaimed();
        }

        var updates = new List<ProjectFieldUpdate>(capacity: 3);

        if (!string.Equals(current.AiStatus, AiRunning, StringComparison.OrdinalIgnoreCase))
            updates.Add(new ProjectFieldUpdate(ProjectFieldNames.Ai, AiRunning));

        if (!string.Equals(current.Status, StatusInProgress, StringComparison.OrdinalIgnoreCase))
            updates.Add(new ProjectFieldUpdate(ProjectFieldNames.Status, StatusInProgress));

        if (!string.Equals(current.RunId, runId, StringComparison.OrdinalIgnoreCase))
            updates.Add(new ProjectFieldUpdate(ProjectFieldNames.RunId, runId));

        return TaskClaimResult.Success(updates);
    }
}
