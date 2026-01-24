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
        var workerResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                new[] { new AIWorkerFileChange("src/Logging.cs", AIWorkerChangeType.Modify, "content") },
                "log",
                null),
            new AIWorkerRepoResult(
                "org/service-b",
                true,
                Array.Empty<AIWorkerFileChange>(),
                "log",
                null)
        });
        var validationResult = new WorkerResultValidationResult(new[]
        {
            WorkerResultRepoValidationResult.Success("org/service-a"),
            WorkerResultRepoValidationResult.Success("org/service-b")
        });
        var executionResult = new TaskRunExecutionResult(executionResults, workerResult, validationResult);

        var result = IssueCommentReportFormatter.Format(summary, testInstructions, executionResults, executionResult, riskNotes);

        Assert.Contains("## Summary", result);
        Assert.Contains(summary, result);
        Assert.Contains("## Pull Requests", result);
        Assert.Contains("✅ org/service-a", result);
        Assert.Contains("https://github.com/org/service-a/pull/10", result);
        Assert.Contains("❌ org/service-b", result);
        Assert.Contains("Failed to open PR.", result);
        Assert.Contains("## How to Test", result);
        Assert.Contains(testInstructions, result);
        Assert.Contains("## Execution Details", result);
        Assert.Contains("### Files Changed", result);
        Assert.Contains("src/Logging.cs", result);
        Assert.Contains("### Unchanged Repositories", result);
        Assert.Contains("## Risks", result);
        Assert.Contains("Touches shared logging middleware.", result);
        Assert.Contains("No database changes.", result);
    }

    [Fact]
    public void Format_UsesPlaceholdersWhenListsAreEmpty()
    {
        var executionResult = new TaskRunExecutionResult(
            Array.Empty<RepoExecutionResult>(),
            new AIWorkerResult(Array.Empty<AIWorkerRepoResult>()),
            new WorkerResultValidationResult(Array.Empty<WorkerResultRepoValidationResult>()));

        var result = IssueCommentReportFormatter.Format(
            "No changes needed.",
            "No tests required.",
            Array.Empty<RepoExecutionResult>(),
            executionResult,
            Array.Empty<string>());

        Assert.Contains("- (none)", result);
        Assert.Contains("- None noted.", result);
    }

    [Fact]
    public void Format_EscapesMarkdownControlCharacters()
    {
        var executionResult = new TaskRunExecutionResult(
            new[]
            {
                RepoExecutionResult.Failure(
                    "org/service-a",
                    "ai/run-1",
                    "main",
                    "PR failed because `base` was missing.")
            },
            new AIWorkerResult(new[]
            {
                new AIWorkerRepoResult(
                    "org/service-a",
                    true,
                    new[] { new AIWorkerFileChange("src/`logging`.cs", AIWorkerChangeType.Modify, "content") },
                    "log",
                    null)
            }),
            new WorkerResultValidationResult(new[]
            {
                WorkerResultRepoValidationResult.Success("org/service-a")
            }));

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
            executionResult,
            new[] { "Risk: touches *shared* pipelines." });

        Assert.Contains("Update \\*logging\\* and \\`tracing\\`.", result);
        Assert.Contains("Run \\`dotnet test\\` in \\(core\\).", result);
        Assert.Contains("PR failed because \\`base\\` was missing.", result);
        Assert.Contains("src/\\`logging\\`.cs", result);
        Assert.Contains("Risk: touches \\*shared\\* pipelines.", result);
    }

    [Fact]
    public void Format_ListsValidationWarnings()
    {
        var executionResults = new[]
        {
            RepoExecutionResult.Failure(
                "org/service-a",
                "ai/run-1",
                "main",
                "AI worker returned no file changes.")
        };
        var workerResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                Array.Empty<AIWorkerFileChange>(),
                "log",
                null)
        });
        var validationResult = new WorkerResultValidationResult(new[]
        {
            WorkerResultRepoValidationResult.Failure(
                "org/service-a",
                new[] { "AI worker returned no file changes." })
        });
        var executionResult = new TaskRunExecutionResult(executionResults, workerResult, validationResult);

        var result = IssueCommentReportFormatter.Format(
            "No changes.",
            "No tests.",
            executionResults,
            executionResult,
            Array.Empty<string>());

        Assert.Contains("### Validation Warnings", result);
        Assert.Contains("AI worker returned no file changes.", result);
    }

    [Fact]
    public void Format_IgnoresDuplicateRepoResultsForFileChanges()
    {
        var executionResults = new[]
        {
            RepoExecutionResult.Success(
                "org/service-a",
                "ai/run-1",
                "main",
                new PullRequestLink("org/service-a", "https://github.com/org/service-a/pull/10"))
        };
        var workerResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                new[] { new AIWorkerFileChange("src/one.cs", AIWorkerChangeType.Modify, "content") },
                "log",
                null),
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                new[] { new AIWorkerFileChange("src/two.cs", AIWorkerChangeType.Modify, "content") },
                "log",
                null)
        });
        var validationResult = new WorkerResultValidationResult(new[]
        {
            WorkerResultRepoValidationResult.Failure(
                "org/service-a",
                new[] { "AI worker returned duplicate results for repository." })
        });
        var executionResult = new TaskRunExecutionResult(executionResults, workerResult, validationResult);

        var result = IssueCommentReportFormatter.Format(
            "Duplicates detected.",
            "No tests.",
            executionResults,
            executionResult,
            Array.Empty<string>());

        Assert.Contains("### Files Changed", result);
        Assert.Contains("src/one.cs", result);
        Assert.DoesNotContain("src/two.cs", result);
        Assert.Contains("### Validation Warnings", result);
    }
}
