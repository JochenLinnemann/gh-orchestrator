namespace GhOrchestrator.Core;

public record OpenAIWorkerConfiguration(
    string ApiKey,
    string Model,
    TimeSpan Timeout,
    int MaxRetries)
{
    public static ValidationResult TryLoadFromEnvironment(out OpenAIWorkerConfiguration? configuration)
    {
        configuration = null;

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return ValidationResult.Failure("OPENAI_API_KEY environment variable is required");

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (string.IsNullOrWhiteSpace(model))
            return ValidationResult.Failure("OPENAI_MODEL environment variable is required");

        var timeoutSecondsRaw = Environment.GetEnvironmentVariable("OPENAI_TIMEOUT_SECONDS");
        if (!int.TryParse(timeoutSecondsRaw, out var timeoutSeconds) || timeoutSeconds <= 0)
            return ValidationResult.Failure("OPENAI_TIMEOUT_SECONDS must be a positive integer");

        var maxRetriesRaw = Environment.GetEnvironmentVariable("OPENAI_MAX_RETRIES");
        if (!int.TryParse(maxRetriesRaw, out var maxRetries) || maxRetries < 0)
            return ValidationResult.Failure("OPENAI_MAX_RETRIES must be a non-negative integer");

        configuration = new OpenAIWorkerConfiguration(
            apiKey,
            model,
            TimeSpan.FromSeconds(timeoutSeconds),
            maxRetries);

        return ValidationResult.Success();
    }
}
