namespace GhOrchestrator.Core.Tests;

public class TaskRunExecutorTests
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
    public void BuildPullRequestPlans_UsesBranchNamingConvention()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a" },
            Steps: Array.Empty<TaskRunStep>());
        var baseBranches = new Dictionary<string, string>
        {
            ["org/service-a"] = "main",
        };

        var plans = TaskRunExecutor.BuildPullRequestPlans(ValidTask, plan, baseBranches);

        Assert.Single(plans);
        Assert.Equal("ai/run-42-20260115083045/improve-logging", plans[0].BranchName);
        Assert.Equal("ai/run-42-20260115083045/improve-logging", plans[0].PullRequest.HeadBranch);
    }

    [Fact]
    public void BuildPullRequestPlans_BuildsOnePullRequestPerRepo()
    {
        var plan = new TaskRunPlan(
            RunId: "run-42-20260115083045",
            Repos: new[] { "org/service-a", "org/service-b" },
            Steps: Array.Empty<TaskRunStep>());
        var baseBranches = new Dictionary<string, string>
        {
            ["org/service-a"] = "main",
            ["org/service-b"] = "master",
        };

        var plans = TaskRunExecutor.BuildPullRequestPlans(ValidTask, plan, baseBranches);

        Assert.Equal(2, plans.Count);
        Assert.Equal("org/service-a", plans[0].Repository);
        Assert.Equal("org/service-b", plans[1].Repository);
        Assert.Equal("main", plans[0].PullRequest.BaseBranch);
        Assert.Equal("master", plans[1].PullRequest.BaseBranch);
        Assert.Equal(plans[0].BranchName, plans[1].BranchName);
    }
}
