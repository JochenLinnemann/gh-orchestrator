using System.Text;

namespace GhOrchestrator.Core;

/// <summary>
/// Formats a report comment for GitHub issues.
/// </summary>
public static class IssueCommentReportFormatter
{
    /// <summary>
    /// Format an issue comment report with summary, test instructions, PR links, execution details, and risk notes.
    /// </summary>
    public static string Format(
        string summary,
        string testInstructions,
        IReadOnlyList<RepoExecutionResult> executionResults,
        TaskRunExecutionResult executionResult,
        IReadOnlyList<string> riskNotes)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));
        if (string.IsNullOrWhiteSpace(testInstructions))
            throw new ArgumentException("Test instructions are required.", nameof(testInstructions));
        if (executionResults is null)
            throw new ArgumentNullException(nameof(executionResults));
        if (executionResult is null)
            throw new ArgumentNullException(nameof(executionResult));
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
            var orderedResults = executionResults
                .OrderBy(result => result.Repository, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var result in orderedResults)
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
        AppendExecutionDetails(builder, executionResults, executionResult);
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

    private static void AppendExecutionDetails(
        StringBuilder builder,
        IReadOnlyList<RepoExecutionResult> executionResults,
        TaskRunExecutionResult executionResult)
    {
        builder.AppendLine("## Execution Details");

        var metadata = executionResult.WorkerResult.Metadata;
        var model = metadata?.Model ?? "Not reported";
        builder.AppendLine($"- AI model: {MarkdownEscaper.Escape(model)}");

        if (metadata?.ExecutionDuration is { } duration)
        {
            var seconds = Math.Round(duration.TotalSeconds, 1);
            builder.AppendLine($"- Execution time: {seconds:0.0}s");
        }
        else
        {
            builder.AppendLine("- Execution time: Not reported");
        }

        if (metadata?.TokenUsage is not null)
        {
            var inputTokens = metadata.TokenUsage.InputTokens is { } input
                ? input.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                : "n/a";
            var outputTokens = metadata.TokenUsage.OutputTokens is { } output
                ? output.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                : "n/a";
            var totalTokens = metadata.TokenUsage.TotalTokens is { } total
                ? total.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                : "n/a";

            builder.AppendLine($"- Token usage: input {inputTokens}, output {outputTokens}, total {totalTokens}");
        }
        else
        {
            builder.AppendLine("- Token usage: Not reported");
        }

        var confidence = metadata?.Confidence;
        var confidenceText = string.IsNullOrWhiteSpace(confidence) ? "Not reported" : confidence;
        builder.AppendLine($"- Confidence: {MarkdownEscaper.Escape(confidenceText)}");

        if (!string.IsNullOrWhiteSpace(metadata?.ExecutionTraceUrl))
        {
            builder.AppendLine($"- Execution trace: {MarkdownEscaper.Escape(metadata.ExecutionTraceUrl)}");
        }

        if (metadata?.Warnings is { Count: > 0 })
        {
            builder.AppendLine("- Warnings:");
            foreach (var warning in metadata.Warnings)
            {
                builder.AppendLine($"  - {MarkdownEscaper.Escape(warning)}");
            }
        }

        var fileChangesByRepo = executionResult.WorkerResult.RepoResults
            .GroupBy(result => result.Repository, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().FileChanges,
                StringComparer.OrdinalIgnoreCase);

        builder.AppendLine();
        builder.AppendLine("### Files Changed");

        var orderedRepos = executionResults
            .Select(result => result.Repository)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(repo => repo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var repository in orderedRepos)
        {
            if (!fileChangesByRepo.TryGetValue(repository, out var changes) || changes.Count == 0)
            {
                builder.AppendLine($"- {MarkdownEscaper.Escape(repository)}: (none)");
                continue;
            }

            var changeCount = changes.Count;
            builder.AppendLine($"- {MarkdownEscaper.Escape(repository)} ({changeCount}):");

            foreach (var change in changes)
            {
                builder.AppendLine($"  - {MarkdownEscaper.Escape(change.Path)} ({change.ChangeType})");
            }
        }

        var unchangedRepos = orderedRepos
            .Where(repo => !fileChangesByRepo.TryGetValue(repo, out var changes) || changes.Count == 0)
            .ToList();

        if (unchangedRepos.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Unchanged Repositories");
            foreach (var repository in unchangedRepos)
            {
                builder.AppendLine($"- {MarkdownEscaper.Escape(repository)}");
            }
        }

        var validationWarnings = executionResult.ValidationResult.RepoResults
            .Where(result => !result.IsValid)
            .ToList();

        if (validationWarnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Validation Warnings");
            foreach (var warning in validationWarnings)
            {
                builder.AppendLine($"- {MarkdownEscaper.Escape(warning.Repository)}: {MarkdownEscaper.Escape(string.Join("; ", warning.Errors))}");
            }
        }
    }
}
