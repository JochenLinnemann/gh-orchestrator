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

}
