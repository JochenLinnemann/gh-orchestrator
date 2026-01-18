namespace GhOrchestrator.Core.Tests;

public class RunPreflightTests
{
    private static TaskSpec ValidTask => new(
        IssueNumber: 1,
        Repository: "org/repo",
        Title: "Add logging",
        Description: "Add logging",
        Repos: new[] { "org/repo" },
        TriggerUser: "alice",
        AcceptanceCriteria: "Tests pass",
        Constraints: "none"
    );

    private static IssueContext ValidContext => new(
        IssueExists: true,
        IsOpen: true,
        IssueUrl: "https://github.com/org/repo/issues/1"
    );

    [Fact]
    public void Validate_ValidTaskAndContext_Succeeds()
    {
        var result = RunPreflight.Validate(ValidTask, ValidContext);

        Assert.True(result.IsValid);
        Assert.False(result.NeedsHumanConfirmation);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_IssueNotExists_Fails()
    {
        var context = new IssueContext(IssueExists: false, IsOpen: true);

        var result = RunPreflight.Validate(ValidTask, context);

        Assert.False(result.IsValid);
        Assert.False(result.NeedsHumanConfirmation);
        Assert.Equal(PreflightFailureReason.IssueNotFound, result.FailureReason);
        Assert.Contains("does not exist", result.ErrorMessage);
    }

    [Fact]
    public void Validate_IssueClosed_Fails()
    {
        var context = new IssueContext(IssueExists: true, IsOpen: false);

        var result = RunPreflight.Validate(ValidTask, context);

        Assert.False(result.IsValid);
        Assert.False(result.NeedsHumanConfirmation);
        Assert.Equal(PreflightFailureReason.IssueClosed, result.FailureReason);
        Assert.Contains("closed", result.ErrorMessage);
    }

    [Fact]
    public void Validate_DestructiveInDescription_EscalatesWithConfirmation()
    {
        var task = ValidTask with { Description = "Delete all data from production database" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
        Assert.Equal(PreflightFailureReason.DestructiveIntentDetected, result.FailureReason);
        Assert.Contains("Potential destructive operation", result.ErrorMessage);
        Assert.Contains("delete", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DestructiveInAcceptanceCriteria_EscalatesWithConfirmation()
    {
        var task = ValidTask with { AcceptanceCriteria = "Wipe all user accounts" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
        Assert.Equal(PreflightFailureReason.DestructiveIntentDetected, result.FailureReason);
    }

    [Fact]
    public void Validate_DestructiveInConstraints_EscalatesWithConfirmation()
    {
        var task = ValidTask with { Constraints = "terraform destroy after testing" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
        Assert.Equal(PreflightFailureReason.DestructiveIntentDetected, result.FailureReason);
    }

    [Fact]
    public void Validate_DestructivePhraseCaseInsensitive_Detected()
    {
        var task = ValidTask with { Description = "DROP DATABASE mydb" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
    }

    [Fact]
    public void Validate_SafeTaskWithoutDestructivePhrases_Succeeds()
    {
        var task = ValidTask with 
        { 
            Description = "Add error handling to the database module",
            AcceptanceCriteria = "Errors are logged and reported",
            Constraints = "Do not change the API signature"
        };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.True(result.IsValid);
        Assert.False(result.NeedsHumanConfirmation);
    }

    [Fact]
    public void Validate_DestructivePhraseSuffixMatches()
    {
        var task = ValidTask with { Description = "We need to truncate the logs table" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
    }

    [Fact]
    public void Validate_DropTablePhraseDetected()
    {
        var task = ValidTask with { Description = "We must drop the old audit table" };

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
        Assert.Contains("drop", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_IssueMissingTakesPrecedenceOverDestructive()
    {
        var task = ValidTask with { Description = "Delete everything" };
        var context = new IssueContext(IssueExists: false, IsOpen: true);

        var result = RunPreflight.Validate(task, context);

        // Should fail on IssueNotFound, not escalate for destructive
        Assert.Equal(PreflightFailureReason.IssueNotFound, result.FailureReason);
        Assert.False(result.NeedsHumanConfirmation);
    }

    [Fact]
    public void Validate_IssueClosedTakesPrecedenceOverDestructive()
    {
        var task = ValidTask with { Description = "Delete everything" };
        var context = new IssueContext(IssueExists: true, IsOpen: false);

        var result = RunPreflight.Validate(task, context);

        // Should fail on IssueClosed, not escalate for destructive
        Assert.Equal(PreflightFailureReason.IssueClosed, result.FailureReason);
        Assert.False(result.NeedsHumanConfirmation);
    }

    [Fact]
    public void Validate_NullFieldsInTaskSpec_SafelyHandled()
    {
        var task = new TaskSpec(
            IssueNumber: 1,
            Repository: "org/repo",
            Title: string.Empty,
            Description: string.Empty,
            Repos: new[] { "org/repo" },
            TriggerUser: null,
            AcceptanceCriteria: null,
            Constraints: "none"
        );

        var result = RunPreflight.Validate(task, ValidContext);

        Assert.True(result.IsValid, "Should not crash on empty fields");
    }
}
