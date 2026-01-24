namespace GhOrchestrator.Core;

/// <summary>
/// Result of orchestrating a complete task execution flow.
/// </summary>
public record OrchestratorResult
{
    public bool IsSuccess { get; init; }
    public string RunId { get; init; }
    public string? ErrorMessage { get; init; }
    public TaskRunExecutionResult? ExecutionResult { get; init; }
    public bool AlreadyClaimed { get; init; }

    public OrchestratorResult(string runId, bool isSuccess, string? errorMessage = null, TaskRunExecutionResult? executionResult = null, bool alreadyClaimed = false)
    {
        RunId = runId;
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ExecutionResult = executionResult;
        AlreadyClaimed = alreadyClaimed;
    }

    public static OrchestratorResult Failure(string runId, string errorMessage) =>
        new(runId, false, errorMessage);

    public static OrchestratorResult Success(string runId, TaskRunExecutionResult executionResult) =>
        new(runId, true, executionResult: executionResult);

    public static OrchestratorResult AlreadyClaimedResult(string runId) =>
        new(runId, true, alreadyClaimed: true);
}
