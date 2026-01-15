namespace GhOrchestrator.Core.Tests;

public class OrchestratorTests
{
    private readonly Orchestrator _orchestrator = new();

    [Fact]
    public void ProcessIssueComment_ValidCommentAndIssue_Passes()
    {
        var comment = "/ai start\nAdd logging to all services";
        var issueBody = @"
## Repositories
- org/service-a
- org/service-b

## Acceptance Criteria
- Logging added to all services
- Tests pass

## Constraints
- No schema changes
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_MissingAiStartCommand_Fails()
    {
        var comment = "This is just a comment";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Tests pass

Constraints: none
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.Contains("/ai start", result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_MissingReposSection_Fails()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Acceptance Criteria
- Tests pass

Constraints: none
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.Contains("Repos", result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_MissingAcceptanceCriteria_Fails()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

Constraints: none
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.Contains("Acceptance criteria", result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_MissingConstraints_Fails()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Tests pass
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.Contains("Constraints", result.ErrorMessage);
    }

    [Fact]
    public void ProcessIssueComment_SingleLineFormat_Passes()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

Acceptance Criteria: Tests must pass
Constraints: none
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueBody: issueBody,
            triggerUser: "bob"
        );

        Assert.True(result.IsValid);
    }
}
