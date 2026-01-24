namespace GhOrchestrator.Core;

/// <summary>
/// Builds per-repo branch and pull request payloads for a task run.
/// Includes helper to execute per-repo branch + PR creation using a GitHub client.
/// </summary>
public static class TaskRunExecutor
{
    private const string CommitAuthorName = "gh-orchestrator[bot]";
    private const string CommitAuthorEmail = "gh-orchestrator@users.noreply.github.com";

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
        IGitOperations gitOperations,
        TaskSpec task,
        TaskRunPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));
        if (aiWorker is null)
            throw new ArgumentNullException(nameof(aiWorker));
        if (gitOperations is null)
            throw new ArgumentNullException(nameof(gitOperations));
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
        var unavailableRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in plan.Repos)
        {
            try
            {
                baseBranches[repo] = await gitHubClient.GetDefaultBranch(repo, cancellationToken);
            }
            catch (Exception)
            {
                unavailableRepos.Add(repo);
            }
        }

        var availableRepos = plan.Repos.Where(repo => !unavailableRepos.Contains(repo)).ToArray();
        var prPlans = availableRepos.Length > 0
            ? BuildPullRequestPlans(task, plan with { Repos = availableRepos }, baseBranches)
            : Array.Empty<RepoPullRequestPlan>();

        // Execute branch and PR creation for each plan
        var executionResults = new List<RepoExecutionResult>(plan.Repos.Count);
        foreach (var repo in unavailableRepos)
        {
            executionResults.Add(RepoExecutionResult.Failure(
                repo,
                string.Empty,
                string.Empty,
                "Default branch lookup failed"));
        }

        foreach (var prPlan in prPlans)
        {
            var baseBranch = baseBranches.GetValueOrDefault(prPlan.Repository, string.Empty);
            
            try
            {
                var workerRepoResult = workerResult.RepoResults.FirstOrDefault(result =>
                    string.Equals(result.Repository, prPlan.Repository, StringComparison.OrdinalIgnoreCase));

                if (workerRepoResult is null)
                    throw new InvalidOperationException("AI worker result missing for repository.");

                if (!workerRepoResult.IsSuccess)
                    throw new InvalidOperationException(workerRepoResult.FailureReason ?? "AI worker execution failed.");

                if (workerRepoResult.FileChanges.Count == 0)
                    throw new InvalidOperationException("AI worker returned no file changes.");

                var cloneUrl = await gitHubClient.GetRepositoryCloneUrl(prPlan.Repository, cancellationToken);
                var accessToken = await gitHubClient.GetRepositoryAccessToken(prPlan.Repository, cancellationToken);
                var workspacePath = CreateWorkspacePath(plan.RunId, prPlan.Repository);

                try
                {
                    await gitOperations.CloneRepositoryAsync(cloneUrl, workspacePath, baseBranch, accessToken, cancellationToken);
                    await gitOperations.CheckoutBranchAsync(workspacePath, prPlan.BranchName, baseBranch, cancellationToken);
                    await gitOperations.ApplyFileChangesAsync(workspacePath, workerRepoResult.FileChanges, cancellationToken);
                    await gitOperations.CommitAsync(workspacePath, plan.RunId, CommitAuthorName, CommitAuthorEmail, cancellationToken);
                    await gitOperations.PushAsync(workspacePath, prPlan.BranchName, cancellationToken);
                }
                finally
                {
                    TryDeleteWorkspace(workspacePath);
                }

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

    private static string CreateWorkspacePath(string runId, string repository)
    {
        var sanitizedRepo = repository.Replace('/', '-').Replace('\\', '-');
        return Path.Combine(Path.GetTempPath(), $"gh-orchestrator-{runId}-{sanitizedRepo}");
    }

    private static void TryDeleteWorkspace(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        try
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
