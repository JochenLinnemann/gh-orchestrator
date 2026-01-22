namespace GhOrchestrator.Core;

public static class AIPromptPolicyProvider
{
    public static AIPromptPolicies Default { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { "All tests pass", "Code is committed to branch", "PR is opened" },
        new[] { "Output is valid JSON", "All repositories have results", "File paths are correct" });

    public static IReadOnlyDictionary<string, string> ToDictionary(AIPromptPolicies policies)
    {
        if (policies is null)
            throw new ArgumentNullException(nameof(policies));

        return new Dictionary<string, string>
        {
            ["security"] = string.Join('\n', policies.Security),
            ["naming"] = string.Join('\n', policies.Naming),
            ["testing"] = string.Join('\n', policies.Testing),
            ["ci_cd"] = string.Join('\n', policies.CiCd),
            ["definition_of_done"] = string.Join('\n', policies.DefinitionOfDone),
            ["success_criteria"] = string.Join('\n', policies.SuccessCriteria)
        };
    }
}
