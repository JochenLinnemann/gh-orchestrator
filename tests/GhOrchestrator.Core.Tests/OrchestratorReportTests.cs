namespace GhOrchestrator.Core.Tests;

public class OrchestratorReportTests
{
    [Fact]
    public async Task ProcessTaskAsync_WhenReportCanceled_ReturnsSuccess()
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
            - none
            """,
            true,
            "https://example.com/issues/42");
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState(null, null, null),
            Array.Empty<string>());
        var client = new FakeGitHubClient(issue, snapshot);
        var orchestrator = new Orchestrator();

        var result = await orchestrator.ProcessTaskAsync(
            client,
            new IssueCommentEvent("octo-org/octo-repo", 42, "/ai start do the thing", "octocat"),
            "project-id",
            runId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ExecutionResult);
        Assert.Single(result.ExecutionResult!.Results);
        Assert.True(client.AddIssueCommentCalled);
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

        public bool AddIssueCommentCalled { get; private set; }

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<GitHubIssue?>(_issue);

        public Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default)
        {
            AddIssueCommentCalled = true;
            throw new TaskCanceledException("Report canceled.");
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult("main");

        public Task<string> GetRepositoryCloneUrl(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://example.com/{repository}.git");

        public Task<string> GetRepositoryAccessToken(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult("token");

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PullRequestLink> CreatePullRequest(
            string repository,
            PullRequestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PullRequestLink(repository, $"https://example.com/{repository}/pulls/1"));
    }
}
