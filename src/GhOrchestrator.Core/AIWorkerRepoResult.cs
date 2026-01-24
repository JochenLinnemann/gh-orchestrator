namespace GhOrchestrator.Core;

public record AIWorkerRepoResult(
    string Repository,
    bool IsSuccess,
    IReadOnlyList<AIWorkerFileChange> FileChanges,
    string? ExecutionLog,
    string? FailureReason
);
