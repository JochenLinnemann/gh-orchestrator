using System.Text;

namespace GhOrchestrator.Core;

/// <summary>
/// Formats a report comment for GitHub issues.
/// </summary>
public static class IssueCommentReportFormatter
{
    /// <summary>
    /// Format an issue comment report with summary, test instructions, PR links, and risk notes.
    /// </summary>
    public static string Format(
        string summary,
        string testInstructions,
        IReadOnlyList<PullRequestLink> pullRequests,
        IReadOnlyList<string> riskNotes)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(testInstructions))
            throw new ArgumentException("Test instructions are required.", nameof(testInstructions));
        if (pullRequests is null)
            throw new ArgumentNullException(nameof(pullRequests));
        if (riskNotes is null)
            throw new ArgumentNullException(nameof(riskNotes));

        var builder = new StringBuilder();
        builder.AppendLine("## Summary");
        builder.AppendLine(summary.Trim());
        builder.AppendLine();
        builder.AppendLine("## Pull Requests");

        if (pullRequests.Count == 0)
        {
            builder.AppendLine("- (none)");
        }
        else
        {
            foreach (var pr in pullRequests)
            {
                builder.AppendLine($"- {pr.Repository}: {pr.Url}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## How to Test");
        builder.AppendLine(testInstructions.Trim());
        builder.AppendLine();
        builder.AppendLine("## Risks");

        if (riskNotes.Count == 0)
        {
            builder.AppendLine("- None noted.");
        }
        else
        {
            foreach (var risk in riskNotes)
            {
                builder.AppendLine($"- {risk}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
