using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GhOrchestrator.Core;

namespace GhOrchestrator.Host;

public sealed class GitHubClient : IGitHubClient
{
    private readonly HttpClient _httpClient;
    private readonly IGitHubInstallationTokenProvider _tokenProvider;
    private readonly GitHubProjectClient _projectClient;

    public GitHubClient(HttpClient httpClient, IGitHubInstallationTokenProvider tokenProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri("https://api.github.com/");

        EnsureUserAgent();

        _projectClient = new GitHubProjectClient(_httpClient, _tokenProvider.GetInstallationToken);
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

        return await _projectClient.GetProjectTaskState(repository, projectId, issueNumber, cancellationToken);
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

        await _projectClient.UpdateProjectFields(repository, projectId, issueNumber, updates, cancellationToken);
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

    public async Task<string> GetRepositoryCloneUrl(string repository, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequest(HttpMethod.Get, repository, $"repos/{repository}", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var cloneUrl = document.RootElement.GetProperty("clone_url").GetString();

        if (string.IsNullOrWhiteSpace(cloneUrl))
            throw new InvalidOperationException("Clone URL not found");

        return cloneUrl;
    }

    public Task<string> GetRepositoryAccessToken(string repository, CancellationToken cancellationToken = default)
    {
        return _tokenProvider.GetInstallationToken(repository, cancellationToken);
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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.graphql-preview+json"));
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
}
