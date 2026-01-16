using System.Text.RegularExpressions;

namespace GhOrchestrator.Core;

/// <summary>
/// Pure functions for parsing GitHub Issue comments and metadata.
/// </summary>
public static class CommandParser
{
    /// <summary>
    /// Matches /ai start command at the beginning of a line (after whitespace).
    /// </summary>
    private static readonly Regex AiStartPattern = new(
        @"^\s*/ai\s+start",
        RegexOptions.Multiline | RegexOptions.Compiled
    );

    /// <summary>
    /// Matches repository list in line format: "Repos: owner/repo, owner/repo"
    /// </summary>
    private static readonly Regex ReposLinePattern = new(
        @"^\s*Repos\s*:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// Matches individual repository in owner/repo format
    /// </summary>
    private static readonly Regex RepoFormatPattern = new(
        @"[a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Matches repository list item in section format: "- owner/repo"
    /// </summary>
    private static readonly Regex RepoListItemPattern = new(
        @"^\s*-\s+([a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+)",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Matches /ai command at the beginning of a line
    /// </summary>
    private static readonly Regex AiCommandPattern = new(
        @"^\s*/ai\s+",
        RegexOptions.Multiline | RegexOptions.Compiled
    );

    /// <summary>
    /// Parse /ai start command from an issue comment.
    /// Accepts /ai start at the beginning of any line (ignoring leading whitespace).
    /// Task description includes trailing text on the same line plus subsequent lines,
    /// stopping at the next /ai command or end of comment.
    /// </summary>
    /// <param name="commentText">The raw comment text.</param>
    /// <returns>Task description if command found, null otherwise.</returns>
    public static string? ParseAiStartCommand(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return null;

        var match = AiStartPattern.Match(commentText);
        if (!match.Success)
            return null;

        // Find where this line ends
        var matchEnd = match.Index + match.Length;
        var lineEnd = commentText.IndexOf('\n', matchEnd);
        
        // Extract any trailing text on the same line (after /ai start)
        string trailingOnLine;
        int nextLineStart;
        
        if (lineEnd == -1)
        {
            // /ai start is on the last line
            trailingOnLine = commentText.Substring(matchEnd).Trim();
            return string.IsNullOrWhiteSpace(trailingOnLine) ? null : trailingOnLine;
        }
        else
        {
            trailingOnLine = commentText.Substring(matchEnd, lineEnd - matchEnd).Trim();
            nextLineStart = lineEnd + 1; // Skip the \n
        }

        if (nextLineStart >= commentText.Length)
        {
            // No content after the /ai start line
            return string.IsNullOrWhiteSpace(trailingOnLine) ? null : trailingOnLine;
        }

        // Extract remaining content after the /ai start line
        var remainingContent = commentText.Substring(nextLineStart);

        // Look for the next /ai command
        var nextAiMatch = AiCommandPattern.Match(remainingContent);
        string contentUntilNextCommand;

        if (nextAiMatch.Success)
        {
            // Found another /ai command; stop at its beginning
            contentUntilNextCommand = remainingContent.Substring(0, nextAiMatch.Index).TrimEnd();
        }
        else
        {
            // No more /ai commands; use all remaining content
            contentUntilNextCommand = remainingContent.TrimEnd();
        }

        // Combine trailing text on same line with subsequent content
        if (string.IsNullOrEmpty(trailingOnLine))
        {
            var trimmed = contentUntilNextCommand.TrimStart();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }
        
        if (string.IsNullOrEmpty(contentUntilNextCommand))
        {
            return trailingOnLine;
        }
        
        return $"{trailingOnLine}\n{contentUntilNextCommand}";
    }

    /// <summary>
    /// Parse repository list from Issue body.
    /// Tries line format first: "Repos: owner/repo, owner/repo"
    /// Falls back to section format: "## Repositories\n- owner/repo"
    /// </summary>
    /// <param name="issueBody">The full issue body.</param>
    /// <returns>List of repositories in format "owner/repo".</returns>
    public static IReadOnlyList<string> ParseRepositories(string issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
            return Array.Empty<string>();

        // Try line format first
        var lineRepos = ParseRepositoriesLineFormat(issueBody);
        if (lineRepos.Count > 0)
            return lineRepos;

        // Fall back to section format
        return ParseRepositoriesSectionFormat(issueBody);
    }

    /// <summary>
    /// Parse repositories from "Repos: owner/repo, owner/repo" format.
    /// </summary>
    private static IReadOnlyList<string> ParseRepositoriesLineFormat(string issueBody)
    {
        var repos = new List<string>();

        foreach (var line in issueBody.Split('\n'))
        {
            var match = ReposLinePattern.Match(line);
            if (!match.Success)
                continue;

            var reposText = match.Groups[1].Value;
            var repoMatches = RepoFormatPattern.Matches(reposText);
            foreach (Match repoMatch in repoMatches)
            {
                repos.Add(repoMatch.Value);
            }

            // Only process first match (should be unique)
            break;
        }

        return repos.AsReadOnly();
    }

    /// <summary>
    /// Parse repositories from section format.
    /// Expects a section like:
    /// ## Repositories
    /// - owner/repo1
    /// - owner/repo2
    /// </summary>
    private static IReadOnlyList<string> ParseRepositoriesSectionFormat(string issueBody)
    {
        var repos = new List<string>();
        var inReposSection = false;

        foreach (var line in issueBody.Split('\n'))
        {
            var trimmedLine = line.Trim();

            // Check if this is exactly the "## Repositories" header
            if (trimmedLine.Equals("## Repositories", StringComparison.OrdinalIgnoreCase))
            {
                inReposSection = true;
                continue;
            }

            if (inReposSection)
            {
                // Stop at any other section header (but not non-header lines)
                if (trimmedLine.StartsWith("##"))
                    break;

                // Parse list item
                var match = RepoListItemPattern.Match(line);
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
            var trimmedLine = line.Trim();

            // Check for section header (exact match)
            if (trimmedLine.Equals("## Acceptance Criteria", StringComparison.OrdinalIgnoreCase))
            {
                inCriteriaSection = true;
                continue;
            }

            // Check for single-line format
            if (trimmedLine.StartsWith("Acceptance Criteria:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring(line.IndexOf(':') + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (inCriteriaSection)
            {
                // Stop at any other section header
                if (trimmedLine.StartsWith("##"))
                    break;

                // Collect non-empty lines
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    criteria.Add(trimmedLine);
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
            var trimmedLine = line.Trim();

            // Check for section header (exact match)
            if (trimmedLine.Equals("## Constraints", StringComparison.OrdinalIgnoreCase))
            {
                inConstraintsSection = true;
                continue;
            }

            // Check for single-line format
            if (trimmedLine.StartsWith("Constraints:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring(line.IndexOf(':') + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (inConstraintsSection)
            {
                // Stop at any other section header
                if (trimmedLine.StartsWith("##"))
                    break;

                // Collect non-empty lines
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    constraints.Add(trimmedLine);
                }
            }
        }

        return constraints.Count > 0 ? string.Join("\n", constraints) : null;
    }
}
