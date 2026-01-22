namespace GhOrchestrator.Core;

/// <summary>
/// Policy guidance to include in AI worker prompts.
/// </summary>
public record AIPromptPolicies(
    IReadOnlyList<string> Security,
    IReadOnlyList<string> Naming,
    IReadOnlyList<string> Testing,
    IReadOnlyList<string> CiCd,
    IReadOnlyList<string> DefinitionOfDone
);
