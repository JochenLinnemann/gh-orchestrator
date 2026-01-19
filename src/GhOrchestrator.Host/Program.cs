using GhOrchestrator.Core;
using GhOrchestrator.Host;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var hostConfiguration = GitHubHostConfiguration.Load(builder.Configuration);
var webhookHandler = new IssueCommentWebhookHandler();
var httpClient = new HttpClient();
var jwtProvider = new GitHubAppJwtProvider(hostConfiguration.AppId, hostConfiguration.ReadPrivateKeyPem());
var tokenCache = new GitHubInstallationTokenCache();
var installationTokenProvider = new GitHubInstallationTokenProvider(httpClient, jwtProvider, tokenCache);
var gitHubClient = new GitHubClient(httpClient, installationTokenProvider);
var orchestrator = new Orchestrator();

var app = builder.Build();

// Health check
app.MapGet("/health", () => "OK");

// Webhook endpoint for issue comments
app.MapPost("/webhook", async (HttpRequest request) =>
{
    try
    {
        var eventName = request.Headers["X-GitHub-Event"].ToString();
        if (!string.Equals(eventName, "issue_comment", StringComparison.OrdinalIgnoreCase))
        {
            app.Logger.LogInformation("Ignoring event type: {Event}", eventName);
            return Results.Ok(new { status = "Event received but ignored (unsupported event type)" });
        }

        // Extract raw body
        request.EnableBuffering();
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Position = 0;

        // Extract signature header
        var signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();

        app.Logger.LogInformation("Received webhook: event={Event}, signature={Signature}, bodyLength={Length}", eventName, signatureHeader, body.Length);

        var webhookResult = webhookHandler.Handle(body, signatureHeader, hostConfiguration.WebhookSecret);
        if (!webhookResult.IsValid || webhookResult.Event is null)
        {
            if (string.Equals(webhookResult.ErrorMessage, "Webhook signature validation failed", StringComparison.OrdinalIgnoreCase))
            {
                app.Logger.LogWarning("Webhook signature verification failed");
                return Results.Unauthorized();
            }

            app.Logger.LogWarning("Failed to parse webhook payload: {Error}", webhookResult.ErrorMessage);
            return Results.BadRequest(webhookResult.ErrorMessage ?? "Invalid payload");
        }

        app.Logger.LogInformation("Signature verification passed");

        var action = TryGetAction(body);
        if (action is null)
        {
            app.Logger.LogWarning("Failed to read action from webhook payload");
            return Results.BadRequest("Invalid payload");
        }

        if (!string.Equals(action, "created", StringComparison.OrdinalIgnoreCase))
        {
            app.Logger.LogInformation("Ignoring action: {Action}", action);
            return Results.Ok(new { status = "Event received but ignored (action not 'created')" });
        }

        var @event = webhookResult.Event;
        app.Logger.LogInformation("Checking org authorization: repository={Repository}, allowedOrg={AllowedOrg}", @event.Repository, hostConfiguration.AllowedOrg);
        if (!IsAllowedOrg(@event.Repository, hostConfiguration.AllowedOrg))
        {
            app.Logger.LogWarning("Rejected webhook for unauthorized org: {Repository}", @event.Repository);
            return Results.StatusCode(403);
        }

        app.Logger.LogInformation("Parsed event: repo={Repo}, issue={Issue}, author={Author}, body={Body}", @event.Repository, @event.IssueNumber, @event.CommentAuthor, @event.CommentBody);
        var runId = RunIdFormatter.Format(@event.IssueNumber, DateTimeOffset.UtcNow);

        // Orchestrate the full task flow
        OrchestratorResult result;
        try
        {
            result = await orchestrator.ProcessTaskAsync(
                gitHubClient,
                @event,
                hostConfiguration.ProjectId,
                runId,
                request.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Unexpected error during orchestration");
            return Results.StatusCode(500);
        }

        if (!result.IsSuccess)
        {
            app.Logger.LogWarning("Task processing failed: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { status = "Failed", error = result.ErrorMessage, runId = result.RunId });
        }

        if (result.AlreadyClaimed)
        {
            app.Logger.LogInformation("Task already claimed for run {RunId}", result.RunId);
            return Results.Ok(new { status = "Already claimed", runId = result.RunId });
        }

        var successCount = result.ExecutionResult?.Results.Count(r => r.IsSuccess) ?? 0;
        var failureCount = result.ExecutionResult?.Results.Count(r => !r.IsSuccess) ?? 0;

        app.Logger.LogInformation(
            "Task execution completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount,
            failureCount);

        // Log details of any failures
        if (result.ExecutionResult?.Results != null)
        {
            foreach (var executionResult in result.ExecutionResult.Results.Where(r => !r.IsSuccess))
            {
                app.Logger.LogWarning("Execution failed for {Repository}: {Error}", executionResult.Repository, executionResult.ErrorMessage);
            }
        }

        return Results.Ok(new
        {
            status = "Executed",
            runId = result.RunId,
            successCount,
            failureCount,
            results = result.ExecutionResult?.Results.Select(r => new
            {
                r.Repository,
                r.IsSuccess,
                r.BranchName,
                r.ErrorMessage,
                PullRequestUrl = r.PullRequest?.Url
            })
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error processing webhook");
        return Results.StatusCode(500);
    }
});

app.Run();

static string? TryGetAction(string payload)
{
    try
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("action", out var action)
            ? action.GetString()
            : null;
    }
    catch (JsonException)
    {
        return null;
    }
}

static bool IsAllowedOrg(string repository, string allowedOrg)
{
    var separatorIndex = repository.IndexOf('/');
    if (separatorIndex <= 0)
        return false;

    var org = repository[..separatorIndex];
    return string.Equals(org, allowedOrg, StringComparison.OrdinalIgnoreCase);
}
