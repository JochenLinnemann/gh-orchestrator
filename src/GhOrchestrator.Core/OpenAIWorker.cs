using OpenAI.Chat;

namespace GhOrchestrator.Core;

/// <summary>
/// Executes tasks using the OpenAI API with a structured JSON response contract.
/// </summary>
public sealed class OpenAIWorker : IAIWorker
{
    private readonly OpenAIWorkerConfiguration _configuration;
    private readonly IOrchestratorLogger _logger;
    private readonly ChatClient? _chatClient;
    private readonly bool _isChatModel;

    public OpenAIWorker(OpenAIWorkerConfiguration configuration, IOrchestratorLogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? NullOrchestratorLogger.Instance;
        _isChatModel = IsChatModel(configuration.Model);

        if (_isChatModel)
        {
            _chatClient = new ChatClient(configuration.Model, configuration.ApiKey);
        }
        else
        {
            // TODO: Add support for non-chat models using the completions endpoint.
            _chatClient = null;
            _logger.LogWarning("Model {Model} is not a chat model; completions endpoint support is TODO", configuration.Model);
        }
    }

    public async Task<AIWorkerResult> ExecuteAsync(AIWorkerRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (!_isChatModel || _chatClient is null)
        {
            const string reason = "Configured model is not chat-compatible; TODO: add completions endpoint support.";
            _logger.LogWarning(reason + " Model={Model}", _configuration.Model);
            return BuildFailureResult(request.Repositories, reason);
        }

        var prompt = BuildPrompt(request);
        var messages = new ChatMessage[]
        {
            new SystemChatMessage("You are a careful coding assistant. Respond only with JSON that matches the schema."),
            new UserChatMessage(prompt)
        };

        Exception? lastException = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
                var workerResult = OpenAIWorkerResponseParser.Parse(responseText, request.Repositories);
                var metadata = BuildMetadata(_configuration.Model, stopwatch.Elapsed);
                return workerResult with { Metadata = metadata };
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
        // Note: Repository context (language, files, structure) is not available in AIWorkerRequest,
        // so this uses simplified versions.
        // TODO: Refactor to pass AIPromptRequest directly to enable richer prompts.

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
            request.Policies);

        return AIPromptBuilder.Build(promptRequest);
    }

    private static AIWorkerExecutionMetadata BuildMetadata(
        string model,
        TimeSpan elapsed)
    {
        return new AIWorkerExecutionMetadata(
            model,
            elapsed,
            null,
            null,
            null,
            Array.Empty<string>());
    }

    private static bool IsChatModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        // Heuristic: Most "gpt-" models support chat completions.
        // Exclude known non-chat models (legacy completion/codex models).
        var normalized = model.Trim().ToLowerInvariant();
        
        // Known non-chat models (use completions endpoint instead)
        if (normalized.Contains("text-") ||
            normalized.Contains("code-") ||
            normalized.Contains("davinci") ||
            normalized.Contains("curie") ||
            normalized.Contains("babbage") ||
            normalized.Contains("ada") ||
            normalized.Contains("codex"))
        {
            return false;
        }

        // Assume "gpt-" prefix means chat model (GPT-3.5, GPT-4, GPT-4o, GPT-5, o1, etc.)
        return normalized.StartsWith("gpt-") || normalized.StartsWith("o1");
    }
}
