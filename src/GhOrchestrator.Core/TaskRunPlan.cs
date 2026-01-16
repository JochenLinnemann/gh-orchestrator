namespace GhOrchestrator.Core;

/// <summary>
/// Represents a planned execution run for a task.
/// </summary>
public record TaskRunPlan(
    string RunId,
    IReadOnlyList<string> Repos,
    IReadOnlyList<TaskRunStep> Steps
);
