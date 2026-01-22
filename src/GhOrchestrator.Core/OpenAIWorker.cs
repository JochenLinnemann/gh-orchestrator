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
        var acceptanceCriteria = string.IsNullOrWhiteSpace(request.AcceptanceCriteria)
            ? "(none provided)"
            : request.AcceptanceCriteria.Trim();
        var constraints = string.IsNullOrWhiteSpace(request.Constraints)
            ? "(none provided)"
            : request.Constraints.Trim();
        var definitionOfDone = string.IsNullOrWhiteSpace(request.DefinitionOfDone)
            ? "(none provided)"
            : request.DefinitionOfDone.Trim();
        var policyLines = request.Policies.Count == 0
            ? "(none provided)"
            : string.Join("\n", request.Policies.Select(policy => $"- {policy.Key}: {policy.Value}"));
        var executionConstraints = request.ExecutionConstraints.Count == 0
            ? "(none provided)"
            : string.Join("\n", request.ExecutionConstraints.Select(constraint => $"- {constraint.Key}: {constraint.Value}"));

        return $$$"""
            ## Task
            Title: {request.Task.Title}
            Description: {request.Task.Description}
            Acceptance Criteria: {acceptanceCriteria}
            Constraints: {constraints}
            Definition of Done: {definitionOfDone}

            ## Repositories
            {string.Join("\n", request.Repositories.Select(repo => $"- {repo}"))}

            ## Policies
            {policyLines}

            ## Execution Constraints
            {executionConstraints}

            ## Output Schema
            Respond with JSON only, matching this schema:
            {{
              "repoResults": [
                {{
                  "repository": "org/repo",
                  "summary": "short summary of changes",
                  "changes": [
                    {{
                      "path": "path/to/file.cs",
                      "changeType": "create|modify|delete",
                      "content": "full file content after change"
                    }}
                  ]
                }}
              ]
            }}

            Requirements:
            - Include one repoResults entry for each repository listed.
            - Use empty changes array when no updates are needed.
            - Do not include any text outside the JSON.
            """;
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
