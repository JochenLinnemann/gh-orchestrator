namespace GhOrchestrator.Core;

public interface IOrchestratorLogger
{
    void LogInformation(string message, params object?[] args);

    void LogWarning(string message, params object?[] args);

    void LogError(Exception exception, string message, params object?[] args);
}
