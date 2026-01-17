namespace GhOrchestrator.Core;

/// <summary>
/// Formats branch names for AI execution runs.
/// </summary>
public static class BranchNameFormatter
{
    /// <summary>
    /// Format a branch name using the Playbook convention: ai/&lt;run-id&gt;/&lt;short-slug&gt;.
    /// </summary>
    /// <param name="runId">Formatted run identifier.</param>
    /// <param name="repository">Repository in owner/repo format.</param>
    /// <returns>Branch name string.</returns>
    public static string Format(string runId, string repository)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID must be provided", nameof(runId));

        if (string.IsNullOrWhiteSpace(repository))
            throw new ArgumentException("Repository must be provided", nameof(repository));

        var shortSlug = GetShortSlug(repository);
        return $"ai/{runId}/{shortSlug}";
    }

    private static string GetShortSlug(string repository)
    {
        var parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : repository.Trim();
    }
}
