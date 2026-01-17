namespace GhOrchestrator.Core;

public record ProjectTaskState(
    string? AiStatus,
    string? Status,
    string? RunId);
