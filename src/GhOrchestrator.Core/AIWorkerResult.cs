namespace GhOrchestrator.Core;

public record AIWorkerResult(
    IReadOnlyList<AIWorkerRepoResult> RepoResults,
    AIWorkerExecutionMetadata? Metadata = null);
