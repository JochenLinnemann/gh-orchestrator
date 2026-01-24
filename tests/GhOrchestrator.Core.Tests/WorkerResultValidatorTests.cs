namespace GhOrchestrator.Core.Tests;

public class WorkerResultValidatorTests
{
    [Fact]
    public void Validate_FlagsUndeclaredAndMissingRepositories()
    {
        var plan = new TaskRunPlan(
            RunId: "run-24-20260124090000",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var workerResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-b",
                true,
                new[] { new AIWorkerFileChange("README.md", AIWorkerChangeType.Modify, "content") },
                "log",
                null)
        });
        var settings = WorkerResultValidationSettings.Default("bot", "bot@example.com");

        var result = WorkerResultValidator.Validate(plan, workerResult, settings);

        Assert.False(result.IsValid);
        Assert.Contains(result.RepoResults, item =>
            item.Repository == "org/service-b" &&
            item.Errors.Contains("AI worker returned changes for undeclared repository."));
        Assert.Contains(result.RepoResults, item =>
            item.Repository == "org/service-a" &&
            item.Errors.Contains("AI worker did not return results for repository."));
    }

    [Fact]
    public void Validate_FlagsSchemaChangesAndDeleteRatios()
    {
        var plan = new TaskRunPlan(
            RunId: "run-24-20260124090001",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var changes = new List<AIWorkerFileChange>
        {
            new("db/schema.sql", AIWorkerChangeType.Modify, "alter table"),
        };
        for (var i = 0; i < 6; i++)
        {
            changes.Add(new AIWorkerFileChange($"file-{i}.txt", AIWorkerChangeType.Delete, string.Empty));
        }
        for (var i = 0; i < 3; i++)
        {
            changes.Add(new AIWorkerFileChange($"file-{i}.md", AIWorkerChangeType.Modify, "content"));
        }
        var workerResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult("org/service-a", true, changes, "log", null)
        });
        var settings = WorkerResultValidationSettings.Default("bot", "bot@example.com");

        var result = WorkerResultValidator.Validate(plan, workerResult, settings);

        Assert.False(result.IsValid);
        var repoResult = Assert.Single(result.RepoResults, item => item.Repository == "org/service-a");
        Assert.Contains(repoResult.Errors, error => error.Contains("Schema change detected", StringComparison.Ordinal));
        Assert.Contains(repoResult.Errors, error => error.Contains("Delete change ratio", StringComparison.Ordinal));
    }
}
