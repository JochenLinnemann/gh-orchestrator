using System.Text.RegularExpressions;

namespace GhOrchestrator.Core;

/// <summary>
/// Pure functions for parsing GitHub Issue comments and metadata.
/// </summary>
public static class CommandParser
{
    private static readonly Regex AiStartPattern = new(@"^/ai\s+start\s*\n?(.*)", 
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Parse /ai start command from an issue comment.
    /// </summary>
    /// <param name="commentText">The raw comment text.</param>
    /// <returns>Task description if command found, null otherwise.</returns>
    public static string? ParseAiStartCommand(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return null;

        var match = AiStartPattern.Match(commentText.Trim());
        if (!match.Success)
            return null;

        return match.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Parse repository list from Issue body.
    /// Expects a section like:
    /// ## Repositories
    /// - owner/repo1
    /// - owner/repo2
    /// </summary>
    /// <param name="issueBody">The full issue body.</param>
    /// <returns>List of repositories in format "owner/repo".</returns>
    public static IReadOnlyList<string> ParseRepositories(string issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return Array.Empty<string>();

        var repos = new List<string>();
        var inReposSection = false;
        var repoPattern = new Regex(@"^\s*-\s+([a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+)");

        foreach (var line in issueBody.Split('\n'))
        {
            if (line.Contains("## Repositories"))
            {
                inReposSection = true;
                continue;
            }

            if (inReposSection)
            {
                // Stop at next section header
                if (line.StartsWith("##") && !line.Contains("## Repositories"))
                    break;

                // Parse list item
                var match = repoPattern.Match(line);
                if (match.Success)
                {
                    repos.Add(match.Groups[1].Value);
                }
            }
        }

        return repos.AsReadOnly();
    }

    /// <summary>
    /// Parse acceptance criteria from Issue body.
    /// Expects a section like:
    /// ## Acceptance Criteria
    /// - First criterion
    /// - Second criterion
    /// 
    /// Or a single-line format:
    /// Acceptance Criteria: criterion text
    /// </summary>
    /// <param name="issueBody">The full issue body.</param>
    /// <returns>Acceptance criteria text, or null if not found.</returns>
    public static string? ParseAcceptanceCriteria(string issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return null;

        var criteria = new List<string>();
        var inCriteriaSection = false;

        foreach (var line in issueBody.Split('\n'))
        {
            // Check for section header
            if (line.Contains("## Acceptance Criteria"))
            {
                inCriteriaSection = true;
                continue;
            }

            // Check for single-line format
            if (line.Trim().StartsWith("Acceptance Criteria:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring(line.IndexOf(':') + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (inCriteriaSection)
            {
                // Stop at next section header
                if (line.StartsWith("##") && !line.Contains("## Acceptance Criteria"))
                    break;

                // Collect non-empty lines
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    criteria.Add(trimmed);
                }
            }
        }

        return criteria.Count > 0 ? string.Join("\n", criteria) : null;
    }

    /// <summary>
    /// Parse constraints from Issue body.
    /// Expects a section like:
    /// ## Constraints
    /// - No schema changes
    /// - Touch only /src
    /// 
    /// Or a single-line format:
    /// Constraints: constraint text
    /// Constraints: none
    /// </summary>
    /// <param name="issueBody">The full issue body.</param>
    /// <returns>Constraints text, or null if not found.</returns>
    public static string? ParseConstraints(string issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return null;

        var constraints = new List<string>();
        var inConstraintsSection = false;

        foreach (var line in issueBody.Split('\n'))
        {
            // Check for section header
            if (line.Contains("## Constraints"))
            {
                inConstraintsSection = true;
                continue;
            }

            // Check for single-line format
            if (line.Trim().StartsWith("Constraints:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring(line.IndexOf(':') + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (inConstraintsSection)
            {
                // Stop at next section header
                if (line.StartsWith("##") && !line.Contains("## Constraints"))
                    break;

                // Collect non-empty lines
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    constraints.Add(trimmed);
                }
            }
        }

        return constraints.Count > 0 ? string.Join("\n", constraints) : null;
    }
}
