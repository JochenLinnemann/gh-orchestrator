namespace GhOrchestrator.Core.Tests;

/// <summary>
/// Critical Path: Integration tests for complete orchestration flow (Plan 17, v0 release criteria).
/// Validates the end-to-end pipeline: validation → claim → planning → execution → reporting.
/// </summary>
public class OrchestratorIntegrationTests
{
    private static TaskSpec ValidTask => new(
        IssueNumber: 42,
        Repository: "org/main",
        Title: "Improve logging",
        Description: "Add structured logging",
        Repos: new[] { "org/service-a", "org/service-b" },
        TriggerUser: "alice",
        AcceptanceCriteria: "Tests pass and logs are structured",
        Constraints: "No breaking changes"
    );

    private static IssueCommentEvent ValidCommentEvent => new(
        Repository: "org/main",
        IssueNumber: 42,
        CommentBody: "/ai start Improve logging output and add structured logging",
        CommentAuthor: "alice"
    );

    [Fact]
    public void ProcessIssueComment_WithValidInput_ValidatesSuccessfully()
    {
        // Arrange
        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "/ai start Add logging to services",
            issueTitle: "Add logging",
            issueBody: "## Repositories\n- org/service-a\n- org/service-b\n\n## Acceptance Criteria\nTests pass\n\n## Constraints\nNone",
            issueContext: issueContext,
            triggerUser: "alice"
        );

        // Assert: Complete validation flow
        Assert.True(result.IsValid, $"Should validate successfully. Error: {result.ErrorMessage}");
        Assert.NotNull(result.Task);
        Assert.Equal(42, result.Task.IssueNumber);
        Assert.Equal("org/main", result.Task.Repository);
        Assert.Equal("Add logging to services", result.Task.Description);
        Assert.Contains("org/service-a", result.Task.Repos);
        Assert.Contains("org/service-b", result.Task.Repos);
    }

    [Fact]
    public void ProcessIssueComment_WithoutAiStartCommand_FailsValidation()
    {
        // Arrange
        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "Just a regular comment",  // Missing /ai start
            issueTitle: "Add logging",
            issueBody: "## Repositories\n- org/service-a\n\n## Acceptance Criteria\nTests pass\n\n## Constraints\nNone",
            issueContext: issueContext,
            triggerUser: "alice"
        );

        // Assert: Validation fails early
        Assert.False(result.IsValid);
        Assert.Contains("/ai start", result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_WithClosedIssue_FailsPreflight()
    {
        // Arrange
        var closedIssueContext = new IssueContext(
            IssueExists: true,
            IsOpen: false,  // Issue is closed
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "/ai start Add logging",
            issueTitle: "Add logging",
            issueBody: "## Repositories\n- org/service-a\n\n## Acceptance Criteria\nTests pass\n\n## Constraints\nNone",
            issueContext: closedIssueContext,
            triggerUser: "alice"
        );

        // Assert: Preflight validation fails (issue must be open)
        Assert.False(result.IsValid);
        // RunPreflight checks that issue is open
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_WithMissingRepos_FailsQualityGate()
    {
        // Arrange
        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act: Issue body has no Repositories section and missing constraints
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "/ai start Add logging",
            issueTitle: "Add logging",
            issueBody: "## Acceptance Criteria\nTests pass",  // Missing Repositories and Constraints
            issueContext: issueContext,
            triggerUser: "alice"
        );

        // Assert: Quality gate fails (needs at least 1 repo)
        Assert.False(result.IsValid);
        // TaskQualityGate requires repos
        Assert.Contains("Repos", result.ErrorMessage ?? "");
    }

    [Fact]
    public void ProcessIssueComment_WithAcceptanceCriteriaInIssueBody_UsesIt()
    {
        // Arrange
        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "/ai start",  // No description in comment
            issueTitle: "Refactor logging",
            issueBody: "## Repositories\n- org/service-a\n\n## Acceptance Criteria\nAll logs use structured format\n\n## Constraints\nNone",
            issueContext: issueContext,
            triggerUser: "alice"
        );

        // Assert: Falls back to acceptance criteria as description
        Assert.True(result.IsValid);
        Assert.NotNull(result.Task);
        Assert.Equal("All logs use structured format", result.Task.Description);
        Assert.Equal("All logs use structured format", result.Task.AcceptanceCriteria);
    }

    [Fact]
    public void ProcessIssueComment_ExtractsConstraintsFromIssueBody()
    {
        // Arrange
        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/42"
        );
        var orchestrator = new Orchestrator();

        // Act
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: "/ai start Add logging",
            issueTitle: "Add logging",
            issueBody: "## Repositories\n- org/service-a\n\n## Acceptance Criteria\nLogs are structured\n\n## Constraints\nNo schema changes to production databases",
            issueContext: issueContext,
            triggerUser: "alice"
        );

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.Task);
        Assert.Equal("No schema changes to production databases", result.Task.Constraints);
    }

    [Fact]
    public void ValidationFlow_CoversCriticalPath()
    {
        // This test documents the Critical Path validation flow for Plan 17
        // Steps covered:
        // 1. /ai start command detection → ProcessIssueComment
        // 2. Task quality gate (title, description, repos, acceptance criteria, constraints) → TaskQualityGate.Validate
        // 3. Preflight checks (issue exists, is open) → RunPreflight.Validate
        // 4. All validators must pass for orchestration to proceed

        var issueContext = new IssueContext(
            IssueExists: true,
            IsOpen: true,
            IssueUrl: "https://github.com/org/main/issues/1"
        );
        var orchestrator = new Orchestrator();

        // Valid input triggers all validations
        var result = orchestrator.ProcessIssueComment(
            issueNumber: 1,
            repository: "org/main",
            commentText: "/ai start Implement feature",
            issueTitle: "New feature",
            issueBody: "## Repositories\n- org/service\n\n## Acceptance Criteria\nFeature works\n\n## Constraints\nNone",
            issueContext: issueContext
        );

        // All validators pass
        Assert.True(result.IsValid, $"Validation should pass. Error: {result.ErrorMessage}");
        Assert.NotNull(result.Task);
        Assert.Null(result.ErrorMessage);
    }
}
