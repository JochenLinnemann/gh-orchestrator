namespace GhOrchestrator.Core.Tests;

public class TaskRunExecutionTests
{
    private static TaskSpec ValidTask => new(
        IssueNumber: 42,
        Repository: "org/main",
        Title: "Improve logging",
        Description: "Add logging",
        Repos: new[] { "org/service-a", "org/service-b" },
        TriggerUser: "alice",
        AcceptanceCriteria: "Tests pass",
        Constraints: "none"
    );

    [Fact]
    public async Task ExecuteAsync_UsesDefaultBranchAndBuildsPullRequestPayload()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var client = new FakeGitHubClient(new Dictionary<string, string>
        {
            ["org/service-a"] = "main"
        });
        var worker = new FakeAIWorker();

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, ValidTask, plan);

        Assert.Single(result.Results);
        Assert.NotNull(result.WorkerResult);
        Assert.Equal("main", client.BranchesCreated[0].BaseBranch);
        Assert.Equal("ai/run-42-20260115083045/improve-logging", client.BranchesCreated[0].NewBranch);
        Assert.Equal("AI: Improve logging", client.PullRequests[0].Request.Title);
        Assert.Contains("Run: run-42-20260115083045", client.PullRequests[0].Request.Body);
        Assert.Contains("Repo: org/service-a", client.PullRequests[0].Request.Body);
        Assert.Equal("main", client.PullRequests[0].Request.BaseBranch);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesWhenARepoFails()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a", "org/service-b" },
            Steps: Array.Empty<TaskRunStep>());
        var client = new FakeGitHubClient(
            new Dictionary<string, string>
            {
                ["org/service-a"] = "main",
                ["org/service-b"] = "develop"
            },
            failingRepo: "org/service-a");
        var worker = new FakeAIWorker();

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, ValidTask, plan);

        Assert.Equal(2, result.Results.Count);
        Assert.Single(result.Results, item => item.Repository == "org/service-a" && !item.IsSuccess);
        Assert.Single(result.Results, item => item.Repository == "org/service-b" && item.IsSuccess);
        Assert.Single(client.PullRequests);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesAIWorkerResult()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var client = new FakeGitHubClient(new Dictionary<string, string>
        {
            ["org/service-a"] = "main"
        });
        var expectedResult = new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult("org/service-a", true, Array.Empty<AIWorkerFileChange>(), "log", null)
        });
        var worker = new FakeAIWorker(expectedResult);

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, ValidTask, plan);

        Assert.Equal(expectedResult, result.WorkerResult);
        Assert.NotNull(worker.LastRequest);
        Assert.Equal("org/service-a", worker.LastRequest!.Repositories[0]);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        private readonly IReadOnlyDictionary<string, string> _defaultBranches;
        private readonly string? _failingRepo;

        public FakeGitHubClient(IReadOnlyDictionary<string, string> defaultBranches, string? failingRepo = null)
        {
            _defaultBranches = defaultBranches;
            _failingRepo = failingRepo;
        }

        public List<(string Repository, string NewBranch, string BaseBranch)> BranchesCreated { get; } = new();

        public List<(string Repository, PullRequestRequest Request)> PullRequests { get; } = new();

        public Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult<GitHubIssue?>(null);

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

        public Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default)
        {
            if (!_defaultBranches.TryGetValue(repository, out var branch))
                throw new InvalidOperationException("Default branch missing");

            return Task.FromResult(branch);
        }

        public Task CreateBranch(
            string repository,
            string newBranch,
            string baseBranch,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(repository, _failingRepo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Branch creation failed");

            BranchesCreated.Add((repository, newBranch, baseBranch));
            return Task.CompletedTask;
        }

        public Task<PullRequestLink> CreatePullRequest(
            string repository,
            PullRequestRequest request,
            CancellationToken cancellationToken = default)
        {
            PullRequests.Add((repository, request));
            return Task.FromResult(new PullRequestLink(repository, $"https://example.com/{repository}/pulls/1"));
        }
    }

    private sealed class FakeAIWorker : IAIWorker
    {
        private readonly AIWorkerResult _result;

        public FakeAIWorker(AIWorkerResult? result = null)
        {
            _result = result ?? new AIWorkerResult(Array.Empty<AIWorkerRepoResult>());
        }

        public AIWorkerRequest? LastRequest { get; private set; }

        public Task<AIWorkerResult> ExecuteAsync(AIWorkerRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }
}
