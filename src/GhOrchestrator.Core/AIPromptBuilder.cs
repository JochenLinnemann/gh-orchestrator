using System.Text;

namespace GhOrchestrator.Core;

/// <summary>
/// Builds a structured prompt payload for AI worker execution.
/// </summary>
public static class AIPromptBuilder
{
    public static string Build(AIPromptRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (request.Task is null)
            throw new ArgumentException("Task is required.", nameof(request));
        if (request.Repositories is null)
            throw new ArgumentException("Repositories are required.", nameof(request));
        if (request.Policies is null)
            throw new ArgumentException("Policies are required.", nameof(request));
        if (request.DefinitionOfDone is null)
            throw new ArgumentException("Definition of done is required.", nameof(request));
        if (request.SuccessCriteria is null)
            throw new ArgumentException("Success criteria is required.", nameof(request));

        var builder = new StringBuilder();

        builder.AppendLine("## Task");
        builder.AppendLine($"- Title: {Escape(request.Task.Title)}");
        builder.AppendLine($"- Description: {FormatOptionalText(request.Task.Description)}");
        builder.AppendLine();

        AppendListSection(builder, "Acceptance Criteria", NormalizeLines(request.Task.AcceptanceCriteria));
        AppendListSection(builder, "Constraints", NormalizeLines(request.Task.Constraints));

        builder.AppendLine();
        builder.AppendLine("## Repository Context");

        if (request.Repositories.Count == 0)
        {
            builder.AppendLine("- (none provided)");
        }
        else
        {
            foreach (var repository in request.Repositories)
            {
                builder.AppendLine($"### {Escape(repository.Repository)}");
                builder.AppendLine($"- Primary Language: {FormatOptionalText(repository.PrimaryLanguage)}");
                AppendSubList(builder, "Key Files", repository.KeyFiles);
                AppendSubList(builder, "File Structure", repository.FileStructure);
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Policies");
        AppendListSection(builder, "Security", request.Policies.Security);
        AppendListSection(builder, "Naming", request.Policies.Naming);
        AppendListSection(builder, "Testing", request.Policies.Testing);
        AppendListSection(builder, "CI/CD", request.Policies.CiCd);

        AppendListSection(builder, "Definition of Done", request.DefinitionOfDone);
        AppendListSection(builder, "Success Criteria", request.SuccessCriteria);

        return builder.ToString().TrimEnd();
    }

    private static void AppendListSection(StringBuilder builder, string heading, IReadOnlyList<string> items)
    {
        builder.AppendLine($"## {heading}");

        if (items.Count == 0)
        {
            builder.AppendLine("- (none provided)");
        }
        else
        {
            foreach (var item in items)
            {
                builder.AppendLine($"- {Escape(item)}");
            }
        }
    }

    private static void AppendSubList(StringBuilder builder, string heading, IReadOnlyList<string> items)
    {
        builder.AppendLine($"- {heading}:");

        if (items.Count == 0)
        {
            builder.AppendLine("  - (none provided)");
        }
        else
        {
            foreach (var item in items)
            {
                builder.AppendLine($"  - {Escape(item)}");
            }
        }
    }

    private static IReadOnlyList<string> NormalizeLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static string FormatOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(none provided)";

        return Escape(value);
    }

    private static string Escape(string value) => MarkdownEscaper.Escape(value.Trim());
}
