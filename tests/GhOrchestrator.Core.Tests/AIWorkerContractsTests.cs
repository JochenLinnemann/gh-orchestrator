namespace GhOrchestrator.Core.Tests;

public class AIWorkerContractsTests
{
    [Fact]
    public void AIWorkerRequest_CapturesTaskContextAndPolicies()
    {
        var task = new TaskSpec(
            IssueNumber: 42,
            Repository: "org/main",
            Title: "Update logging",
            Description: "Add structured logs",
            Repos: new[] { "org/service-a" },
            TriggerUser: "alice",
            AcceptanceCriteria: "Tests pass",
            Constraints: "No new dependencies"
        );
        var policies = new Dictionary<string, string>
        {
            ["security"] = "Follow OWASP guidance",
        };
        var constraints = new Dictionary<string, string>
        {
            ["timeout"] = "15m",
        };

        var request = new AIWorkerRequest(
            Task: task,
            Repositories: new[] { "org/service-a" },
            AcceptanceCriteria: "Tests pass",
            Constraints: "No new dependencies",
            DefinitionOfDone: "PR ready",
            Policies: policies,
            ExecutionConstraints: constraints
        );

        Assert.Same(task, request.Task);
        Assert.Equal("org/service-a", request.Repositories[0]);
        Assert.Equal("Tests pass", request.AcceptanceCriteria);
        Assert.Equal("No new dependencies", request.Constraints);
        Assert.Equal("PR ready", request.DefinitionOfDone);
        Assert.Equal("Follow OWASP guidance", request.Policies["security"]);
        Assert.Equal("15m", request.ExecutionConstraints["timeout"]);
    }

    [Fact]
    public void AIWorkerResult_TracksPerRepoOutcomes()
    {
        var repoResult = new AIWorkerRepoResult(
            Repository: "org/service-a",
            IsSuccess: true,
            FileChanges: new[]
            {
                new AIWorkerFileChange("src/Logging.cs", AIWorkerChangeType.Modify, "// content")
            },
            ExecutionLog: "Applied changes",
            FailureReason: null
        );

        var result = new AIWorkerResult(new[] { repoResult });

        Assert.Single(result.RepoResults);
        Assert.Equal("org/service-a", result.RepoResults[0].Repository);
        Assert.True(result.RepoResults[0].IsSuccess);
        Assert.Equal("src/Logging.cs", result.RepoResults[0].FileChanges[0].Path);
        Assert.Equal("Applied changes", result.RepoResults[0].ExecutionLog);
        Assert.Null(result.RepoResults[0].FailureReason);
    }
}
