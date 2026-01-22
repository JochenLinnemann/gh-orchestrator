namespace GhOrchestrator.Core;

public record AIWorkerRequest(
    TaskSpec Task,
    IReadOnlyList<string> Repositories,
    IReadOnlyDictionary<string, string> Policies,
    IReadOnlyDictionary<string, string> ExecutionConstraints
);
