namespace GhOrchestrator.Core.Tests;

public class IssueCommentReportFormatterTests
{
    [Fact]
    public void Format_IncludesAllSectionsAndEntries()
    {
        var summary = "Updated logging to include correlation IDs.";
        var testInstructions = "Run unit tests: dotnet test.";
        var pullRequests = new[]
        {
            new PullRequestLink("org/service-a", "https://github.com/org/service-a/pull/10"),
            new PullRequestLink("org/service-b", "https://github.com/org/service-b/pull/12")
        };
        var riskNotes = new[]
        {
            "Touches shared logging middleware.",
            "No database changes."
        };

        var result = IssueCommentReportFormatter.Format(summary, testInstructions, pullRequests, riskNotes);

        Assert.Contains("## Summary", result);
        Assert.Contains(summary, result);
        Assert.Contains("## Pull Requests", result);
        Assert.Contains("org/service-a", result);
        Assert.Contains("org/service-b", result);
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
            Array.Empty<PullRequestLink>(),
            Array.Empty<string>());

        Assert.Contains("- (none)", result);
        Assert.Contains("- None noted.", result);
    }
}
