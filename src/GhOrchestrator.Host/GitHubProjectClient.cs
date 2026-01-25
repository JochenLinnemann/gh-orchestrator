using System.Text;
using System.Text.Json;
using GhOrchestrator.Core;

namespace GhOrchestrator.Host;

/// <summary>
/// Handles GitHub Projects v2 REST API interactions.
/// </summary>
internal sealed class GitHubProjectClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<string, CancellationToken, Task<string>> _getToken;

    public GitHubProjectClient(HttpClient httpClient, Func<string, CancellationToken, Task<string>> getToken)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _getToken = getToken ?? throw new ArgumentNullException(nameof(getToken));
    }

    public async Task<ProjectTaskStateSnapshot> GetProjectTaskState(
        string repository,
        string projectId,
        int issueNumber,
        CancellationToken cancellationToken)
    {
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

        var fieldValues = await GetProjectItemFieldValues(repository, projectId, metadata.ItemId, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0)
            return;

        var metadata = await GetProjectMetadata(repository, projectId, issueNumber, cancellationToken);
        var fieldsToUpdate = new List<object>();

        foreach (var update in updates)
        {
            var field = metadata.Fields.FirstOrDefault(field => string.Equals(field.Name, update.FieldName, StringComparison.OrdinalIgnoreCase));
            if (field is null)
                throw new InvalidOperationException($"Project field not found: {update.FieldName}");

            object fieldValue;
            if (field.Options.Count == 0)
            {
                fieldValue = update.Value;
            }
            else
            {
                var option = field.Options.FirstOrDefault(opt =>
                    string.Equals(opt.Name, update.Value, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                    throw new InvalidOperationException($"Project field option not found for {field.Name}: {update.Value}");

                fieldValue = option.Id;
            }

            if (int.TryParse(field.Id, out var fieldIdInt))
            {
                fieldsToUpdate.Add(new { id = fieldIdInt, value = fieldValue });
            }
            else
            {
                fieldsToUpdate.Add(new { id = field.Id, value = fieldValue });
            }
        }

        var org = repository.Split('/')[0];
        var path = $"orgs/{org}/projectsV2/{projectId}/items/{metadata.ItemId}";
        var payload = new { fields = fieldsToUpdate };
        var json = JsonSerializer.Serialize(payload);

        // TODO: Remove debug logging
        Console.WriteLine($"[DEBUG] UpdateProjectFields: PATCH {path}");
        Console.WriteLine($"[DEBUG] UpdateProjectFields payload: {json}");

        await SendRequest(repository, new HttpMethod("PATCH"), path, json, cancellationToken);
        
        Console.WriteLine($"[DEBUG] UpdateProjectFields: Update completed successfully");
    }

    private async Task<ProjectMetadata> GetProjectMetadata(
        string repository,
        string projectId,
        int issueNumber,
        CancellationToken cancellationToken)
    {
        var org = repository.Split('/')[0];
        string? cursor = null;
        List<ProjectField>? fields = null;

        while (true)
        {
            var path = $"orgs/{org}/projectsV2/{projectId}/items";
            if (cursor is not null)
                path += $"?per_page=100&page={cursor}";
            else
                path += "?per_page=100";

            var (responseBody, hasNextPage) = await SendRequestWithPagination(repository, HttpMethod.Get, path, null, cancellationToken);
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            fields ??= await GetProjectFields(repository, org, projectId, cancellationToken);

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemElement in root.EnumerateArray())
                {
                    string? itemId = null;
                    string? itemNodeId = null;

                    if (itemElement.TryGetProperty("id", out var idElement))
                    {
                        itemId = idElement.ValueKind switch
                        {
                            JsonValueKind.Number => idElement.GetInt64().ToString(),
                            JsonValueKind.String => idElement.GetString(),
                            _ => null
                        };
                    }

                    if (itemElement.TryGetProperty("node_id", out var nodeIdElement))
                        itemNodeId = nodeIdElement.GetString();

                    itemId ??= itemNodeId;
                    if (string.IsNullOrWhiteSpace(itemId))
                        continue;

                    if (itemElement.TryGetProperty("content_type", out var typeElement) &&
                        typeElement.GetString() == "Issue" &&
                        itemElement.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("number", out var numberElement) &&
                        numberElement.GetInt32() == issueNumber)
                    {
                        return new ProjectMetadata(itemId, itemNodeId, fields);
                    }
                }
            }

            if (hasNextPage)
            {
                cursor = cursor is null ? "2" : (int.Parse(cursor) + 1).ToString();
                continue;
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
        var json = await SendRequest(repository, HttpMethod.Get, path, null, cancellationToken);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var fields = new List<ProjectField>();
        var fieldsArray = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("fields", out var fieldsElement) ? fieldsElement : root;

        if (fieldsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var fieldElement in fieldsArray.EnumerateArray())
            {
                var id = fieldElement.GetProperty("id").ValueKind == JsonValueKind.Number
                    ? fieldElement.GetProperty("id").GetInt64().ToString()
                    : fieldElement.GetProperty("id").GetString() ?? string.Empty;

                var name = fieldElement.GetProperty("name").GetString() ?? string.Empty;
                var dataType = fieldElement.TryGetProperty("data_type", out var dtElement)
                    ? dtElement.GetString()
                    : null;

                var options = ParseFieldOptions(fieldElement);

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

    private static List<ProjectFieldOption> ParseFieldOptions(JsonElement fieldElement)
    {
        var options = new List<ProjectFieldOption>();

        if (!fieldElement.TryGetProperty("options", out var optionsElement) ||
            optionsElement.ValueKind != JsonValueKind.Array)
            return options;

        foreach (var optionElement in optionsElement.EnumerateArray())
        {
            if (!optionElement.TryGetProperty("id", out var idProp))
                continue;

            var optionId = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt64().ToString()
                : idProp.GetString() ?? string.Empty;

            string optionName = optionId;
            if (optionElement.TryGetProperty("name", out var nameProp))
            {
                optionName = nameProp.ValueKind switch
                {
                    JsonValueKind.String => nameProp.GetString() ?? optionId,
                    JsonValueKind.Object when nameProp.TryGetProperty("raw", out var rawProp) =>
                        rawProp.GetString() ?? optionId,
                    JsonValueKind.Object when nameProp.TryGetProperty("text", out var textProp) =>
                        textProp.GetString() ?? optionId,
                    _ => optionId
                };
            }

            options.Add(new ProjectFieldOption(optionId, optionName));
        }

        return options;
    }

    private async Task<Dictionary<string, string?>> GetProjectItemFieldValues(
        string repository,
        string projectId,
        string itemId,
        CancellationToken cancellationToken)
    {
        var org = repository.Split('/')[0];
        var fields = await GetProjectFields(repository, org, projectId, cancellationToken);
        var fieldIdToName = fields.ToDictionary(f => f.Id, f => f.Name, StringComparer.OrdinalIgnoreCase);

        var path = $"orgs/{org}/projectsV2/{projectId}/items/{itemId}";
        var json = await SendRequest(repository, HttpMethod.Get, path, null, cancellationToken);

        // TODO: Remove debug logging after diagnosing field value parsing
        Console.WriteLine($"[DEBUG] GetProjectItemFieldValues response: {json.Substring(0, Math.Min(1000, json.Length))}");
        Console.WriteLine($"[DEBUG] Field ID to Name mappings: {string.Join(", ", fieldIdToName.Select(kv => $"{kv.Key}={kv.Value}"))}");

        using var document = JsonDocument.Parse(json);
        var result = ParseRestFieldValues(document.RootElement, fieldIdToName);

        Console.WriteLine($"[DEBUG] Parsed field values: {string.Join(", ", result.Select(kv => $"{kv.Key}={kv.Value ?? "(null)"}"))}");

        return result;
    }

    private static Dictionary<string, string?> ParseRestFieldValues(
        JsonElement itemElement,
        Dictionary<string, string> fieldIdToName)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // TODO: Remove debug logging after diagnosing field value parsing
        Console.WriteLine($"[DEBUG] Item JSON properties: {string.Join(", ", itemElement.EnumerateObject().Select(p => p.Name))}");

        if (!itemElement.TryGetProperty("fields", out var fieldsElement))
        {
            Console.WriteLine("[DEBUG] No 'fields' property found in item response");
            return values;
        }

        Console.WriteLine($"[DEBUG] fields type: {fieldsElement.ValueKind}");

        if (fieldsElement.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("[DEBUG] 'fields' is not an array");
            return values;
        }

        // Iterate over fields array
        var elementIndex = 0;
        foreach (var fieldElement in fieldsElement.EnumerateArray())
        {
            // TODO: Remove - dump more elements to find AI/Run ID fields
            if (elementIndex < 20)
            {
                Console.WriteLine($"[DEBUG] Field element {elementIndex}: {fieldElement.GetRawText()}");
            }
            elementIndex++;

            // Extract field name directly (REST API returns name in each element)
            if (!fieldElement.TryGetProperty("name", out var nameElement))
                continue;

            var fieldName = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(fieldName))
                continue;

            // Extract value
            string? value = null;
            if (fieldElement.TryGetProperty("value", out var valueElement))
            {
                if (valueElement.ValueKind == JsonValueKind.String)
                {
                    value = valueElement.GetString();
                }
                else if (valueElement.ValueKind == JsonValueKind.Object)
                {
                    // Try raw, text, name, html in order
                    if (valueElement.TryGetProperty("raw", out var rawElement))
                        value = rawElement.GetString();
                    else if (valueElement.TryGetProperty("text", out var textElement))
                        value = textElement.GetString();
                    else if (valueElement.TryGetProperty("name", out var valueNameElement))
                        value = valueNameElement.GetString();
                    else if (valueElement.TryGetProperty("html", out var htmlElement))
                        value = htmlElement.GetString();
                }
            }

            Console.WriteLine($"[DEBUG] Parsed field: fieldName={fieldName}, value={value ?? "(null)"}");
            values[fieldName] = value;
        }

        return values;
    }

    private async Task<string> SendRequest(
        string repository,
        HttpMethod method,
        string path,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        var (body, _) = await SendRequestWithPagination(repository, method, path, jsonBody, cancellationToken);
        return body;
    }

    private async Task<(string Body, bool HasNextPage)> SendRequestWithPagination(
        string repository,
        HttpMethod method,
        string path,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        var token = await _getToken(repository, cancellationToken);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub API request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}");

        // Check for pagination
        var hasNextPage = false;
        if (response.Headers.TryGetValues("Link", out var linkValues))
        {
            var linkHeader = linkValues.FirstOrDefault();
            hasNextPage = linkHeader?.Contains("rel=\"next\"") == true;
        }

        return (responseBody, hasNextPage);
    }

    private sealed record ProjectMetadata(string ItemId, string? ItemNodeId, List<ProjectField> Fields);
    private sealed record ProjectField(string Id, string Name, string? TypeName, List<ProjectFieldOption> Options);
    private sealed record ProjectFieldOption(string Id, string Name);
}
