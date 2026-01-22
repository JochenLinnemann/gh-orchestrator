namespace GhOrchestrator.Core;

public record AIWorkerRequest(
    TaskSpec Task,
    IReadOnlyList<string> Repositories,
    string? DefinitionOfDone,
    IReadOnlyDictionary<string, string> Policies,
    IReadOnlyDictionary<string, string> ExecutionConstraints
);
