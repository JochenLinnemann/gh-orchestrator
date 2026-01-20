namespace GhOrchestrator.Core;

public record AIWorkerRepoResult(
    string Repository,
    bool IsSuccess,
    IReadOnlyList<string> FilesChanged,
    string? ExecutionLog,
    string? FailureReason
);
