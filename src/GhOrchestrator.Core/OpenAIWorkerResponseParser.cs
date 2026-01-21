using System.Text.Json;

namespace GhOrchestrator.Core;

public static class OpenAIWorkerResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AIWorkerResult Parse(string response, IReadOnlyList<string> expectedRepositories)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new FormatException("AI response is empty.");
        if (expectedRepositories is null)
            throw new ArgumentNullException(nameof(expectedRepositories));

        var payload = JsonSerializer.Deserialize<ResponsePayload>(response, JsonOptions)
            ?? throw new FormatException("AI response could not be parsed.");

        var repoPayloads = payload.RepoResults ?? Array.Empty<RepoPayload>();
        var byRepository = new Dictionary<string, RepoPayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var repoPayload in repoPayloads)
        {
            if (string.IsNullOrWhiteSpace(repoPayload.Repository))
                continue;

            byRepository[repoPayload.Repository] = repoPayload;
        }

        var repoResults = new List<AIWorkerRepoResult>(expectedRepositories.Count);
        foreach (var repository in expectedRepositories)
        {
            if (!byRepository.TryGetValue(repository, out var repoPayload))
            {
                repoResults.Add(new AIWorkerRepoResult(
                    repository,
                    false,
                    Array.Empty<AIWorkerFileChange>(),
                    string.Empty,
                    "No response returned for repository."));
                continue;
            }

            var changes = new List<AIWorkerFileChange>();
            if (repoPayload.Changes is not null)
            {
                foreach (var change in repoPayload.Changes)
                {
                    if (string.IsNullOrWhiteSpace(change.Path))
                        continue;

                    changes.Add(new AIWorkerFileChange(
                        change.Path,
                        ParseChangeType(change.ChangeType),
                        change.Content ?? string.Empty));
                }
            }

            repoResults.Add(new AIWorkerRepoResult(
                repository,
                true,
                changes,
                repoPayload.Summary ?? string.Empty,
                null));
        }

        return new AIWorkerResult(repoResults);
    }

    private static AIWorkerChangeType ParseChangeType(string? changeType)
    {
        if (string.Equals(changeType, "create", StringComparison.OrdinalIgnoreCase))
            return AIWorkerChangeType.Create;
        if (string.Equals(changeType, "modify", StringComparison.OrdinalIgnoreCase))
            return AIWorkerChangeType.Modify;
        if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
            return AIWorkerChangeType.Delete;

        throw new FormatException($"Unsupported change type: {changeType}");
    }

    private sealed record ResponsePayload(IReadOnlyList<RepoPayload>? RepoResults);

    private sealed record RepoPayload(
        string? Repository,
        IReadOnlyList<ChangePayload>? Changes,
        string? Summary);

    private sealed record ChangePayload(
        string? Path,
        string? ChangeType,
        string? Content);
}
