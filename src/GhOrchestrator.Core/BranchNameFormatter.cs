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
    /// <param name="shortSlug">Short slug describing the task.</param>
    /// <returns>Branch name string.</returns>
    public static string Format(string runId, string shortSlug)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID must be provided", nameof(runId));

        if (string.IsNullOrWhiteSpace(shortSlug))
            throw new ArgumentException("Short slug must be provided", nameof(shortSlug));

        return $"ai/{runId}/{shortSlug}";
    }
}
