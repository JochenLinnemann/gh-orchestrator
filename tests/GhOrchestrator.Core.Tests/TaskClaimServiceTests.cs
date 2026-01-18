namespace GhOrchestrator.Core.Tests;

public class TaskClaimServiceTests
{
    [Fact]
    public async Task ClaimAsync_WhenProjectMissingFields_ReturnsFailure()
    {
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState(null, null, null),
            new[] { ProjectFieldNames.Ai });
        var client = new FakeGitHubClient(snapshot);
        var service = new TaskClaimService();

        var result = await service.ClaimAsync(client, "org/repo", "project-id", 42, "run-42");

        Assert.False(result.IsValid);
        Assert.Contains("missing required fields", result.ErrorMessage);
        Assert.False(client.UpdateProjectFieldsCalled);
    }

    [Fact]
    public async Task ClaimAsync_WhenAlreadyClaimed_SkipsUpdate()
    {
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState("running", "In Progress", "run-42"),
            Array.Empty<string>());
        var client = new FakeGitHubClient(snapshot);
        var service = new TaskClaimService();

        var result = await service.ClaimAsync(client, "org/repo", "project-id", 42, "run-42");

        Assert.True(result.IsValid);
        Assert.True(result.IsAlreadyClaimed);
        Assert.False(client.UpdateProjectFieldsCalled);
    }

    [Fact]
    public async Task ClaimAsync_WhenUpdateFails_ReturnsFailure()
    {
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState(null, null, null),
            Array.Empty<string>());
        var client = new FakeGitHubClient(snapshot)
        {
            ThrowOnUpdate = true
        };
        var service = new TaskClaimService();

        var result = await service.ClaimAsync(client, "org/repo", "project-id", 42, "run-42");

        Assert.False(result.IsValid);
        Assert.Equal("Failed to update project fields.", result.ErrorMessage);
        Assert.True(client.UpdateProjectFieldsCalled);
    }

    [Fact]
    public async Task ClaimAsync_WhenUpdatesNeeded_AppliesUpdates()
    {
        var snapshot = new ProjectTaskStateSnapshot(
            new ProjectTaskState(null, "To Do", null),
            Array.Empty<string>());
        var client = new FakeGitHubClient(snapshot);
        var service = new TaskClaimService();

        var result = await service.ClaimAsync(client, "org/repo", "project-id", 42, "run-42");

        Assert.True(result.IsValid);
        Assert.False(result.IsAlreadyClaimed);
        Assert.True(client.UpdateProjectFieldsCalled);
        Assert.NotNull(client.LastUpdates);
        Assert.Contains(client.LastUpdates, update => update.FieldName == ProjectFieldNames.Ai);
        Assert.Contains(client.LastUpdates, update => update.FieldName == ProjectFieldNames.Status);
        Assert.Contains(client.LastUpdates, update => update.FieldName == ProjectFieldNames.RunId);
    }

    [Fact]
    public async Task ClaimAsync_WhenProjectStateReadFails_ReturnsFailure()
    {
        var client = new FakeGitHubClient(new ProjectTaskStateSnapshot(new ProjectTaskState(null, null, null), Array.Empty<string>()))
        {
            ThrowOnGetProjectTaskState = true
        };
        var service = new TaskClaimService();

        var result = await service.ClaimAsync(client, "org/repo", "project-id", 42, "run-42");

        Assert.False(result.IsValid);
        Assert.Equal("Failed to read project task state.", result.ErrorMessage);
        Assert.False(client.UpdateProjectFieldsCalled);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        private readonly ProjectTaskStateSnapshot _snapshot;

        public FakeGitHubClient(ProjectTaskStateSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public bool ThrowOnUpdate { get; set; }

        public bool ThrowOnGetProjectTaskState { get; set; }

        public bool UpdateProjectFieldsCalled { get; private set; }

        public IReadOnlyCollection<ProjectFieldUpdate>? LastUpdates { get; private set; }

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<GitHubIssue?>(null);

        public Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProjectTaskStateSnapshot> GetProjectTaskState(
            string repository,
            string projectId,
            int issueNumber,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetProjectTaskState)
                throw new InvalidOperationException("boom");

            return Task.FromResult(_snapshot);
        }

        public Task UpdateProjectFields(
            string repository,
            string projectId,
            int issueNumber,
            IReadOnlyCollection<ProjectFieldUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            UpdateProjectFieldsCalled = true;
            LastUpdates = updates;

            if (ThrowOnUpdate)
                throw new InvalidOperationException("boom");

            return Task.CompletedTask;
        }

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreatePullRequest(string repository, PullRequestRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
