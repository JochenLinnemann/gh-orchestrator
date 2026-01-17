namespace GhOrchestrator.Core.Tests;

public class TaskClaimPlannerTests
{
    [Fact]
    public void Plan_WhenUnclaimed_ReturnsUpdatesForAllFields()
    {
        var current = new ProjectTaskState(null, null, null);

        var result = TaskClaimPlanner.Plan(current, "run-42-20260115083045");

        Assert.True(result.IsValid);
        Assert.False(result.IsAlreadyClaimed);
        Assert.Collection(
            result.Updates,
            update =>
            {
                Assert.Equal(ProjectFieldNames.Ai, update.FieldName);
                Assert.Equal("running", update.Value);
            },
            update =>
            {
                Assert.Equal(ProjectFieldNames.Status, update.FieldName);
                Assert.Equal("In Progress", update.Value);
            },
            update =>
            {
                Assert.Equal(ProjectFieldNames.RunId, update.FieldName);
                Assert.Equal("run-42-20260115083045", update.Value);
            });
    }

    [Fact]
    public void Plan_WhenAlreadyClaimed_ReturnsIdempotentResult()
    {
        var current = new ProjectTaskState("running", "In Progress", "run-42-20260115083045");

        var result = TaskClaimPlanner.Plan(current, "run-42-20260115083045");

        Assert.True(result.IsValid);
        Assert.True(result.IsAlreadyClaimed);
        Assert.Empty(result.Updates);
    }

    [Fact]
    public void Plan_WhenDifferentRunId_ReturnsFailure()
    {
        var current = new ProjectTaskState("running", "In Progress", "run-41-20260115083045");

        var result = TaskClaimPlanner.Plan(current, "run-42-20260115083045");

        Assert.False(result.IsValid);
        Assert.Contains("Run ID", result.ErrorMessage);
        Assert.Empty(result.Updates);
    }
}
