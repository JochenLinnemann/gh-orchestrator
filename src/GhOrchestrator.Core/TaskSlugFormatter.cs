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
    /// <param name="description">Task description.</param>
    /// <returns>Slug suitable for branch names.</returns>
    public static string Format(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "task";

        var builder = new StringBuilder(description.Length);
        var previousDash = false;

        foreach (var ch in description.Trim())
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
