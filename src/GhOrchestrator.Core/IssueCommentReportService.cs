namespace GhOrchestrator.Core;

public sealed class IssueCommentReportService
{
    public async Task PostReportAsync(
        IGitHubClient gitHubClient,
        TaskSpec task,
        string summary,
        string testInstructions,
        IReadOnlyList<RepoExecutionResult> executionResults,
        IReadOnlyList<string> riskNotes,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));
        if (task is null)
            throw new ArgumentNullException(nameof(task));
        if (executionResults is null)
            throw new ArgumentNullException(nameof(executionResults));
        if (riskNotes is null)
            throw new ArgumentNullException(nameof(riskNotes));

        var body = IssueCommentReportFormatter.Format(
            summary,
            testInstructions,
            executionResults,
            riskNotes);

        await gitHubClient.AddIssueComment(task.Repository, task.IssueNumber, body, cancellationToken);
    }
}
