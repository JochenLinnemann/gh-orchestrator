namespace GhOrchestrator.Core;

public static class AIPromptPolicyProvider
{
    public static AIPromptPolicies Default { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { 
            "Input validation is explicit",
            "Errors are handled clearly",
            "Existing behavior is preserved",
            "Output is valid JSON",
            "All repositories have results",
            "File paths are correct" 
        });

}
