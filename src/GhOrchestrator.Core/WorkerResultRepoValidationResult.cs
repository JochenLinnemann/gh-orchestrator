namespace GhOrchestrator.Core;

public record WorkerResultRepoValidationResult(
    string Repository,
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static WorkerResultRepoValidationResult Success(string repository) =>
        new(repository, true, Array.Empty<string>());

    public static WorkerResultRepoValidationResult Failure(string repository, IReadOnlyList<string> errors) =>
        new(repository, false, errors);
}
