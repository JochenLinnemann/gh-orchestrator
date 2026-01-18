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
            var request = BuildPullRequestRequest(task, plan, repo, branchName, baseBranch);

            plans.Add(new RepoPullRequestPlan(repo, branchName, request));
        }

        return plans;
    }

    public static async Task<TaskRunExecutionResult> ExecuteAsync(
        IGitHubClient gitHubClient,
        TaskSpec task,
        TaskRunPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));
        if (task is null)
            throw new ArgumentNullException(nameof(task));
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));

        var results = new List<RepoExecutionResult>(plan.Repos.Count);
        var shortSlug = TaskSlugFormatter.Format(task.Title, task.Description);

        foreach (var repo in plan.Repos)
        {
            var branchName = BranchNameFormatter.Format(plan.RunId, shortSlug);
            var baseBranch = string.Empty;

            try
            {
                baseBranch = await gitHubClient.GetDefaultBranch(repo, cancellationToken);
                var request = BuildPullRequestRequest(task, plan, repo, branchName, baseBranch);

                await gitHubClient.CreateBranch(repo, branchName, baseBranch, cancellationToken);
                var pullRequest = await gitHubClient.CreatePullRequest(repo, request, cancellationToken);

                results.Add(RepoExecutionResult.Success(repo, branchName, baseBranch, pullRequest));
            }
            catch (Exception ex)
            {
                results.Add(RepoExecutionResult.Failure(repo, branchName, baseBranch, ex.Message));
            }
        }

        return new TaskRunExecutionResult(results);
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
