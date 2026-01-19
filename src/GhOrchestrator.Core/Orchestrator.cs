namespace GhOrchestrator.Core;

/// <summary>
/// Orchestrates AI execution across repositories.
/// Stateless: all state lives in GitHub.
/// GitHub API calls will be injected later.
/// </summary>
public class Orchestrator
{
    /// <summary>
    /// Process an issue comment event.
    /// Validates /ai start command and task quality gates.
    /// This is a pure function; no GitHub calls are made.
    /// </summary>
    /// <param name="issueNumber">GitHub issue number.</param>
    /// <param name="repository">Repository in format "owner/repo".</param>
    /// <param name="commentText">Raw comment text.</param>
    /// <param name="issueBody">Full issue body.</param>
    /// <param name="triggerUser">GitHub user who posted the comment.</param>
    /// <returns>Validation result.</returns>
    public TaskValidationResult ProcessIssueComment(
        int issueNumber,
        string repository,
        string commentText,
        string issueTitle,
        string issueBody,
        IssueContext issueContext,
        string? triggerUser = null)
    {
        // Check that /ai start command exists (but allow bare /ai start without description)
        if (!commentText.Contains("/ai start", StringComparison.OrdinalIgnoreCase))
            return TaskValidationResult.FromTaskQualityGateFailure(
                ValidationResult.Failure("Comment does not contain /ai start command"));

        // Parse /ai start command description (may be null for bare /ai start)
        var description = CommandParser.ParseAiStartCommand(commentText) ?? string.Empty;

        // Parse metadata from issue body
        var repos = CommandParser.ParseRepositories(issueBody);
        var acceptanceCriteria = CommandParser.ParseAcceptanceCriteria(issueBody);
        var constraints = CommandParser.ParseConstraints(issueBody);

        // Create task specification with fallback to acceptance criteria for description
        var finalDescription = !string.IsNullOrWhiteSpace(description) ? description : (acceptanceCriteria ?? string.Empty);
        var task = new TaskSpec(issueNumber, repository, issueTitle, finalDescription, repos, triggerUser, acceptanceCriteria, constraints);

        // Validate task
        var taskQualityGateResult = TaskQualityGate.Validate(task);
        if (!taskQualityGateResult.IsValid)
            return TaskValidationResult.FromTaskQualityGateFailure(taskQualityGateResult);

        var preflightResult = RunPreflight.Validate(task, issueContext);

        return TaskValidationResult.FromPreflight(taskQualityGateResult, preflightResult);
    }

    /// <summary>
    /// Process an issue comment event using GitHub context from a client boundary.
    /// </summary>
    public async Task<TaskValidationResult> ProcessIssueCommentAsync(
        IGitHubClient gitHubClient,
        IssueCommentEvent issueCommentEvent,
        CancellationToken cancellationToken = default)
    {
        var issue = await gitHubClient.GetIssue(
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            cancellationToken);

        if (issue is null)
        {
            return TaskValidationResult.FromPreflight(
                ValidationResult.Success(),
                PreflightValidationResult.Failure(
                    PreflightFailureReason.IssueNotFound,
                    "Issue does not exist or is inaccessible"));
        }

        var issueContext = new IssueContext(
            true,
            issue.IsOpen,
            issue.Url);

        return ProcessIssueComment(
            issueCommentEvent.IssueNumber,
            issueCommentEvent.Repository,
            issueCommentEvent.CommentBody,
            issue.Title,
            issue.Body ?? string.Empty,
            issueContext,
            issueCommentEvent.CommentAuthor);
    }

    /// <summary>
    /// Execute a validated task.
    /// TODO: Invoke worker
    /// TODO: GitHub API calls (create branches, open PRs)
    /// </summary>
    public void ExecuteTask(TaskSpec task)
    {
        throw new NotImplementedException("Worker invocation not yet implemented");
    }

    /// <summary>
    /// Report execution result back to GitHub.
    /// TODO: Post comment on Issue
    /// TODO: Update Project board
    /// </summary>
    public void ReportResult(TaskSpec task, bool success, string message)
    {
        throw new NotImplementedException("GitHub reporting not yet implemented");
    }

    /// <summary>
    /// Orchestrate the complete task execution flow: validate, claim, plan, execute.
    /// </summary>
    public async Task<OrchestratorResult> ProcessTaskAsync(
        IGitHubClient gitHubClient,
        IssueCommentEvent issueCommentEvent,
        string projectId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate the task
        var validationResult = await ProcessIssueCommentAsync(
            gitHubClient,
            issueCommentEvent,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return OrchestratorResult.Failure(runId, validationResult.ErrorMessage ?? "Validation failed");
        }

        // 2. Parse task spec from validated issue
        var issue = await gitHubClient.GetIssue(
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            cancellationToken);

        if (issue is null)
        {
            return OrchestratorResult.Failure(runId, "Issue not found");
        }

        var commandDescription = CommandParser.ParseAiStartCommand(issueCommentEvent.CommentBody);
        var acceptanceCriteria = CommandParser.ParseAcceptanceCriteria(issue.Body ?? string.Empty);
        
        // Use command description if provided, otherwise use acceptance criteria
        var description = !string.IsNullOrWhiteSpace(commandDescription) 
            ? commandDescription 
            : acceptanceCriteria;

        if (string.IsNullOrWhiteSpace(description))
        {
            return OrchestratorResult.Failure(runId, "Task description missing: provide text after /ai start or set Acceptance Criteria in issue");
        }

        var repos = CommandParser.ParseRepositories(issue.Body ?? string.Empty);
        var constraints = CommandParser.ParseConstraints(issue.Body ?? string.Empty);
        var task = new TaskSpec(
            issueCommentEvent.IssueNumber,
            issueCommentEvent.Repository,
            issue.Title,
            description,
            repos,
            issueCommentEvent.CommentAuthor,
            acceptanceCriteria,
            constraints);

        // 3. Claim the task
        var taskClaimService = new TaskClaimService();
        var claimResult = await taskClaimService.ClaimAsync(
            gitHubClient,
            issueCommentEvent.Repository,
            projectId,
            issueCommentEvent.IssueNumber,
            runId,
            cancellationToken);

        if (!claimResult.IsValid)
        {
            return OrchestratorResult.Failure(runId, claimResult.ErrorMessage ?? "Claim failed");
        }

        if (claimResult.IsAlreadyClaimed)
        {
            return OrchestratorResult.AlreadyClaimedResult(runId);
        }

        // 4. Plan the task execution
        var planResult = TaskRunPlanner.Plan(task, DateTimeOffset.UtcNow);
        if (!planResult.IsValid || planResult.Plan is null)
        {
            return OrchestratorResult.Failure(runId, planResult.ErrorMessage ?? "Planning failed");
        }

        // 5. Execute the task (create branches and PRs)
        var executionResult = await TaskRunExecutor.ExecuteAsync(
            gitHubClient,
            task,
            planResult.Plan,
            cancellationToken);

        return OrchestratorResult.Success(runId, executionResult);
    }
}
