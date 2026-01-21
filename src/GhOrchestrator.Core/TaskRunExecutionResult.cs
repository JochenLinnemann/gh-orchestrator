using System.Linq;

namespace GhOrchestrator.Core;

public record TaskRunExecutionResult(
    IReadOnlyList<RepoExecutionResult> Results,
    AIWorkerResult WorkerResult)
{
    public bool IsSuccessful => Results.All(result => result.IsSuccess);

    public bool IsPartialSuccess =>
        Results.Any(result => result.IsSuccess) && Results.Any(result => !result.IsSuccess);
}
