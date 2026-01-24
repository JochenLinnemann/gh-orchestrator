namespace GhOrchestrator.Core;

public record AIWorkerTokenUsage(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
