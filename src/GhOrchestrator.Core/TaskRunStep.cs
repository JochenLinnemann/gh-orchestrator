namespace GhOrchestrator.Core;

/// <summary>
/// Represents an execution step within a task run plan.
/// </summary>
public record TaskRunStep(TaskRunStepType StepType, string Repository);
