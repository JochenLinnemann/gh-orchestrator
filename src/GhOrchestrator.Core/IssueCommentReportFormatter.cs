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
        IReadOnlyList<RepoExecutionResult> executionResults,
        IReadOnlyList<string> riskNotes)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(testInstructions))
            throw new ArgumentException("Test instructions are required.", nameof(testInstructions));
        if (executionResults is null)
            throw new ArgumentNullException(nameof(executionResults));
        if (riskNotes is null)
            throw new ArgumentNullException(nameof(riskNotes));

        var builder = new StringBuilder();
        builder.AppendLine("## Summary");
        builder.AppendLine(MarkdownEscaper.Escape(summary.Trim()));
        builder.AppendLine();
        builder.AppendLine("## Pull Requests");

        if (executionResults.Count == 0)
        {
            builder.AppendLine("- (none)");
        }
        else
        {
            foreach (var result in executionResults)
            {
                var repository = MarkdownEscaper.Escape(result.Repository);

                if (result.IsSuccess)
                {
                    if (result.PullRequest is null)
                    {
                        builder.AppendLine($"- ⚠️ {repository}: PR link missing");
                    }
                    else
                    {
                        builder.AppendLine($"- ✅ {repository}: {result.PullRequest.Url}");
                    }
                }
                else
                {
                    var errorMessage = MarkdownEscaper.Escape(result.ErrorMessage ?? "Unknown error");
                    builder.AppendLine($"- ❌ {repository}: {errorMessage}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## How to Test");
        builder.AppendLine(MarkdownEscaper.Escape(testInstructions.Trim()));
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
                builder.AppendLine($"- {MarkdownEscaper.Escape(risk)}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
