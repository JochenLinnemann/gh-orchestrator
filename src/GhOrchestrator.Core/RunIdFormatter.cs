using System.Globalization;

namespace GhOrchestrator.Core;

public static class RunIdFormatter
{
    public static string Format(int issueNumber, DateTimeOffset now)
    {
        var timestamp = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        return $"run-{issueNumber}-{timestamp}";
    }
}
