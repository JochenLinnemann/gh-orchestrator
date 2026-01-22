namespace GhOrchestrator.Core.Tests;

public class OpenAIWorkerConfigurationTests
{
    [Fact]
    public void OpenAIWorkerConfiguration_WithValidEnvironment_Loads()
    {
        using var environment = new EnvironmentScope(
            ("OPENAI_API_KEY", "test-key"),
            ("OPENAI_MODEL", "gpt-4o-mini"),
            ("OPENAI_TIMEOUT_SECONDS", "30"),
            ("OPENAI_MAX_RETRIES", "2"));

        var result = OpenAIWorkerConfiguration.TryLoadFromEnvironment(out var configuration);

        Assert.True(result.IsValid);
        Assert.NotNull(configuration);
        Assert.Equal("test-key", configuration!.ApiKey);
        Assert.Equal("gpt-4o-mini", configuration.Model);
        Assert.Equal(TimeSpan.FromSeconds(30), configuration.Timeout);
        Assert.Equal(2, configuration.MaxRetries);
    }

    [Fact]
    public void OpenAIWorkerConfiguration_MissingApiKey_Fails()
    {
        using var environment = new EnvironmentScope(
            ("OPENAI_API_KEY", null),
            ("OPENAI_MODEL", "gpt-4o-mini"),
            ("OPENAI_TIMEOUT_SECONDS", "30"),
            ("OPENAI_MAX_RETRIES", "2"));

        var result = OpenAIWorkerConfiguration.TryLoadFromEnvironment(out var configuration);

        Assert.False(result.IsValid);
        Assert.Null(configuration);
        Assert.Equal("OPENAI_API_KEY environment variable is required", result.ErrorMessage);
    }

    [Fact]
    public void OpenAIWorkerConfiguration_InvalidTimeout_Fails()
    {
        using var environment = new EnvironmentScope(
            ("OPENAI_API_KEY", "test-key"),
            ("OPENAI_MODEL", "gpt-4o-mini"),
            ("OPENAI_TIMEOUT_SECONDS", "0"),
            ("OPENAI_MAX_RETRIES", "2"));

        var result = OpenAIWorkerConfiguration.TryLoadFromEnvironment(out var configuration);

        Assert.False(result.IsValid);
        Assert.Null(configuration);
        Assert.Equal("OPENAI_TIMEOUT_SECONDS must be a positive integer", result.ErrorMessage);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.OrdinalIgnoreCase);

        public EnvironmentScope(params (string Name, string? Value)[] variables)
        {
            foreach (var (name, value) in variables)
            {
                _originalValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
