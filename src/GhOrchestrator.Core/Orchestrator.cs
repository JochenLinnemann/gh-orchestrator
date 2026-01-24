namespace GhOrchestrator.Core;

/// <summary>
/// Orchestrates AI execution across repositories.
/// Stateless: all state lives in GitHub.
/// GitHub API calls will be injected later.
/// </summary>
public class Orchestrator
{
    private readonly IOrchestratorLogger _logger;
    private readonly IAIWorker _aiWorker;

    public Orchestrator(IOrchestratorLogger? logger = null, IAIWorker? aiWorker = null)
    {
        _logger = logger ?? NullOrchestratorLogger.Instance;
        _aiWorker = aiWorker ?? new MockAIWorker(_logger);
    }

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
    /// <returns>Validation result with TaskSpec if valid.</returns>
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

        var taskBuildResult = BuildTaskSpec(
            issueNumber,
            repository,
            issueTitle,
            issueBody,
            commentText,
            triggerUser,
            requireDescription: false);
        if (taskBuildResult.ErrorMessage is not null || taskBuildResult.Task is null)
        {
            return TaskValidationResult.FromTaskQualityGateFailure(
                ValidationResult.Failure(taskBuildResult.ErrorMessage ?? "Task description missing"));
        }

        // Validate task
        var taskQualityGateResult = TaskQualityGate.Validate(taskBuildResult.Task);
        if (!taskQualityGateResult.IsValid)
            return TaskValidationResult.FromTaskQualityGateFailure(taskQualityGateResult);

        var preflightResult = RunPreflight.Validate(taskBuildResult.Task, issueContext);

        return TaskValidationResult.FromPreflight(taskQualityGateResult, preflightResult, taskBuildResult.Task);
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
        _logger.LogInformation(
            "Orchestration started: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);

        // 1. Validate the task
        var validationResult = await ProcessIssueCommentAsync(
            gitHubClient,
            issueCommentEvent,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Validation failed: repo={Repository}, issue={IssueNumber}, runId={RunId}, error={Error}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId,
                validationResult.ErrorMessage);
            return OrchestratorResult.Failure(runId, validationResult.ErrorMessage ?? "Validation failed");
        }

        _logger.LogInformation(
            "Validation passed: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);

        // 2. Get task spec from validation result
        var task = validationResult.Task;
        if (task is null)
        {
            _logger.LogWarning(
                "Task spec not available after validation: repo={Repository}, issue={IssueNumber}, runId={RunId}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId);
            return OrchestratorResult.Failure(runId, "Task spec not available");
        }

        // Validate description is present for execution (validation allows bare /ai start for testing)
        if (string.IsNullOrWhiteSpace(task.Description))
        {
            _logger.LogWarning(
                "Task description missing: repo={Repository}, issue={IssueNumber}, runId={RunId}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId);
            return OrchestratorResult.Failure(
                runId,
                "Task description missing: provide text after /ai start or set Acceptance Criteria in issue");
        }

        // 3. Claim the task
        _logger.LogInformation(
            "Claiming task: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);
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
            _logger.LogWarning(
                "Claim failed: repo={Repository}, issue={IssueNumber}, runId={RunId}, error={Error}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId,
                claimResult.ErrorMessage);
            return OrchestratorResult.Failure(runId, claimResult.ErrorMessage ?? "Claim failed");
        }

        if (claimResult.IsAlreadyClaimed)
        {
            _logger.LogInformation(
                "Task already claimed: repo={Repository}, issue={IssueNumber}, runId={RunId}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId);
            return OrchestratorResult.AlreadyClaimedResult(runId);
        }

        // 4. Plan the task execution
        _logger.LogInformation(
            "Planning task run: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);
        var planResult = TaskRunPlanner.Plan(task, DateTimeOffset.UtcNow);
        if (!planResult.IsValid || planResult.Plan is null)
        {
            _logger.LogWarning(
                "Planning failed: repo={Repository}, issue={IssueNumber}, runId={RunId}, error={Error}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId,
                planResult.ErrorMessage);
            return OrchestratorResult.Failure(runId, planResult.ErrorMessage ?? "Planning failed");
        }

        _logger.LogInformation(
            "Planning complete: repo={Repository}, issue={IssueNumber}, runId={RunId}, repoCount={RepoCount}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId,
            planResult.Plan.Repos.Count);

        // 5. Execute the task (create branches and PRs)
        _logger.LogInformation(
            "Executing task run: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);
        var gitOperations = new GitOperations();
        var executionResult = await TaskRunExecutor.ExecuteAsync(
            gitHubClient,
            _aiWorker,
            gitOperations,
            task,
            planResult.Plan,
            cancellationToken);

        // 6. Post report comment back to the issue
        _logger.LogInformation(
            "Posting execution report: repo={Repository}, issue={IssueNumber}, runId={RunId}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId);
        var reportService = new IssueCommentReportService();
        var summary = $"Task execution completed for run `{runId}`.";
        var testInstructions = task.AcceptanceCriteria ?? "Review the PRs and verify changes meet the task requirements.";
        var riskNotes = !string.IsNullOrWhiteSpace(task.Constraints) 
            ? new[] { $"Constraints: {task.Constraints}" } 
            : Array.Empty<string>();

        try
        {
            await reportService.PostReportAsync(
                gitHubClient,
                task,
                summary,
                testInstructions,
                executionResult,
                riskNotes,
                cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                "Posting execution report canceled: repo={Repository}, issue={IssueNumber}, runId={RunId}, error={Error}",
                issueCommentEvent.Repository,
                issueCommentEvent.IssueNumber,
                runId,
                ex.Message);
        }

        _logger.LogInformation(
            "Orchestration completed: repo={Repository}, issue={IssueNumber}, runId={RunId}, resultCount={ResultCount}",
            issueCommentEvent.Repository,
            issueCommentEvent.IssueNumber,
            runId,
            executionResult.Results.Count);

        return OrchestratorResult.Success(runId, executionResult);
    }

    private static TaskSpecBuildResult BuildTaskSpec(
        int issueNumber,
        string repository,
        string issueTitle,
        string issueBody,
        string commentText,
        string? triggerUser,
        bool requireDescription)
    {
        var commandDescription = CommandParser.ParseAiStartCommand(commentText);
        var acceptanceCriteria = CommandParser.ParseAcceptanceCriteria(issueBody);
        var constraints = CommandParser.ParseConstraints(issueBody);
        var repos = CommandParser.ParseRepositories(issueBody);

        var description = !string.IsNullOrWhiteSpace(commandDescription)
            ? commandDescription
            : acceptanceCriteria;

        if (requireDescription && string.IsNullOrWhiteSpace(description))
        {
            return TaskSpecBuildResult.Failure(
                "Task description missing: provide text after /ai start or set Acceptance Criteria in issue");
        }

        var finalDescription = description ?? string.Empty;
        var task = new TaskSpec(
            issueNumber,
            repository,
            issueTitle,
            finalDescription,
            repos,
            triggerUser,
            acceptanceCriteria,
            constraints);

        return TaskSpecBuildResult.Success(task);
    }

    private sealed record TaskSpecBuildResult(TaskSpec? Task, string? ErrorMessage)
    {
        public static TaskSpecBuildResult Success(TaskSpec task) => new(task, null);

        public static TaskSpecBuildResult Failure(string errorMessage) => new(null, errorMessage);
    }
}
