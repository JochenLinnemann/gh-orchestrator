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
        var worker = new FakeAIWorker(new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                new[] { new AIWorkerFileChange("README.md", AIWorkerChangeType.Modify, "content") },
                "log",
                null)
        }));
        var gitOperations = new FakeGitOperations();

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, gitOperations, ValidTask, plan);

        Assert.Single(result.Results);
        Assert.NotNull(result.WorkerResult);
        Assert.Single(gitOperations.Clones);
        Assert.Equal("main", gitOperations.Clones[0].Branch);
        Assert.Equal("ai/run-42-20260115083045/improve-logging", gitOperations.Checkouts[0].BranchName);
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
            });
        var worker = new FakeAIWorker(new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult("org/service-a", true, new[] { new AIWorkerFileChange("README.md", AIWorkerChangeType.Modify, "content") }, "log", null),
            new AIWorkerRepoResult("org/service-b", true, new[] { new AIWorkerFileChange("README.md", AIWorkerChangeType.Modify, "content") }, "log", null),
        }));
        var gitOperations = new FakeGitOperations { FailCloneForRepo = "org/service-a" };

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, gitOperations, ValidTask, plan);

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
        var gitOperations = new FakeGitOperations();

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, gitOperations, ValidTask, plan);

        Assert.Equal(expectedResult, result.WorkerResult);
        Assert.NotNull(worker.LastRequest);
        Assert.Equal("org/service-a", worker.LastRequest!.Repositories[0]);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsReposThatFailValidation()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var client = new FakeGitHubClient(new Dictionary<string, string>
        {
            ["org/service-a"] = "main"
        });
        var worker = new FakeAIWorker(new AIWorkerResult(new[]
        {
            new AIWorkerRepoResult(
                "org/service-a",
                true,
                new[] { new AIWorkerFileChange("db/schema.sql", AIWorkerChangeType.Modify, "alter table") },
                "log",
                null)
        }));
        var gitOperations = new FakeGitOperations();

        var result = await TaskRunExecutor.ExecuteAsync(client, worker, gitOperations, ValidTask, plan);

        Assert.Single(result.Results);
        Assert.False(result.Results[0].IsSuccess);
        Assert.Empty(gitOperations.Clones);
        Assert.Empty(client.PullRequests);
    }

    private sealed class FakeGitHubClient : IGitHubClient
    {
        private readonly IReadOnlyDictionary<string, string> _defaultBranches;

        public FakeGitHubClient(IReadOnlyDictionary<string, string> defaultBranches)
        {
            _defaultBranches = defaultBranches;
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

    private sealed class FakeGitOperations : IGitOperations
    {
        public List<(string RepositoryUrl, string Destination, string? Branch, string? AccessToken)> Clones { get; } = new();

        public List<(string RepositoryPath, string BranchName, string BaseBranch)> Checkouts { get; } = new();

        public List<(string RepositoryPath, IReadOnlyList<AIWorkerFileChange> Changes)> AppliedChanges { get; } = new();

        public List<(string RepositoryPath, string RunId, string AuthorName, string AuthorEmail)> Commits { get; } = new();

        public List<(string RepositoryPath, string BranchName)> Pushes { get; } = new();

        public string? FailCloneForRepo { get; set; }

        public Task CloneRepositoryAsync(
            string repositoryUrl,
            string destinationPath,
            string? branch = null,
            string? accessToken = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(FailCloneForRepo) &&
                repositoryUrl.Contains(FailCloneForRepo, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Clone failed");
            }

            Clones.Add((repositoryUrl, destinationPath, branch, accessToken));
            return Task.CompletedTask;
        }

        public Task FetchAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CheckoutBranchAsync(
            string repositoryPath,
            string branchName,
            string baseBranch,
            CancellationToken cancellationToken = default)
        {
            Checkouts.Add((repositoryPath, branchName, baseBranch));
            return Task.CompletedTask;
        }

        public Task ApplyFileChangesAsync(
            string repositoryPath,
            IEnumerable<AIWorkerFileChange> changes,
            CancellationToken cancellationToken = default)
        {
            AppliedChanges.Add((repositoryPath, changes.ToArray()));
            return Task.CompletedTask;
        }

        public Task CommitAsync(
            string repositoryPath,
            string runId,
            string authorName,
            string authorEmail,
            CancellationToken cancellationToken = default)
        {
            Commits.Add((repositoryPath, runId, authorName, authorEmail));
            return Task.CompletedTask;
        }

        public Task PushAsync(
            string repositoryPath,
            string branchName,
            CancellationToken cancellationToken = default)
        {
            Pushes.Add((repositoryPath, branchName));
            return Task.CompletedTask;
        }
    }
}
