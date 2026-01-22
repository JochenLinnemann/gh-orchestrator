namespace GhOrchestrator.Core;

public record AIWorkerRequest(
    TaskSpec Task,
    IReadOnlyList<string> Repositories,
    AIPromptPolicies Policies
);
