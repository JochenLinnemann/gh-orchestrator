namespace GhOrchestrator.Core.Tests;

public class IssueCommentReportFormatterTests
{
    [Fact]
    public void Format_IncludesAllSectionsAndEntries()
    {
        var summary = "Updated logging to include correlation IDs.";
        var testInstructions = "Run unit tests: dotnet test.";
        var executionResults = new[]
        {
            RepoExecutionResult.Success(
                "org/service-a",
                "ai/run-1",
                "main",
                new PullRequestLink("org/service-a", "https://github.com/org/service-a/pull/10")),
            RepoExecutionResult.Failure(
                "org/service-b",
                "ai/run-1",
                "main",
                "Failed to open PR.")
        };
        var riskNotes = new[]
        {
            "Touches shared logging middleware.",
            "No database changes."
        };

        var result = IssueCommentReportFormatter.Format(summary, testInstructions, executionResults, riskNotes);

        Assert.Contains("## Summary", result);
        Assert.Contains(summary, result);
        Assert.Contains("## Pull Requests", result);
        Assert.Contains("✅ org/service-a", result);
        Assert.Contains("https://github.com/org/service-a/pull/10", result);
        Assert.Contains("❌ org/service-b", result);
        Assert.Contains("Failed to open PR.", result);
        Assert.Contains("## How to Test", result);
        Assert.Contains(testInstructions, result);
        Assert.Contains("## Risks", result);
        Assert.Contains("Touches shared logging middleware.", result);
        Assert.Contains("No database changes.", result);
    }

    [Fact]
    public void Format_UsesPlaceholdersWhenListsAreEmpty()
    {
        var result = IssueCommentReportFormatter.Format(
            "No changes needed.",
            "No tests required.",
            Array.Empty<RepoExecutionResult>(),
            Array.Empty<string>());

        Assert.Contains("- (none)", result);
        Assert.Contains("- None noted.", result);
    }

    [Fact]
    public void Format_EscapesMarkdownControlCharacters()
    {
        var result = IssueCommentReportFormatter.Format(
            "Update *logging* and `tracing`.",
            "Run `dotnet test` in (core).",
            new[]
            {
                RepoExecutionResult.Failure(
                    "org/service-a",
                    "ai/run-1",
                    "main",
                    "PR failed because `base` was missing.")
            },
            new[] { "Risk: touches *shared* pipelines." });

        Assert.Contains("Update \\*logging\\* and \\`tracing\\`.", result);
        Assert.Contains("Run \\`dotnet test\\` in \\(core\\).", result);
        Assert.Contains("PR failed because \\`base\\` was missing.", result);
        Assert.Contains("Risk: touches \\*shared\\* pipelines.", result);
    }
}
