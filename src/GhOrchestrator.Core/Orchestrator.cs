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
    public ValidationResult ProcessIssueComment(
        int issueNumber,
        string repository,
        string commentText,
        string issueBody,
        string? triggerUser = null)
    {
        // Parse /ai start command
        var description = CommandParser.ParseAiStartCommand(commentText);
        if (description is null)
            return ValidationResult.Failure("Comment does not contain /ai start command");

        // Parse metadata from issue body
        var repos = CommandParser.ParseRepositories(issueBody);
        var acceptanceCriteria = CommandParser.ParseAcceptanceCriteria(issueBody);
        var constraints = CommandParser.ParseConstraints(issueBody);

        // Create task specification
        var task = new TaskSpec(issueNumber, repository, description, repos, triggerUser, acceptanceCriteria, constraints);

        // Validate task
        return TaskQualityGate.Validate(task);
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
}
