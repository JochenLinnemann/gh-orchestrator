namespace GhOrchestrator.Core;

/// <summary>
/// Builds per-repo branch and pull request payloads for a task run.
/// Stub only: no GitHub I/O.
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
            var request = new PullRequestRequest(
                Title: $"AI: {task.Description}",
                Body: $"Run {plan.RunId} for {repo}.\n\nTask: {task.Description}",
                HeadBranch: branchName,
                BaseBranch: baseBranch);

            plans.Add(new RepoPullRequestPlan(repo, branchName, request));
        }

        return plans;
    }
}
