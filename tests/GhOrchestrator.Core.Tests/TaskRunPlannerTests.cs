namespace GhOrchestrator.Core.Tests;

/// <summary>
/// Critical Path: Tests for task run planning (Plan 17, v0 release criteria).
/// Validates that execution plans are correctly generated with proper repository and branch mapping.
/// </summary>
public class TaskRunPlannerTests
{
    private static TaskSpec ValidTask => new(
        IssueNumber: 42,
        Repository: "org/main",
        Title: "Add logging",
        Description: "Add logging",
        Repos: new[] { "org/service-a", "org/service-b" },
        TriggerUser: "alice",
        AcceptanceCriteria: "Tests pass",
        Constraints: "none"
    );

    [Fact]
    public void Plan_MultiRepoTask_BuildsStepsForEachRepo()
    {
        var now = new DateTimeOffset(2026, 1, 15, 8, 30, 45, TimeSpan.Zero);

        var result = TaskRunPlanner.Plan(ValidTask, now);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Plan);
        Assert.Equal(new[] { "org/service-a", "org/service-b" }, result.Plan?.Repos);
        Assert.Equal(6, result.Plan?.Steps.Count);
        Assert.Equal(
            new TaskRunStep(TaskRunStepType.CreateBranch, "org/service-a"),
            result.Plan?.Steps[0]
        );
        Assert.Equal(
            new TaskRunStep(TaskRunStepType.ExecuteTask, "org/service-a"),
            result.Plan?.Steps[1]
        );
        Assert.Equal(
            new TaskRunStep(TaskRunStepType.OpenPullRequest, "org/service-a"),
            result.Plan?.Steps[2]
        );
    }

    [Fact]
    public void Plan_RunIdFormatting_UsesIssueAndTimestamp()
    {
        var now = new DateTimeOffset(2026, 1, 15, 8, 30, 45, TimeSpan.Zero);

        var result = TaskRunPlanner.Plan(ValidTask, now);

        Assert.Equal("run-42-20260115083045", result.Plan?.RunId);
    }

    [Fact]
    public void Plan_NoRepos_ReturnsFailure()
    {
        var task = ValidTask with { Repos = Array.Empty<string>() };

        var result = TaskRunPlanner.Plan(task, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        Assert.Contains("Repos", result.ErrorMessage);
    }
}
