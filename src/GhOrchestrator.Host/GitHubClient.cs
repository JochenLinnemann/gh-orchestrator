using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GhOrchestrator.Core;

namespace GhOrchestrator.Host;

public sealed class GitHubClient : IGitHubClient
{
    private const string GraphQlEndpoint = "graphql";
    private readonly HttpClient _httpClient;
    private readonly IGitHubInstallationTokenProvider _tokenProvider;

    public GitHubClient(HttpClient httpClient, IGitHubInstallationTokenProvider tokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri("https://api.github.com/");

        EnsureUserAgent();
    }

    public async Task<GitHubIssue?> GetIssue(string repository, int issueNumber, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequest(HttpMethod.Get, repository, $"repos/{repository}/issues/{issueNumber}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var title = root.GetProperty("title").GetString() ?? string.Empty;
        var body = root.GetProperty("body").GetString() ?? string.Empty;
        var state = root.GetProperty("state").GetString();
        var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

        return new GitHubIssue(issueNumber, title, body, string.Equals(state, "open", StringComparison.OrdinalIgnoreCase), url);
    }

    public async Task AddIssueComment(string repository, int issueNumber, string body, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { body });
        using var request = await CreateRequest(HttpMethod.Post, repository, $"repos/{repository}/issues/{issueNumber}/comments", cancellationToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task<ProjectTaskStateSnapshot> GetProjectTaskState(
        string repository,
        string projectId,
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project ID is required", nameof(projectId));

        Console.WriteLine($"[DEBUG] GetProjectTaskState: repository={repository}, projectId={projectId}, issueNumber={issueNumber}");

        var metadata = await GetProjectMetadata(repository, projectId, issueNumber, cancellationToken);
        var requiredFieldNames = new[]
        {
            ProjectFieldNames.Ai,
            ProjectFieldNames.Status,
            ProjectFieldNames.RunId
        };

        var missingFields = requiredFieldNames
            .Where(required => metadata.Fields.All(field => !string.Equals(field.Name, required, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var fieldValues = await GetProjectItemFieldValues(repository, metadata.ItemId, cancellationToken);

        fieldValues.TryGetValue(ProjectFieldNames.Ai, out var aiStatus);
        fieldValues.TryGetValue(ProjectFieldNames.Status, out var status);
        fieldValues.TryGetValue(ProjectFieldNames.RunId, out var runId);

        var state = new ProjectTaskState(aiStatus, status, runId);
        return new ProjectTaskStateSnapshot(state, missingFields);
    }

    public async Task UpdateProjectFields(
        string repository,
        string projectId,
        int issueNumber,
        IReadOnlyCollection<ProjectFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project ID is required", nameof(projectId));
        if (updates is null)
            throw new ArgumentNullException(nameof(updates));
        if (updates.Count == 0)
            return;

        var metadata = await GetProjectMetadata(repository, projectId, issueNumber, cancellationToken);

        foreach (var update in updates)
        {
            var field = metadata.Fields.FirstOrDefault(field => string.Equals(field.Name, update.FieldName, StringComparison.OrdinalIgnoreCase));
            if (field is null)
                throw new InvalidOperationException($"Project field not found: {update.FieldName}");

            var value = field.BuildValue(update.Value);
            var mutationRequest = new
            {
                query = ProjectFieldMutation,
                variables = new
                {
                    projectId,
                    itemId = metadata.ItemId,
                    fieldId = field.Id,
                    value
                }
            };

            using var mutationResponse = await SendGraphQl(repository, mutationRequest, cancellationToken);
        }
    }

    public async Task<string> GetDefaultBranch(string repository, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequest(HttpMethod.Get, repository, $"repos/{repository}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var branch = document.RootElement.GetProperty("default_branch").GetString();

        if (string.IsNullOrWhiteSpace(branch))
            throw new InvalidOperationException("Default branch not found");

        return branch;
    }

    public async Task CreateBranch(
        string repository,
        string newBranch,
        string baseBranch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newBranch))
            throw new ArgumentException("New branch is required", nameof(newBranch));
        if (string.IsNullOrWhiteSpace(baseBranch))
            throw new ArgumentException("Base branch is required", nameof(baseBranch));

        var baseSha = await GetBranchSha(repository, baseBranch, cancellationToken);
        var payload = JsonSerializer.Serialize(new { @ref = $"refs/heads/{newBranch}", sha = baseSha });

        using var request = await CreateRequest(HttpMethod.Post, repository, $"repos/{repository}/git/refs", cancellationToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    public async Task<PullRequestLink> CreatePullRequest(
        string repository,
        PullRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var payload = JsonSerializer.Serialize(new
        {
            title = request.Title,
            body = request.Body,
            head = request.HeadBranch,
            @base = request.BaseBranch
        });

        using var message = await CreateRequest(HttpMethod.Post, repository, $"repos/{repository}/pulls", cancellationToken);
        message.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var url = document.RootElement.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Pull request URL not returned");

        return new PullRequestLink(repository, url);
    }

    private async Task<ProjectMetadata> GetProjectMetadata(
        string repository,
        string projectId,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        string? cursor = null;
        List<ProjectField>? fields = null;

        while (true)
        {
            var request = new
            {
                query = ProjectMetadataQuery,
                variables = new { projectId, itemsAfter = cursor }
            };

            using var response = await SendGraphQl(repository, request, cancellationToken);
            if (!response.RootElement.TryGetProperty("data", out var dataElement))
                throw new InvalidOperationException("GraphQL response missing data");

            if (!dataElement.TryGetProperty("node", out var nodeElement))
                throw new InvalidOperationException("GraphQL response missing project node");

            fields ??= ParseFields(nodeElement);
            var page = ParseItems(nodeElement);
            var itemId = TryFindProjectItemId(page.Items, issueNumber);
            if (itemId is not null)
                return new ProjectMetadata(itemId, fields);

            if (!page.PageInfo.HasNextPage)
                break;

            cursor = page.PageInfo.EndCursor;
        }

        throw new InvalidOperationException($"Project item for issue {issueNumber} not found");
    }

    private async Task<Dictionary<string, string?>> GetProjectItemFieldValues(
        string repository,
        string itemId,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            query = ProjectItemFieldValuesQuery,
            variables = new { itemId }
        };

        using var response = await SendGraphQl(repository, request, cancellationToken);
        if (!response.RootElement.TryGetProperty("data", out var dataElement))
            throw new InvalidOperationException("GraphQL response missing data");

        if (!dataElement.TryGetProperty("node", out var nodeElement))
            throw new InvalidOperationException("GraphQL response missing project item node");

        return ParseFieldValues(nodeElement);
    }

    private static List<ProjectField> ParseFields(JsonElement nodeElement)
    {
        if (!nodeElement.TryGetProperty("fields", out var fieldsElement))
            throw new InvalidOperationException("Project fields not found");

        if (!fieldsElement.TryGetProperty("nodes", out var nodesElement))
            throw new InvalidOperationException("Project field nodes not found");

        var fields = new List<ProjectField>();
        foreach (var fieldNode in nodesElement.EnumerateArray())
        {
            var name = fieldNode.GetProperty("name").GetString() ?? string.Empty;
            var id = fieldNode.GetProperty("id").GetString() ?? string.Empty;
            var typeName = fieldNode.GetProperty("__typename").GetString();
            var options = ParseOptions(fieldNode);

            fields.Add(new ProjectField(id, name, typeName, options));
        }

        return fields;
    }

    private static Dictionary<string, string?> ParseFieldValues(JsonElement nodeElement)
    {
        if (!nodeElement.TryGetProperty("fieldValues", out var valuesElement))
            throw new InvalidOperationException("Project item field values not found");

        if (!valuesElement.TryGetProperty("nodes", out var nodesElement))
            throw new InvalidOperationException("Project item field value nodes not found");

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var valueElement in nodesElement.EnumerateArray())
        {
            if (!valueElement.TryGetProperty("field", out var fieldElement))
                continue;

            if (!fieldElement.TryGetProperty("name", out var fieldNameElement))
                continue;

            var fieldName = fieldNameElement.GetString();
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;

            string? value = null;
            if (valueElement.TryGetProperty("text", out var textElement))
                value = textElement.GetString();
            else if (valueElement.TryGetProperty("name", out var nameElement))
                value = nameElement.GetString();

            values[fieldName] = value;
        }

        return values;
    }

    private static List<ProjectFieldOption> ParseOptions(JsonElement fieldNode)
    {
        if (!fieldNode.TryGetProperty("options", out var optionsElement))
            return new List<ProjectFieldOption>();

        var options = new List<ProjectFieldOption>();
        foreach (var option in optionsElement.EnumerateArray())
        {
            var id = option.GetProperty("id").GetString() ?? string.Empty;
            var name = option.GetProperty("name").GetString() ?? string.Empty;
            options.Add(new ProjectFieldOption(id, name));
        }

        return options;
    }

    private static ProjectItemPage ParseItems(JsonElement nodeElement)
    {
        if (!nodeElement.TryGetProperty("items", out var itemsElement))
            throw new InvalidOperationException("Project items not found");
        if (!itemsElement.TryGetProperty("nodes", out var nodesElement))
            throw new InvalidOperationException("Project item nodes not found");
        if (!itemsElement.TryGetProperty("pageInfo", out var pageInfoElement))
            throw new InvalidOperationException("Project item pageInfo not found");

        var items = new List<ProjectItem>();
        foreach (var itemElement in nodesElement.EnumerateArray())
        {
            var id = itemElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Project item id missing");

            int? issueNumber = null;
            if (itemElement.TryGetProperty("content", out var content) &&
                content.TryGetProperty("number", out var numberElement))
            {
                issueNumber = numberElement.GetInt32();
            }

            items.Add(new ProjectItem(id, issueNumber));
        }

        var pageInfo = new ProjectPageInfo(
            pageInfoElement.GetProperty("hasNextPage").GetBoolean(),
            pageInfoElement.TryGetProperty("endCursor", out var endCursorElement)
                ? endCursorElement.GetString()
                : null);

        return new ProjectItemPage(items, pageInfo);
    }

    private static string? TryFindProjectItemId(IEnumerable<ProjectItem> items, int issueNumber)
    {
        foreach (var item in items)
        {
            if (item.IssueNumber == issueNumber)
                return item.Id;
        }

        return null;
    }

    private async Task<string> GetBranchSha(string repository, string baseBranch, CancellationToken cancellationToken)
    {
        using var request = await CreateRequest(HttpMethod.Get, repository, $"repos/{repository}/git/ref/heads/{baseBranch}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var sha = document.RootElement.GetProperty("object").GetProperty("sha").GetString();
        if (string.IsNullOrWhiteSpace(sha))
            throw new InvalidOperationException("Base branch SHA not found");

        return sha;
    }

    private async Task<JsonDocument> SendGraphQl(string repository, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        Console.WriteLine($"[DEBUG] GraphQL request: {json}");
        
        using var request = await CreateRequest(HttpMethod.Post, repository, GraphQlEndpoint, cancellationToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"[DEBUG] GraphQL response: {responseBody}");
        
        var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new InvalidOperationException($"GraphQL request failed: {errors}");

        return document;
    }

    private async Task<HttpRequestMessage> CreateRequest(
        HttpMethod method,
        string repository,
        string path,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetInstallationToken(repository, cancellationToken);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return request;
    }

    private void EnsureUserAgent()
    {
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GhOrchestrator", "0.1"));
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"GitHub API request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
    }

    private sealed record ProjectMetadata(string ItemId, List<ProjectField> Fields);

    private sealed record ProjectItem(string Id, int? IssueNumber);

    private sealed record ProjectPageInfo(bool HasNextPage, string? EndCursor);

    private sealed record ProjectItemPage(List<ProjectItem> Items, ProjectPageInfo PageInfo);

    private sealed record ProjectField(string Id, string Name, string? TypeName, List<ProjectFieldOption> Options)
    {
        public object BuildValue(string value)
        {
            if (Options.Count == 0)
                return new { text = value };

            var option = Options.FirstOrDefault(option => string.Equals(option.Name, value, StringComparison.OrdinalIgnoreCase));
            if (option is null)
                throw new InvalidOperationException($"Project field option not found for {Name}: {value}");

            return new { singleSelectOptionId = option.Id };
        }
    }

    private sealed record ProjectFieldOption(string Id, string Name);

    private const string ProjectMetadataQuery = @"
query($projectId: ID!, $itemsAfter: String) {
  node(id: $projectId) {
    ... on ProjectV2 {
      fields(first: 100) {
        nodes {
          __typename
          ... on ProjectV2Field {
            id
            name
          }
          ... on ProjectV2SingleSelectField {
            id
            name
            options {
              id
              name
            }
          }
        }
      }
      items(first: 100, after: $itemsAfter) {
        nodes {
          id
          content {
            ... on Issue {
              number
            }
          }
        }
        pageInfo {
          hasNextPage
          endCursor
        }
      }
    }
  }
}
";

    private const string ProjectFieldMutation = @"
mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $value: ProjectV2FieldValue!) {
  updateProjectV2ItemFieldValue(
    input: { projectId: $projectId, itemId: $itemId, fieldId: $fieldId, value: $value }
  ) {
    projectV2Item {
      id
    }
  }
}
";

    private const string ProjectItemFieldValuesQuery = @"
query($itemId: ID!) {
  node(id: $itemId) {
    ... on ProjectV2Item {
      fieldValues(first: 100) {
        nodes {
          ... on ProjectV2ItemFieldTextValue {
            text
            field {
              ... on ProjectV2FieldCommon {
                name
              }
            }
          }
          ... on ProjectV2ItemFieldSingleSelectValue {
            name
            field {
              ... on ProjectV2FieldCommon {
                name
              }
            }
          }
        }
      }
    }
  }
}
";
}
