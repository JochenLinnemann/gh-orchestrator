namespace GhOrchestrator.Core.Tests;

public class OrchestratorTests
{
    private readonly Orchestrator _orchestrator = new();
    private static readonly IssueContext OpenIssueContext = new(true, true, "https://github.com/org/repo/issues/42");

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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
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
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
            triggerUser: "bob"
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ProcessIssueComment_IssueClosed_FailsPreflight()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Tests pass

Constraints: none
";

        var closedIssueContext = new IssueContext(true, false, "https://github.com/org/repo/issues/42");

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: closedIssueContext,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.False(result.NeedsHumanConfirmation);
        Assert.Contains("closed", result.ErrorMessage);
        Assert.NotNull(result.PreflightResult);
        Assert.Equal(PreflightFailureReason.IssueClosed, result.PreflightResult?.FailureReason);
    }

    [Fact]
    public void ProcessIssueComment_DestructiveIntent_RequiresEscalation()
    {
        var comment = "/ai start\nDelete old tables";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Delete old tables

Constraints: none
";

        var result = _orchestrator.ProcessIssueComment(
            issueNumber: 42,
            repository: "org/main",
            commentText: comment,
            issueTitle: "Add logging",
            issueBody: issueBody,
            issueContext: OpenIssueContext,
            triggerUser: "bob"
        );

        Assert.False(result.IsValid);
        Assert.True(result.NeedsHumanConfirmation);
        Assert.Contains("destructive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.PreflightResult);
        Assert.Equal(PreflightFailureReason.DestructiveIntentDetected, result.PreflightResult?.FailureReason);
    }

    [Fact]
    public async Task ProcessIssueCommentAsync_UsesIssueContextFromGitHubClient()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Tests pass

Constraints: none
";
        var issue = new GitHubIssue(42, "Add logging", issueBody, false, "https://github.com/org/repo/issues/42");
        var client = new FakeGitHubClient(issue);
        var issueEvent = new IssueCommentEvent("org/main", 42, comment, "bob");

        var result = await _orchestrator.ProcessIssueCommentAsync(client, issueEvent);

        Assert.True(client.GetIssueCalled);
        Assert.False(result.IsValid);
        Assert.Equal(PreflightFailureReason.IssueClosed, result.PreflightResult?.FailureReason);
    }

    [Fact]
    public async Task ProcessIssueCommentAsync_OpenIssue_Passes()
    {
        var comment = "/ai start\nAdd logging";
        var issueBody = @"
## Repositories
- org/repo

## Acceptance Criteria
- Tests pass

Constraints: none
";
        var issue = new GitHubIssue(42, "Add logging", issueBody, true, "https://github.com/org/repo/issues/42");
        var client = new FakeGitHubClient(issue);
        var issueEvent = new IssueCommentEvent("org/main", 42, comment, "bob");

        var result = await _orchestrator.ProcessIssueCommentAsync(client, issueEvent);

        Assert.True(client.GetIssueCalled);
        Assert.True(result.IsValid);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        private readonly GitHubIssue? _issue;

        public FakeGitHubClient(GitHubIssue? issue)
        {
            _issue = issue;
        }

        public bool GetIssueCalled { get; private set; }

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default)
        {
            GetIssueCalled = true;
            return Task.FromResult(_issue);
        }

        public Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProjectTaskStateSnapshot> GetProjectTaskState(
            string repository,
            string projectId,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectTaskStateSnapshot(new ProjectTaskState(null, null, null), Array.Empty<string>()));

        public Task UpdateProjectFields(
            string repository,
            string projectId,
            int issueNumber,
            IReadOnlyCollection<ProjectFieldUpdate> updates,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreatePullRequest(string repository, PullRequestRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
