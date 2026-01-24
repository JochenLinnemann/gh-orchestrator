namespace GhOrchestrator.Core.Tests;

public class AIPromptBuilderTests
{
    [Fact]
    public void Build_IncludesTaskRepositoryAndPolicySections()
    {
        var task = new TaskSpec(
            42,
            "org/orchestrator",
            "Add worker prompt builder",
            "Build a structured prompt for AI workers.",
            new[] { "org/orchestrator" },
            "agent",
            "Prompt includes sections.\nNo missing headings.",
            "No new dependencies.");

        var repositories = new[]
        {
            new AIPromptRepositoryContext(
                "org/orchestrator",
                "C#",
                new[] { "src/GhOrchestrator.Core/Orchestrator.cs" },
                new[] { "src", "tests" })
        };

        var policies = new AIPromptPolicies(
            new[] { "Do not log secrets." },
            new[] { "Use PascalCase for public members." },
            new[] { "Run unit tests: dotnet test." },
            new[] { "No CI changes required." },
            new[] { "Input validation explicit", "Errors handled clearly" });

        var request = new AIPromptRequest(
            task,
            repositories,
            policies);

        var result = AIPromptBuilder.Build(request);

        Assert.Contains("## Task", result);
        Assert.Contains("Add worker prompt builder", result);
        Assert.Contains("## Repository Context", result);
        Assert.Contains("org/orchestrator", result);
        Assert.Contains("## Policies", result);
        Assert.Contains("Do not log secrets.", result);
        Assert.Contains("## Success Criteria", result);
        Assert.Contains("Input validation explicit", result);
        Assert.Contains("Errors handled clearly", result);
    }

    [Fact]
    public void Build_EscapesMarkdownControlCharacters()
    {
        var task = new TaskSpec(
            7,
            "org/orchestrator",
            "Update *prompt* headings",
            "Avoid `backticks` and ## headings.",
            new[] { "org/orchestrator" },
            "agent",
            "Block ```code``` fences.",
            "Do not allow # injection.");

        var repositories = new[]
        {
            new AIPromptRepositoryContext(
                "org/orchestrator",
                "C#",
                new[] { "src/GhOrchestrator.Core/AIPromptBuilder.cs" },
                new[] { "src/GhOrchestrator.Core" })
        };

        var policies = new AIPromptPolicies(
            new[] { "No *markdown* injection." },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "Escape `characters`.", "No ## headings." });

        var request = new AIPromptRequest(
            task,
            repositories,
            policies);

        var result = AIPromptBuilder.Build(request);

        Assert.Contains("Update \\*prompt\\* headings", result);
        Assert.Contains("Avoid \\`backticks\\` and \\#\\# headings.", result);
        Assert.Contains("Block \\`\\`\\`code\\`\\`\\` fences.", result);
        Assert.Contains("Do not allow \\# injection.", result);
        Assert.Contains("No \\*markdown\\* injection.", result);
        Assert.Contains("Escape \\`characters\\`.", result);
        Assert.Contains("No \\#\\# headings.", result);
    }

    [Fact]
    public void Build_IncludesOutputSchemaAndRequirements()
    {
        var task = new TaskSpec(
            99,
            "org/test",
            "Schema validation",
            "Ensure output schema and requirements are present.",
            new[] { "org/test" },
            null,
            null,
            null);

        var request = new AIPromptRequest(
            task,
            Array.Empty<AIPromptRepositoryContext>(),
            new AIPromptPolicies(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "Output is deterministic" }));

        var result = AIPromptBuilder.Build(request);

        // Verify JSON schema section
        Assert.Contains("## Output Schema", result);
        Assert.Contains("\"repoResults\"", result);
        Assert.Contains("\"repository\"", result);
        Assert.Contains("\"changeType\": \"create|modify|delete\"", result);
        Assert.Contains("\"content\"", result);

        // Verify requirements section
        Assert.Contains("## Requirements", result);
        Assert.Contains("Include one repoResults entry for each repository listed.", result);
        Assert.Contains("Use empty changes array when no updates are needed.", result);
        Assert.Contains("Do not include any text outside the JSON.", result);
    }

    [Fact]
    public void Build_UsesPlaceholdersForMissingLists()
    {
        var task = new TaskSpec(
            3,
            "org/orchestrator",
            "Placeholder coverage",
            "Ensure empty lists are handled.",
            new[] { "org/orchestrator" },
            null,
            null,
            null);

        var request = new AIPromptRequest(
            task,
            Array.Empty<AIPromptRepositoryContext>(),
            new AIPromptPolicies(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "Output is deterministic" }));

        var result = AIPromptBuilder.Build(request);

        Assert.Contains("- (none provided)", result);
    }
}
