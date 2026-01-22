namespace GhOrchestrator.Core;

/// <summary>
/// Builds per-repo branch and pull request payloads for a task run.
/// Includes helper to execute per-repo branch + PR creation using a GitHub client.
/// </summary>
public static class TaskRunExecutor
{
    /// <summary>
    /// Build pull request plans for each repo in the run plan.
    /// </summary>
    /// <param name="task">Task metadata.</param>
    /// <param name="plan">Planned run with run ID and repos.</param>
    /// <param name="baseBranches">Per-repo base branches keyed by repository.</param>
    /// <returns>Per-repo pull request plans.</returns>
    public static IReadOnlyList<RepoPullRequestPlan> BuildPullRequestPlans(
        TaskSpec task,
        TaskRunPlan plan,
        IReadOnlyDictionary<string, string> baseBranches)
    {
        if (task is null)
            throw new ArgumentNullException(nameof(task));
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));
        if (baseBranches is null)
            throw new ArgumentNullException(nameof(baseBranches));

        var plans = new List<RepoPullRequestPlan>(plan.Repos.Count);
        var shortSlug = TaskSlugFormatter.Format(task.Title, task.Description);

        foreach (var repo in plan.Repos)
        {
            if (!baseBranches.TryGetValue(repo, out var baseBranch) || string.IsNullOrWhiteSpace(baseBranch))
                throw new ArgumentException($"Base branch is required for {repo}", nameof(baseBranches));

            var branchName = BranchNameFormatter.Format(plan.RunId, shortSlug);
            var pullRequest = BuildPullRequestRequest(task, plan, repo, branchName, baseBranch);

            plans.Add(new RepoPullRequestPlan(repo, branchName, pullRequest));
        }

        return plans;
    }

    public static async Task<TaskRunExecutionResult> ExecuteAsync(
        IGitHubClient gitHubClient,
        IAIWorker aiWorker,
        TaskSpec task,
        TaskRunPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));
        if (aiWorker is null)
            throw new ArgumentNullException(nameof(aiWorker));
        if (task is null)
            throw new ArgumentNullException(nameof(task));
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));

        var policies = AIPromptPolicyProvider.Default;
        var workerRequest = new AIWorkerRequest(
            task,
            plan.Repos,
            policies);
        var workerResult = await aiWorker.ExecuteAsync(workerRequest, cancellationToken);

        // Fetch base branches and build PR plans
        var baseBranches = new Dictionary<string, string>(plan.Repos.Count);
        foreach (var repo in plan.Repos)
        {
            try
            {
                baseBranches[repo] = await gitHubClient.GetDefaultBranch(repo, cancellationToken);
            }
            catch (Exception)
            {
                baseBranches[repo] = string.Empty;
            }
        }

        var prPlans = BuildPullRequestPlans(task, plan, baseBranches);

        // Execute branch and PR creation for each plan
        var executionResults = new List<RepoExecutionResult>(prPlans.Count);
        foreach (var prPlan in prPlans)
        {
            var baseBranch = baseBranches.GetValueOrDefault(prPlan.Repository, string.Empty);
            
            try
            {
                await gitHubClient.CreateBranch(prPlan.Repository, prPlan.BranchName, baseBranch, cancellationToken);
                var pullRequest = await gitHubClient.CreatePullRequest(prPlan.Repository, prPlan.PullRequest, cancellationToken);

                executionResults.Add(RepoExecutionResult.Success(prPlan.Repository, prPlan.BranchName, baseBranch, pullRequest));
            }
            catch (Exception ex)
            {
                executionResults.Add(RepoExecutionResult.Failure(prPlan.Repository, prPlan.BranchName, baseBranch, ex.Message));
            }
        }

        return new TaskRunExecutionResult(executionResults, workerResult);
    }

    private static PullRequestRequest BuildPullRequestRequest(
        TaskSpec task,
        TaskRunPlan plan,
        string repository,
        string branchName,
        string baseBranch)
    {
        var title = $"AI: {task.Title}";
        var body = $"Run: {plan.RunId}\nRepo: {repository}\nIssue: {task.Repository}#{task.IssueNumber}\n\nTask: {task.Description}";
        return new PullRequestRequest(title, body, branchName, baseBranch);
    }
}
