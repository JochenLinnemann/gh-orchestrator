namespace GhOrchestrator.Core;

/// <summary>
/// Request payload for building an AI worker prompt.
/// </summary>
public record AIPromptRequest(
    TaskSpec Task,
    IReadOnlyList<AIPromptRepositoryContext> Repositories,
    AIPromptPolicies Policies,
    IReadOnlyList<string> SuccessCriteria
);
