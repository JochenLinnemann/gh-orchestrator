using System.Globalization;

namespace GhOrchestrator.Core;

/// <summary>
/// Produces a deterministic task run plan from a validated task specification.
/// Pure function: no external I/O.
/// </summary>
public static class TaskRunPlanner
{
    /// <summary>
    /// Build a task run plan with a formatted run ID and execution steps.
    /// </summary>
    /// <param name="task">Task specification to plan.</param>
    /// <param name="now">Current timestamp (UTC recommended) for run ID formatting.</param>
    /// <returns>Plan result with structured error information.</returns>
    public static TaskRunPlanResult Plan(TaskSpec task, DateTimeOffset now)
    {
        var validationResult = TaskQualityGate.Validate(task);
        if (!validationResult.IsValid)
            return TaskRunPlanResult.Failure(validationResult.ErrorMessage ?? "Task failed validation.");

        if (task.Repos.Count == 0)
            return TaskRunPlanResult.Failure("Repos must be present and non-empty");

        var runId = FormatRunId(task.IssueNumber, now);
        var repos = task.Repos.ToArray();
        var steps = BuildSteps(repos);

        return TaskRunPlanResult.Success(new TaskRunPlan(runId, repos, steps));
    }

    private static string FormatRunId(int issueNumber, DateTimeOffset now)
    {
        var timestamp = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"run-{issueNumber}-{timestamp}";
    }

    private static IReadOnlyList<TaskRunStep> BuildSteps(IReadOnlyList<string> repos)
    {
        var steps = new List<TaskRunStep>(repos.Count * 3);

        foreach (var repo in repos)
        {
            steps.Add(new TaskRunStep(TaskRunStepType.CreateBranch, repo));
            steps.Add(new TaskRunStep(TaskRunStepType.ExecuteTask, repo));
            steps.Add(new TaskRunStep(TaskRunStepType.OpenPullRequest, repo));
        }

        return steps;
    }
}
