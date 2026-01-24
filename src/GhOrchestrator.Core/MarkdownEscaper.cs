using System.Text;

namespace GhOrchestrator.Core;

/// <summary>
/// Escapes markdown control characters for GitHub issue comments.
/// </summary>
public static class MarkdownEscaper
{
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '*':
                case '_':
                case '`':
                case '[':
                case ']':
                case '(':
                case ')':
                case '#':
                case '+':
                case '!':
                case '|':
                case '>':
                case '<':
                    builder.Append('\\').Append(character);
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
