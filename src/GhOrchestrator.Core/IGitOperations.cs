namespace GhOrchestrator.Core;

public interface IGitOperations
{
    Task CloneRepositoryAsync(
        string repositoryUrl,
        string destinationPath,
        string? branch = null,
        string? accessToken = null,
        CancellationToken cancellationToken = default);

    Task FetchAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default);

    Task CheckoutBranchAsync(
        string repositoryPath,
        string branchName,
        string baseBranch,
        CancellationToken cancellationToken = default);

    Task ApplyFileChangesAsync(
        string repositoryPath,
        IEnumerable<AIWorkerFileChange> changes,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        string repositoryPath,
        string runId,
        string authorName,
        string authorEmail,
        CancellationToken cancellationToken = default);

    Task PushAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default);
}
