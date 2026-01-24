namespace GhOrchestrator.Core;

public sealed class IssueCommentReportService
{
    public async Task PostReportAsync(
        IGitHubClient gitHubClient,
        TaskSpec task,
        string summary,
        string testInstructions,
        TaskRunExecutionResult executionResult,
        IReadOnlyList<string> riskNotes,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));
        if (task is null)
            throw new ArgumentNullException(nameof(task));
        if (executionResult is null)
            throw new ArgumentNullException(nameof(executionResult));
        if (riskNotes is null)
            throw new ArgumentNullException(nameof(riskNotes));

        var body = IssueCommentReportFormatter.Format(
            summary,
            testInstructions,
            executionResult.Results,
            executionResult,
            riskNotes);

        await gitHubClient.AddIssueComment(task.Repository, task.IssueNumber, body, cancellationToken);
    }
}
