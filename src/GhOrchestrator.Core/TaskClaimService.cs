namespace GhOrchestrator.Core;

public sealed class TaskClaimService
{
    public async Task<TaskClaimResult> ClaimAsync(
        IGitHubClient gitHubClient,
        string repository,
        string projectId,
        int issueNumber,
        string runId,
        CancellationToken cancellationToken = default)
    {
        if (gitHubClient is null)
            throw new ArgumentNullException(nameof(gitHubClient));

        ProjectTaskStateSnapshot snapshot;
        try
        {
            snapshot = await gitHubClient.GetProjectTaskState(repository, projectId, issueNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            return TaskClaimResult.Failure($"Failed to read project task state: {ex.Message}");
        }

        if (snapshot.MissingFields.Count > 0)
        {
            var missingFields = string.Join(", ", snapshot.MissingFields);
            return TaskClaimResult.Failure($"Project is missing required fields: {missingFields}.");
        }

        var plan = TaskClaimPlanner.Plan(snapshot.State, runId);
        if (!plan.IsValid || plan.IsAlreadyClaimed)
            return plan;

        if (plan.Updates.Count == 0)
            return plan;

        try
        {
            await gitHubClient.UpdateProjectFields(repository, projectId, issueNumber, plan.Updates, cancellationToken);
        }
        catch (Exception)
        {
            return TaskClaimResult.Failure("Failed to update project fields.");
        }

        return plan;
    }
}
