namespace GhOrchestrator.Core.Tests;

public class TaskQualityGateTests
{
    [Fact]
    public void Validate_ValidTask_Passes()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Code compiles and tests pass",
            Constraints: "No schema changes"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingAcceptanceCriteria_Fails()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: "alice",
            AcceptanceCriteria: null,
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.False(result.IsValid);
        Assert.Contains("Acceptance criteria", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyAcceptanceCriteria_Fails()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: "alice",
            AcceptanceCriteria: "   ",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.False(result.IsValid);
        Assert.Contains("Acceptance criteria", result.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingConstraints_Fails()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: null
        );

        var result = TaskQualityGate.Validate(task);

        Assert.False(result.IsValid);
        Assert.Contains("Constraints", result.ErrorMessage);
        Assert.Contains("none", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ConstraintsSetToNone_Passes()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_NoRepos_Fails()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: Array.Empty<string>(),
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.False(result.IsValid);
        Assert.Contains("Repos", result.ErrorMessage);
        Assert.Contains("non-empty", result.ErrorMessage);
    }

    [Fact]
    public void Validate_InvalidRepoFormat_Fails()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "invalid-format" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid repository format", result.ErrorMessage);
    }

    [Fact]
    public void Validate_MultipleRepos_Passes()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Update across services",
            Repos: new[] { "org/service-a", "org/service-b", "org/service-c" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Changes applied to all repos",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingTriggerUser_StillPasses()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Description: "Add logging",
            Repos: new[] { "org/repo" },
            TriggerUser: null,
            AcceptanceCriteria: "Tests pass",
            Constraints: "none"
        );

        var result = TaskQualityGate.Validate(task);

        Assert.True(result.IsValid, "TriggerUser should not be a hard requirement");
    }
}
