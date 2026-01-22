using OpenAI.Chat;

namespace GhOrchestrator.Core;

/// <summary>
/// Executes tasks using the OpenAI API with a structured JSON response contract.
/// </summary>
public sealed class OpenAIWorker : IAIWorker
{
    private readonly OpenAIWorkerConfiguration _configuration;
    private readonly IOrchestratorLogger _logger;
    private readonly ChatClient _chatClient;

    public OpenAIWorker(OpenAIWorkerConfiguration configuration, IOrchestratorLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? new NullOrchestratorLogger();
        _chatClient = new ChatClient(configuration.Model, configuration.ApiKey);
    }

    public async Task<AIWorkerResult> ExecuteAsync(AIWorkerRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var prompt = BuildPrompt(request);
        var messages = new ChatMessage[]
        {
            new SystemChatMessage("You are a careful coding assistant. Respond only with JSON that matches the schema."),
            new UserChatMessage(prompt)
        };

        Exception? lastException = null;

        for (var attempt = 0; attempt <= _configuration.MaxRetries; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_configuration.Timeout);

            try
            {
                var completion = await _chatClient.CompleteChatAsync(
                    messages,
                    new ChatCompletionOptions(),
                    timeoutCts.Token);
                var responseText = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
                return OpenAIWorkerResponseParser.Parse(responseText, request.Repositories);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < _configuration.MaxRetries)
            {
                lastException = ex;
                _logger.LogWarning("OpenAI worker retrying after failure: attempt={Attempt}, error={Error}", attempt + 1, ex.Message);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        _logger.LogError(lastException ?? new InvalidOperationException("Unknown OpenAI worker failure"), "OpenAI worker failed");
        return BuildFailureResult(request.Repositories, lastException?.Message ?? "OpenAI worker failed");
    }

    private static AIWorkerResult BuildFailureResult(IReadOnlyList<string> repositories, string reason)
    {
        var results = repositories
            .Select(repo => new AIWorkerRepoResult(
                repo,
                false,
                Array.Empty<AIWorkerFileChange>(),
                string.Empty,
                reason))
            .ToArray();

        return new AIWorkerResult(results);
    }

    private static bool IsRetryable(Exception exception) =>
        exception is HttpRequestException ||
        exception is TaskCanceledException;

    private static string BuildPrompt(AIWorkerRequest request)
    {
        // Convert flat AIWorkerRequest to structured AIPromptRequest for canonical prompt building.
        // Note: Repository context (language, files, structure) and execution constraints are not
        // available in AIWorkerRequest, so this uses simplified versions.
        // TODO: Refactor to pass AIPromptRequest directly to enable richer prompts.
        var definitionOfDone = request.DefinitionOfDone is not null
            ? NormalizeLines(request.DefinitionOfDone)
            : Array.Empty<string>();

        var policies = new AIPromptPolicies(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var repositories = request.Repositories
            .Select(repo => new AIPromptRepositoryContext(
                repo,
                null,
                Array.Empty<string>(),
                Array.Empty<string>()))
            .ToList();

        var promptRequest = new AIPromptRequest(
            request.Task,
            repositories,
            policies,
            definitionOfDone,
            Array.Empty<string>());

        return AIPromptBuilder.Build(promptRequest);
    }

    private static IReadOnlyList<string> NormalizeLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private sealed class NullOrchestratorLogger : IOrchestratorLogger
    {
        public void LogInformation(string message, params object?[] args)
        {
        }

        public void LogWarning(string message, params object?[] args)
        {
        }

        public void LogError(Exception exception, string message, params object?[] args)
        {
        }
    }
}
