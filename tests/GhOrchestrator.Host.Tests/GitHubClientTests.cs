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
    public async Task UpdateProjectFields_SendsGraphQlMutations()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount += 1;
            if (callCount == 1)
            {
                var metadata = """
                {
                  "data": {
                    "node": {
                      "fields": {
                        "nodes": [
                          { "__typename": "ProjectV2SingleSelectField", "id": "field-ai", "name": "AI", "options": [ { "id": "opt-running", "name": "running" } ] },
                          { "__typename": "ProjectV2Field", "id": "field-run", "name": "Run ID" }
                        ]
                      },
                      "items": {
                        "nodes": [
                          { "id": "item-1", "content": { "number": 42 } }
                        ],
                        "pageInfo": {
                          "hasNextPage": false,
                          "endCursor": null
                        }
                      }
                    }
                  }
                }
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, metadata);
            }

            var mutation = "{\"data\":{\"updateProjectV2ItemFieldValue\":{\"projectV2Item\":{\"id\":\"item-1\"}}}}";
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK, mutation);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        var updates = new[]
        {
            new ProjectFieldUpdate("AI", "running"),
            new ProjectFieldUpdate("Run ID", "run-123")
        };

        await client.UpdateProjectFields("octo/demo", "proj-1", 42, updates);

        Assert.Equal(3, handler.Requests.Count(request => request.RequestUri?.AbsolutePath == "/graphql"));
    }

    [Fact]
    public async Task UpdateProjectFields_PaginatesItemsUntilFound()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount += 1;
            if (callCount == 1)
            {
                var page1 = """
                {
                  "data": {
                    "node": {
                      "fields": {
                        "nodes": [
                          { "__typename": "ProjectV2SingleSelectField", "id": "field-ai", "name": "AI", "options": [ { "id": "opt-running", "name": "running" } ] }
                        ]
                      },
                      "items": {
                        "nodes": [
                          { "id": "item-1", "content": { "number": 1 } }
                        ],
                        "pageInfo": {
                          "hasNextPage": true,
                          "endCursor": "cursor-1"
                        }
                      }
                    }
                  }
                }
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, page1);
            }

            if (callCount == 2)
            {
                var page2 = """
                {
                  "data": {
                    "node": {
                      "fields": {
                        "nodes": [
                          { "__typename": "ProjectV2SingleSelectField", "id": "field-ai", "name": "AI", "options": [ { "id": "opt-running", "name": "running" } ] }
                        ]
                      },
                      "items": {
                        "nodes": [
                          { "id": "item-2", "content": { "number": 42 } }
                        ],
                        "pageInfo": {
                          "hasNextPage": false,
                          "endCursor": null
                        }
                      }
                    }
                  }
                }
                """;
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, page2);
            }

            var mutation = "{\"data\":{\"updateProjectV2ItemFieldValue\":{\"projectV2Item\":{\"id\":\"item-2\"}}}}";
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK, mutation);
        });

        var client = CreateClient(handler, new FixedTokenProvider("token-123"));
        var updates = new[]
        {
            new ProjectFieldUpdate("AI", "running")
        };

        await client.UpdateProjectFields("octo/demo", "proj-1", 42, updates);

        Assert.Equal(3, handler.Requests.Count(request => request.RequestUri?.AbsolutePath == "/graphql"));

        var secondGraphQlIndex = handler.Requests
            .Select((request, index) => new { request, index })
            .Where(entry => entry.request.RequestUri?.AbsolutePath == "/graphql")
            .Skip(1)
            .Select(entry => entry.index)
            .Single();

        var payload = handler.RequestBodies[secondGraphQlIndex] ?? string.Empty;
        using var document = JsonDocument.Parse(payload);
        var cursor = document.RootElement.GetProperty("variables").GetProperty("itemsAfter").GetString();
        Assert.Equal("cursor-1", cursor);
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
