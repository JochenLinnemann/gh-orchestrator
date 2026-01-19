namespace GhOrchestrator.Core.Tests;

public class OrchestratorRetryTests
{
    [Fact]
    public async Task ProcessTaskAsync_WhenAlreadyClaimed_ReturnsAlreadyClaimedAndSkipsExecution()
    {
        var runId = "run-42-20260115083045";
        var issue = new GitHubIssue(
            42,
            "Test issue",
            """
            ## Repositories
            - octo-org/octo-repo

            ## Acceptance Criteria
            - Do the thing

            ## Constraints
            - No schema changes
            """,
            true,
            "https://example.com/issues/42");
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState("running", "In Progress", runId),
            Array.Empty<string>());
        var client = new FakeGitHubClient(issue, snapshot);
        var orchestrator = new Orchestrator();

        var result = await orchestrator.ProcessTaskAsync(
            client,
            new IssueCommentEvent("octo-org/octo-repo", 42, "/ai start do the thing", "octocat"),
            "project-id",
            runId);

        Assert.True(result.IsSuccess);
        Assert.True(result.AlreadyClaimed);
        Assert.False(client.UpdateProjectFieldsCalled);
        Assert.False(client.AddIssueCommentCalled);
        Assert.False(client.CreateBranchCalled);
        Assert.False(client.CreatePullRequestCalled);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        private readonly GitHubIssue _issue;
        private readonly ProjectTaskStateSnapshot _snapshot;

        public FakeGitHubClient(GitHubIssue issue, ProjectTaskStateSnapshot snapshot)
        {
            _issue = issue;
            _snapshot = snapshot;
        }

        public bool UpdateProjectFieldsCalled { get; private set; }

        public bool AddIssueCommentCalled { get; private set; }

        public bool CreateBranchCalled { get; private set; }

        public bool CreatePullRequestCalled { get; private set; }

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<GitHubIssue?>(_issue);

        public Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default)
        {
            AddIssueCommentCalled = true;
            return Task.CompletedTask;
        }

        public Task<ProjectTaskStateSnapshot> GetProjectTaskState(
            string repository,
            string projectId,
            int issueNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_snapshot);

        public Task UpdateProjectFields(
            string repository,
            string projectId,
            int issueNumber,
            IReadOnlyCollection<ProjectFieldUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            UpdateProjectFieldsCalled = true;
            return Task.CompletedTask;
        }

        public Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult("main");

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default)
        {
            CreateBranchCalled = true;
            throw new InvalidOperationException("Execution should not run when already claimed.");
        }

        public Task<PullRequestLink> CreatePullRequest(
            string repository,
            PullRequestRequest request,
            CancellationToken cancellationToken = default)
        {
            CreatePullRequestCalled = true;
            throw new InvalidOperationException("Execution should not run when already claimed.");
        }
    }
}
