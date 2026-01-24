using System.Linq;

namespace GhOrchestrator.Core;

public record WorkerResultValidationResult(IReadOnlyList<WorkerResultRepoValidationResult> RepoResults)
{
    public bool IsValid => RepoResults.All(result => result.IsValid);
}
