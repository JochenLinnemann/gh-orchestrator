using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GhOrchestrator.Core;

namespace GhOrchestrator.Host;

public sealed class GitHubClient : IGitHubClient
{
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

            // REST API: update project item field
            var org = repository.Split('/')[0];
            var path = $"orgs/{org}/projectsV2/items/{metadata.ItemId}/fields/{field.Id}";
            using var request = await CreateRequest(new HttpMethod("PATCH"), repository, path, cancellationToken);

            // Build field value payload based on field type
            object payload;
            if (field.Options.Count == 0)
            {
                // Text field
                payload = new { text = update.Value };
            }
            else
            {
                // Single select field - find option ID
                var option = field.Options.FirstOrDefault(opt =>
                    string.Equals(opt.Name, update.Value, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                    throw new InvalidOperationException($"Project field option not found for {field.Name}: {update.Value}");

                payload = new { option_id = option.Id };
            }

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccess(response, cancellationToken);
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
        // REST API approach: list project items and find the one matching our issue
        // projectId is expected to be the project number (e.g., "1" from the URL)
        var org = repository.Split('/')[0];
        string? cursor = null;
        List<ProjectField>? fields = null;
        string? projectNodeId = null;

        while (true)
        {
            // GitHub's REST API for projects v2 items
            var path = $"orgs/{org}/projectsV2/{projectId}/items";
            if (cursor is not null)
                path += $"?per_page=100&page={cursor}";
            else
                path += "?per_page=100";

            Console.WriteLine($"[DEBUG] Calling REST API: GET {path}");
            using var request = await CreateRequest(HttpMethod.Get, repository, path, cancellationToken);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
            Console.WriteLine($"[DEBUG] Response Content-Type: {response.Content.Headers.ContentType}");
            Console.WriteLine($"[DEBUG] Full response body length: {responseBody.Length} chars");
            Console.WriteLine($"[DEBUG] Full response body: {responseBody}");
            
            // If we get 404, it might be that we need to use the node ID instead
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound && projectNodeId is null)
            {
                Console.WriteLine($"[DEBUG] Got 404 on {path}, trying to fetch project node ID from fields endpoint");
                
                // Get the node ID from the fields endpoint
                var fieldsPath = $"orgs/{org}/projectsV2/{projectId}/fields";
                using var fieldsRequest = await CreateRequest(HttpMethod.Get, repository, fieldsPath, cancellationToken);
                using var fieldsResponse = await _httpClient.SendAsync(fieldsRequest, cancellationToken);
                
                if (fieldsResponse.IsSuccessStatusCode)
                {
                    var fieldsJson = await fieldsResponse.Content.ReadAsStringAsync(cancellationToken);
                    using var fieldsDoc = JsonDocument.Parse(fieldsJson);
                    var fieldsRoot = fieldsDoc.RootElement;
                    
                    if (fieldsRoot.ValueKind == JsonValueKind.Array && fieldsRoot.GetArrayLength() > 0)
                    {
                        var firstField = fieldsRoot[0];
                        if (firstField.TryGetProperty("project_url", out var projectUrlElement))
                        {
                            var projectUrl = projectUrlElement.GetString();
                            // Extract node ID from project_url if available
                            if (!string.IsNullOrEmpty(projectUrl))
                            {
                                Console.WriteLine($"[DEBUG] Found project_url: {projectUrl}");
                            }
                        }
                    }
                }
                
                // Continue with 404 response to avoid infinite loop
            }
            
            Console.WriteLine($"[DEBUG] Full response body: {responseBody}");
            var bodyPreview = responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody;
            Console.WriteLine($"[DEBUG] Response body preview: {bodyPreview}");
            
            await EnsureSuccess(response, cancellationToken);

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            // Parse fields from first page only
            if (fields is null)
            {
                fields = await GetProjectFields(repository, org, projectId, cancellationToken);
            }

            // REST API returns array of items directly
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemElement in root.EnumerateArray())
                {
                    // Get node_id (not id, which is numeric)
                    if (!itemElement.TryGetProperty("node_id", out var nodeIdElement))
                        continue;
                    
                    var itemId = nodeIdElement.GetString();
                    if (string.IsNullOrWhiteSpace(itemId))
                        continue;

                    // Check if this item's content is our issue
                    if (itemElement.TryGetProperty("content_type", out var typeElement) &&
                        typeElement.GetString() == "Issue" &&
                        itemElement.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("number", out var numberElement) &&
                        numberElement.GetInt32() == issueNumber)
                    {
                        return new ProjectMetadata(itemId, fields);
                    }
                }
            }

            // Check for pagination
            if (response.Headers.TryGetValues("Link", out var linkValues))
            {
                var linkHeader = linkValues.FirstOrDefault();
                if (linkHeader?.Contains("rel=\"next\"") == true)
                {
                    // Simple pagination increment
                    cursor = cursor is null ? "2" : (int.Parse(cursor) + 1).ToString();
                    continue;
                }
            }

            break;
        }

        throw new InvalidOperationException($"Project item for issue {issueNumber} not found in project {projectId}");
    }

    private async Task<List<ProjectField>> GetProjectFields(
        string repository,
        string org,
        string projectId,
        CancellationToken cancellationToken)
    {
        var path = $"orgs/{org}/projectsV2/{projectId}/fields";
        Console.WriteLine($"[DEBUG] Calling REST API: GET {path}");
        using var request = await CreateRequest(HttpMethod.Get, repository, path, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine($"[DEBUG] Fields response status: {response.StatusCode}");
        var bodyPreview = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
        Console.WriteLine($"[DEBUG] Fields response body: {bodyPreview}");
        
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var fields = new List<ProjectField>();
        
        // REST API returns array directly, not wrapped in object
        var fieldsArray = root.ValueKind == JsonValueKind.Array 
            ? root 
            : root.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : root;

        if (fieldsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var fieldElement in fieldsArray.EnumerateArray())
            {
                // REST API returns id as number, convert to string
                var id = fieldElement.GetProperty("id").ValueKind == JsonValueKind.Number
                    ? fieldElement.GetProperty("id").GetInt64().ToString()
                    : fieldElement.GetProperty("id").GetString() ?? string.Empty;
                    
                var name = fieldElement.GetProperty("name").GetString() ?? string.Empty;
                var dataType = fieldElement.TryGetProperty("data_type", out var dtElement)
                    ? dtElement.GetString()
                    : null;

                var options = new List<ProjectFieldOption>();
                if (fieldElement.TryGetProperty("options", out var optionsElement) &&
                    optionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var optionElement in optionsElement.EnumerateArray())
                    {
                        // Option IDs can also be numbers
                        string optionId;
                        if (optionElement.TryGetProperty("id", out var idProp))
                        {
                            optionId = idProp.ValueKind == JsonValueKind.Number
                                ? idProp.GetInt64().ToString()
                                : idProp.GetString() ?? string.Empty;
                        }
                        else
                        {
                            continue; // Skip options without ID
                        }

                        // Name might be nested or missing
                        string optionName;
                        if (optionElement.TryGetProperty("name", out var nameProp))
                        {
                            if (nameProp.ValueKind == JsonValueKind.String)
                            {
                                optionName = nameProp.GetString() ?? string.Empty;
                            }
                            else if (nameProp.ValueKind == JsonValueKind.Object)
                            {
                                // Name might be nested in an object - try to extract text field
                                optionName = nameProp.TryGetProperty("text", out var textProp)
                                    ? textProp.GetString() ?? string.Empty
                                    : optionId; // Fallback to ID
                            }
                            else
                            {
                                optionName = string.Empty;
                            }
                        }
                        else
                        {
                            optionName = optionId; // Fallback to ID if name is missing
                        }

                        options.Add(new ProjectFieldOption(optionId, optionName));
                    }
                }

                // Map REST data_type to GraphQL-style typename for compatibility
                var typeName = dataType switch
                {
                    "single_select" => "ProjectV2SingleSelectField",
                    "text" => "ProjectV2Field",
                    _ => "ProjectV2Field"
                };

                fields.Add(new ProjectField(id, name, typeName, options));
            }
        }

        return fields;
    }

    private async Task<Dictionary<string, string?>> GetProjectItemFieldValues(
        string repository,
        string itemId,
        CancellationToken cancellationToken)
    {
        // REST API: get specific project item
        var path = $"projects/items/{itemId}";
        using var request = await CreateRequest(HttpMethod.Get, repository, path, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return ParseRestFieldValues(root);
    }

    private static Dictionary<string, string?> ParseRestFieldValues(JsonElement itemElement)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!itemElement.TryGetProperty("field_values", out var fieldValuesElement))
            return values;

        foreach (var fieldProp in fieldValuesElement.EnumerateObject())
        {
            var fieldName = fieldProp.Name;
            var valueElement = fieldProp.Value;

            string? value = null;
            if (valueElement.TryGetProperty("text", out var textElement))
                value = textElement.GetString();
            else if (valueElement.TryGetProperty("name", out var nameElement))
                value = nameElement.GetString();

            values[fieldName] = value;
        }

        return values;
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
        
        var fullUrl = _httpClient.BaseAddress?.AbsoluteUri.TrimEnd('/') + "/" + path.TrimStart('/');
        Console.WriteLine($"[DEBUG] Full request URL: {method} {fullUrl}");
        Console.WriteLine($"[DEBUG] Token (first 20 chars): {token.Substring(0, Math.Min(20, token.Length))}...");
        Console.WriteLine($"[DEBUG] Request Headers: Accept={string.Join(",", request.Headers.Accept.Select(h => h.MediaType))}, Auth=Bearer {token.Substring(0, 10)}...");
        
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

    private sealed record ProjectField(string Id, string Name, string? TypeName, List<ProjectFieldOption> Options);

    private sealed record ProjectFieldOption(string Id, string Name);
}
