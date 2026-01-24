using GhOrchestrator.Core;
using Microsoft.Extensions.Logging;

namespace GhOrchestrator.Host;

public sealed class HostOrchestratorLogger : IOrchestratorLogger
{
    private readonly ILogger _logger;

    public HostOrchestratorLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message, params object?[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object?[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogError(Exception exception, string message, params object?[] args)
    {
        _logger.LogError(exception, message, args);
    }
}
