using Xunit;

namespace GhOrchestrator.Core.Tests;

public class TaskCompletionPlannerTests
{
    [Fact]
    public void Plan_WithRunningStatus_TransitionsToBlocked()
    {
        // Arrange
        var state = new ProjectTaskState(
            AiStatus: "running",
            Status: "In Progress",
            RunId: "run-12345");

        // Act
        var result = TaskCompletionPlanner.Plan(state);

        // Assert
        Assert.True(result.IsValid);
        Assert.False(result.IsAlreadyCompleted);
        Assert.Single(result.Updates);
        Assert.Equal("AI", result.Updates[0].FieldName);
        Assert.Equal("blocked", result.Updates[0].Value);
    }

    [Fact]
    public void Plan_WithBlockedStatus_ReturnsAlreadyCompleted()
    {
        // Arrange
        var state = new ProjectTaskState(
            AiStatus: "blocked",
            Status: "In Progress",
            RunId: "run-12345");

        // Act
        var result = TaskCompletionPlanner.Plan(state);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsAlreadyCompleted);
        Assert.Empty(result.Updates);
    }

    [Fact]
    public void Plan_WithoutRunId_ReturnsFailure()
    {
        // Arrange
        var state = new ProjectTaskState(
            AiStatus: "running",
            Status: "In Progress",
            RunId: null);

        // Act
        var result = TaskCompletionPlanner.Plan(state);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Run ID", result.ErrorMessage);
    }

    [Fact]
    public void Plan_WithBlockedStatusCaseInsensitive_ReturnsAlreadyCompleted()
    {
        // Arrange
        var state = new ProjectTaskState(
            AiStatus: "BLOCKED",
            Status: "In Progress",
            RunId: "run-12345");

        // Act
        var result = TaskCompletionPlanner.Plan(state);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsAlreadyCompleted);
    }
}
