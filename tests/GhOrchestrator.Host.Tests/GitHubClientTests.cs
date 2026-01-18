using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GhOrchestrator.Core;
using GhOrchestrator.Host;

namespace GhOrchestrator.Host.Tests;

public class GitHubClientTests
{
    [Fact]
    public async Task GetIssue_UsesInstallationToken()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var responseBody = "{\"title\":\"Test\",\"body\":\"Body\",\"state\":\"open\",\"html_url\":\"https://github.com/octo/demo/issues/42\"}";
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK, responseBody);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        var issue = await client.GetIssue("octo/demo", 42);

        Assert.NotNull(issue);
        Assert.Equal("Test", issue?.Title);

        var request = handler.Requests.Single();
        Assert.Equal("/repos/octo/demo/issues/42", request.RequestUri?.AbsolutePath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "token-123"), request.Headers.Authorization);
        Assert.Contains(request.Headers.Accept, header => header.MediaType == "application/vnd.github+json");
    }

    [Fact]
    public async Task CreateBranch_SendsRefCreation()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path == "/repos/octo/demo/git/ref/heads/main")
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{\"object\":{\"sha\":\"abc123\"}}");

            if (path == "/repos/octo/demo/git/refs")
                return FakeHttpMessageHandler.Json(HttpStatusCode.Created, "{}");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        await client.CreateBranch("octo/demo", "ai/run-1/task", "main");

        var createRequest = handler.Requests.Single(request => request.RequestUri?.AbsolutePath == "/repos/octo/demo/git/refs");
        var requestIndex = handler.Requests.IndexOf(createRequest);
        var payload = handler.RequestBodies[requestIndex] ?? string.Empty;
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("refs/heads/ai/run-1/task", document.RootElement.GetProperty("ref").GetString());
        Assert.Equal("abc123", document.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task UpdateProjectFields_SendsRestApiRequests()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            callCount += 1;

            // First call: list project items
            if (path == "/orgs/octo/projectsV2/proj-1/items")
            {
                var items = """
                [
                  { "node_id": "item-1", "content_type": "Issue", "content": { "number": 42 } }
                ]
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, items);
            }

            // Second call: get project fields
            if (path == "/orgs/octo/projectsV2/proj-1/fields")
            {
                var fields = """
                [
                  { "id": "field-ai", "name": "AI", "data_type": "single_select", "options": [ { "id": "opt-running", "name": "running" } ] },
                  { "id": "field-run", "name": "Run ID", "data_type": "text" }
                ]
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, fields);
            }

            // Update calls: PATCH field values
            if (path.StartsWith("/orgs/octo/projectsV2/items/") && request.Method.Method == "PATCH")
            {
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        var updates = new[]
        {
            new ProjectFieldUpdate("AI", "running"),
            new ProjectFieldUpdate("Run ID", "run-123")
        };

        await client.UpdateProjectFields("octo/demo", "proj-1", 42, updates);

        // Verify: list items, get fields, then 2 PATCH requests for field updates
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(2, handler.Requests.Count(r => r.Method.Method == "PATCH"));
    }

    [Fact]
    public async Task UpdateProjectFields_PaginatesItemsUntilFound()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;
            callCount += 1;

            // First call: list items page 1 (no match)
            if (path == "/orgs/octo/projectsV2/proj-1/items" && (string.IsNullOrEmpty(query) || !query.Contains("&page=") && !query.Contains("?page=") && !query.StartsWith("page=")))
            {
                var page1 = """
                [
                  { "node_id": "item-1", "content_type": "Issue", "content": { "number": 1 } }
                ]
                """;
                var response = FakeHttpMessageHandler.Json(HttpStatusCode.OK, page1);
                response.Headers.Add("Link", "<https://api.github.com/orgs/octo/projectsV2/proj-1/items?page=2>; rel=\"next\"");
                return response;
            }

            // Get project fields (called on first page)
            if (path == "/orgs/octo/projectsV2/proj-1/fields")
            {
                var fields = """
                [
                  { "id": "field-ai", "name": "AI", "data_type": "single_select", "options": [ { "id": "opt-running", "name": "running" } ] }
                ]
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, fields);
            }

            // Second call: list items page 2 (found)
            if (path == "/orgs/octo/projectsV2/proj-1/items" && query.Contains("page=2"))
            {
                var page2 = """
                [
                  { "node_id": "item-2", "content_type": "Issue", "content": { "number": 42 } }
                ]
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, page2);
            }

            // PATCH field value
            if (path.StartsWith("/orgs/octo/projectsV2/items/") && request.Method.Method == "PATCH")
            {
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        var updates = new[]
        {
            new ProjectFieldUpdate("AI", "running")
        };

        await client.UpdateProjectFields("octo/demo", "proj-1", 42, updates);

        // Verify: 2 list calls (pagination), 1 get fields, 1 PATCH
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri?.AbsolutePath == "/orgs/octo/projectsV2/proj-1/items"));
    }

    private static GitHubClient CreateClient(FakeHttpMessageHandler handler, IGitHubInstallationTokenProvider tokenProvider)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        return new GitHubClient(httpClient, tokenProvider);
    }

    private sealed class FixedTokenProvider : IGitHubInstallationTokenProvider
    {
        private readonly string _token;

        public FixedTokenProvider(string token)
        {
            _token = token;
        }

        public Task<string> GetInstallationToken(string repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(_token);
    }
}
