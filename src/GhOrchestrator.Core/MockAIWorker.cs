namespace GhOrchestrator.Core;

/// <summary>
/// Stub AI worker for wiring validation. Logs invocation and returns empty results.
/// </summary>
public sealed class MockAIWorker : IAIWorker
{
    private readonly IOrchestratorLogger _logger;

    public MockAIWorker(IOrchestratorLogger? logger = null)
    {
        _logger = logger ?? NullOrchestratorLogger.Instance;
    }

    public Task<AIWorkerResult> ExecuteAsync(AIWorkerRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        _logger.LogInformation(
            "Mock AI worker invoked: repoCount={RepoCount}, issue={IssueNumber}",
            request.Repositories.Count,
            request.Task.IssueNumber);

        var metadata = new AIWorkerExecutionMetadata(
            "mock",
            TimeSpan.Zero,
            null,
            null,
            "low",
            new[] { "Mock worker returned no changes." });

        return Task.FromResult(new AIWorkerResult(Array.Empty<AIWorkerRepoResult>(), metadata));
    }
}
