namespace GhOrchestrator.Core.Tests;

public class IssueCommentReportServiceTests
{
    [Fact]
    public async Task PostReportAsync_PostsFormattedComment()
    {
        var client = new FakeGitHubClient();
        var task = new TaskSpec(
            IssueNumber: 42,
            Repository: "org/main",
            Title: "Improve logging",
            Description: "Add logging",
            Repos: new[] { "org/service-a" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: "none");
        var service = new IssueCommentReportService();

        await service.PostReportAsync(
            client,
            task,
            "Updated logging.",
            "Run `dotnet test`.",
            new[]
            {
                RepoExecutionResult.Success(
                    "org/service-a",
                    "ai/run-1",
                    "main",
                    new PullRequestLink("org/service-a", "https://github.com/org/service-a/pull/10"))
            },
            new[] { "Touches shared logging middleware." });

        Assert.Equal("org/main", client.LastCommentRepository);
        Assert.Equal(42, client.LastCommentIssueNumber);
        Assert.NotNull(client.LastCommentBody);
        Assert.Contains("## Summary", client.LastCommentBody);
        Assert.Contains("Updated logging.", client.LastCommentBody);
        Assert.Contains("✅ org/service-a", client.LastCommentBody);
        Assert.Contains("https://github.com/org/service-a/pull/10", client.LastCommentBody);
        Assert.Contains("Run \\`dotnet test\\`.", client.LastCommentBody);
        Assert.Contains("Touches shared logging middleware.", client.LastCommentBody);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        public string? LastCommentRepository { get; private set; }

        public int? LastCommentIssueNumber { get; private set; }

        public string? LastCommentBody { get; private set; }

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<GitHubIssue?>(null);

        public Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default)
        {
            LastCommentRepository = repository;
            LastCommentIssueNumber = issueNumber;
            LastCommentBody = body;
            return Task.CompletedTask;
        }

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

        public Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult("main");

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PullRequestLink> CreatePullRequest(
            string repository,
            PullRequestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PullRequestLink(repository, "https://example.com/pr"));
    }
}
