namespace GhOrchestrator.Core;

/// <summary>
/// Null object implementation of IOrchestratorLogger that discards all log messages.
/// </summary>
internal sealed class NullOrchestratorLogger : IOrchestratorLogger
{
    public static readonly NullOrchestratorLogger Instance = new();

    private NullOrchestratorLogger()
    {
    }

    public void LogInformation(string message, params object?[] args)
    {
        // Intentionally empty - null object pattern
    }

    public void LogWarning(string message, params object?[] args)
    {
        // Intentionally empty - null object pattern
    }

    public void LogError(Exception exception, string message, params object?[] args)
    {
        // Intentionally empty - null object pattern
    }
}
