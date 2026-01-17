using GhOrchestrator.Core;
using GhOrchestrator.Host;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load GitHub App config from environment
var appId = builder.Configuration["GH_APP_ID"] ?? throw new InvalidOperationException("GH_APP_ID not set");
var privateKey = builder.Configuration["GH_APP_PRIVATE_KEY"] ?? throw new InvalidOperationException("GH_APP_PRIVATE_KEY not set");
var webhookSecret = builder.Configuration["GH_WEBHOOK_SECRET"] ?? throw new InvalidOperationException("GH_WEBHOOK_SECRET not set");

var app = builder.Build();

// Health check
app.MapGet("/health", () => "OK");

// Webhook endpoint for issue comments
app.MapPost("/webhook", async (HttpRequest request) =>
{
    try
    {
        // Extract raw body
        request.EnableBuffering();
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Position = 0;

        // Extract signature header
        var signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();

        app.Logger.LogInformation("Received webhook: signature={Signature}, bodyLength={Length}", signatureHeader, body.Length);

        // Verify signature
        var isValid = GitHubWebhookSignatureVerifier.IsValid(body, signatureHeader, webhookSecret);

        if (!isValid)
        {
            app.Logger.LogWarning("Webhook signature verification failed");
            return Results.Unauthorized();
        }

        app.Logger.LogInformation("Signature verification passed");

        // Parse event from GitHub's webhook payload
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var payload = System.Text.Json.JsonSerializer.Deserialize<GitHubIssueCommentWebhookPayload>(body, options);
        if (payload == null)
        {
            app.Logger.LogWarning("Failed to deserialize webhook payload");
            return Results.BadRequest("Invalid payload");
        }

        // Filter to only "created" actions (ignore edits, deletes)
        if (payload.Action != "created")
        {
            app.Logger.LogInformation("Ignoring action: {Action}", payload.Action);
            return Results.Ok(new { status = "Event received but ignored (action not 'created')" });
        }

        // Map to core event model
        var @event = new IssueCommentEvent(
            Repository: payload.Repository.FullName,
            IssueNumber: payload.Issue.Number,
            CommentBody: payload.Comment.Body,
            CommentAuthor: payload.Comment.User.Login);

        app.Logger.LogInformation("Parsed event: repo={Repo}, issue={Issue}, author={Author}, body={Body}", @event.Repository, @event.IssueNumber, @event.CommentAuthor, @event.CommentBody);

        // TODO: Wire up handler and orchestrator when GitHub client is available
        return Results.Ok(new { status = "Event received and verified" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error processing webhook");
        return Results.StatusCode(500);
    }
});

app.Run();
