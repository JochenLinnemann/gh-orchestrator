using System.Globalization;
using System.Linq;

namespace GhOrchestrator.Core;

public static class WorkerResultValidator
{
    public static WorkerResultValidationResult Validate(
        TaskRunPlan plan,
        AIWorkerResult workerResult,
        WorkerResultValidationSettings settings)
    {
        if (plan is null)
            throw new ArgumentNullException(nameof(plan));
        if (workerResult is null)
            throw new ArgumentNullException(nameof(workerResult));
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        var results = new List<WorkerResultRepoValidationResult>();
        var planRepos = new HashSet<string>(plan.Repos, StringComparer.OrdinalIgnoreCase);
        var repoResults = new Dictionary<string, AIWorkerRepoResult>(StringComparer.OrdinalIgnoreCase);
        var duplicateRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidRepos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var repoResult in workerResult.RepoResults)
        {
            if (!repoResults.TryAdd(repoResult.Repository, repoResult))
                duplicateRepos.Add(repoResult.Repository);
        }

        foreach (var duplicateRepo in duplicateRepos)
        {
            invalidRepos.Add(duplicateRepo);
            results.Add(WorkerResultRepoValidationResult.Failure(
                duplicateRepo,
                new[] { "AI worker returned duplicate results for repository." }));
        }

        foreach (var repoResult in workerResult.RepoResults)
        {
            if (!planRepos.Contains(repoResult.Repository))
            {
                invalidRepos.Add(repoResult.Repository);
                results.Add(WorkerResultRepoValidationResult.Failure(
                    repoResult.Repository,
                    new[] { "AI worker returned changes for undeclared repository." }));
            }
        }

        foreach (var repo in plan.Repos)
        {
            if (invalidRepos.Contains(repo))
                continue;

            if (!repoResults.TryGetValue(repo, out var repoResult))
            {
                results.Add(WorkerResultRepoValidationResult.Failure(
                    repo,
                    new[] { "AI worker did not return results for repository." }));
                continue;
            }

            var errors = ValidateRepoResult(repoResult, settings);
            results.Add(errors.Count == 0
                ? WorkerResultRepoValidationResult.Success(repo)
                : WorkerResultRepoValidationResult.Failure(repo, errors));
        }

        return new WorkerResultValidationResult(results);
    }

    private static List<string> ValidateRepoResult(AIWorkerRepoResult repoResult, WorkerResultValidationSettings settings)
    {
        var errors = new List<string>();

        if (!repoResult.IsSuccess)
        {
            errors.Add(repoResult.FailureReason ?? "AI worker execution failed.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(settings.CommitAuthorName) ||
            string.IsNullOrWhiteSpace(settings.CommitAuthorEmail))
        {
            errors.Add("Commit author metadata missing; AI changes must be attributable.");
        }

        if (repoResult.FileChanges is null || repoResult.FileChanges.Count == 0)
        {
            errors.Add("AI worker returned no file changes.");
            return errors;
        }

        var deleteCount = 0;
        var totalChanges = repoResult.FileChanges.Count;

        foreach (var change in repoResult.FileChanges)
        {
            if (string.IsNullOrWhiteSpace(change.Path))
                errors.Add("File change path is required.");

            if (change.ChangeType == AIWorkerChangeType.Delete)
                deleteCount++;

            if (change.ChangeType != AIWorkerChangeType.Delete &&
                change.Content.Contains('\0', StringComparison.Ordinal))
            {
                errors.Add($"File change content appears binary: {change.Path}");
            }

            if (IsSchemaChange(change.Path, settings))
                errors.Add($"Schema change detected: {change.Path}");
        }

        if (deleteCount > settings.MaxDeleteCount)
        {
            errors.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Delete changes ({0}) exceed max allowed ({1}).",
                deleteCount,
                settings.MaxDeleteCount));
        }

        if (totalChanges >= settings.MinTotalChangesForRatio)
        {
            var deleteRatio = (decimal)deleteCount / totalChanges;
            if (deleteRatio > settings.MaxDeleteRatio)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Delete change ratio ({0:P0}) exceeds max allowed ({1:P0}) across {2} changes.",
                    deleteRatio,
                    settings.MaxDeleteRatio,
                    totalChanges));
            }
        }

        return errors;
    }

    private static bool IsSchemaChange(string path, WorkerResultValidationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');

        foreach (var token in settings.SchemaPathTokens)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var extension = Path.GetExtension(normalized);
        return settings.SchemaFileExtensions.Any(ext =>
            string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase));
    }
}
