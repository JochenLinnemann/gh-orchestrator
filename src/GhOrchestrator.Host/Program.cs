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
var taskClaimService = new TaskClaimService();

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

        var result = webhookHandler.Handle(body, signatureHeader, hostConfiguration.WebhookSecret);
        if (!result.IsValid || result.Event is null)
        {
            if (string.Equals(result.ErrorMessage, "Webhook signature validation failed", StringComparison.OrdinalIgnoreCase))
            {
                app.Logger.LogWarning("Webhook signature verification failed");
                return Results.Unauthorized();
            }

            app.Logger.LogWarning("Failed to parse webhook payload: {Error}", result.ErrorMessage);
            return Results.BadRequest(result.ErrorMessage ?? "Invalid payload");
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

        var @event = result.Event;
        app.Logger.LogInformation("Checking org authorization: repository={Repository}, allowedOrg={AllowedOrg}", @event.Repository, hostConfiguration.AllowedOrg);
        if (!IsAllowedOrg(@event.Repository, hostConfiguration.AllowedOrg))
        {
            app.Logger.LogWarning("Rejected webhook for unauthorized org: {Repository}", @event.Repository);
            return Results.StatusCode(403);
        }

        app.Logger.LogInformation("Parsed event: repo={Repo}, issue={Issue}, author={Author}, body={Body}", @event.Repository, @event.IssueNumber, @event.CommentAuthor, @event.CommentBody);
        var runId = RunIdFormatter.Format(@event.IssueNumber, DateTimeOffset.UtcNow);

        TaskClaimResult claimResult;
        try
        {
            claimResult = await taskClaimService.ClaimAsync(
                gitHubClient,
                @event.Repository,
                hostConfiguration.ProjectId,
                @event.IssueNumber,
                runId,
                cancellationToken: request.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Unexpected error while claiming task");
            return Results.StatusCode(500);
        }

        if (!claimResult.IsValid)
        {
            app.Logger.LogWarning("Task claim failed: {Error}", claimResult.ErrorMessage);
            return Results.BadRequest(new { status = "Claim failed", error = claimResult.ErrorMessage, runId });
        }

        if (claimResult.IsAlreadyClaimed)
        {
            app.Logger.LogInformation("Task already claimed for run {RunId}", runId);
            return Results.Ok(new { status = "Already claimed", runId });
        }

        app.Logger.LogInformation("Task claimed with {UpdateCount} updates for run {RunId}", claimResult.Updates.Count, runId);
        return Results.Ok(new { status = "Claimed", runId });
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
