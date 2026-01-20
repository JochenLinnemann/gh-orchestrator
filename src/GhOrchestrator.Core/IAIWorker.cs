namespace GhOrchestrator.Core;

/// <summary>
/// Defines the boundary for AI execution workers.
/// </summary>
public interface IAIWorker
{
    Task<AIWorkerResult> ExecuteAsync(AIWorkerRequest request, CancellationToken cancellationToken = default);
}
