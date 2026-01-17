namespace GhOrchestrator.Host;

public interface IGitHubInstallationTokenProvider
{
    Task<string> GetInstallationToken(string repository, CancellationToken cancellationToken = default);
}
