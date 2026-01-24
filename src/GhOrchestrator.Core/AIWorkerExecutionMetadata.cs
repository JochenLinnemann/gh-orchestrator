namespace GhOrchestrator.Core;

public record AIWorkerExecutionMetadata(
    string? Model,
    TimeSpan? ExecutionDuration,
    AIWorkerTokenUsage? TokenUsage,
    string? ExecutionTraceUrl,
    string? Confidence,
    IReadOnlyList<string> Warnings);
