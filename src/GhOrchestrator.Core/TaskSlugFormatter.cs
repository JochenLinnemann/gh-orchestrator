using System.Text;

namespace GhOrchestrator.Core;

/// <summary>
/// Formats a short slug for branch names based on task description.
/// </summary>
public static class TaskSlugFormatter
{
    /// <summary>
    /// Build a short slug from a task description.
    /// </summary>
    /// <param name="title">Issue title.</param>
    /// <param name="description">Task description from the /ai start command.</param>
    /// <returns>Slug suitable for branch names.</returns>
    public static string Format(string title, string description)
    {
        var source = string.IsNullOrWhiteSpace(title) ? description : title;
        if (string.IsNullOrWhiteSpace(source))
            return "task";

        var builder = new StringBuilder(source.Length);
        var previousDash = false;

        foreach (var ch in source.Trim())
        {
            var lower = char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(lower))
            {
                builder.Append(lower);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "task" : slug;
    }
}
