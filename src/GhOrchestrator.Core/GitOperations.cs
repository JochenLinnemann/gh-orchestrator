using System.Diagnostics;

namespace GhOrchestrator.Core;

public class GitOperations : IGitOperations
{
    private const string AiAttribution = "Attribution: AI-generated changes";

    public async Task CloneRepositoryAsync(
        string repositoryUrl,
        string destinationPath,
        string? branch = null,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
            throw new ArgumentException("Repository URL is required", nameof(repositoryUrl));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required", nameof(destinationPath));

        var arguments = new List<string>
        {
            "clone",
            "--depth",
            "1",
        };

        if (!string.IsNullOrWhiteSpace(branch))
        {
            arguments.Add("--branch");
            arguments.Add(branch);
        }

        arguments.Add(BuildAuthenticatedUrl(repositoryUrl, accessToken));
        arguments.Add(destinationPath);

        await RunGitAsync(null, arguments, cancellationToken);
    }

    public async Task FetchAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path is required", nameof(repositoryPath));

        await RunGitAsync(repositoryPath, new[] { "fetch", "--prune" }, cancellationToken);
    }

    public async Task CheckoutBranchAsync(
        string repositoryPath,
        string branchName,
        string baseBranch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path is required", nameof(repositoryPath));
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required", nameof(branchName));
        if (string.IsNullOrWhiteSpace(baseBranch))
            throw new ArgumentException("Base branch is required", nameof(baseBranch));

        await RunGitAsync(repositoryPath, new[] { "checkout", "-B", branchName, $"origin/{baseBranch}" }, cancellationToken);
    }

    public Task ApplyFileChangesAsync(
        string repositoryPath,
        IEnumerable<AIWorkerFileChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path is required", nameof(repositoryPath));
        if (changes is null)
            throw new ArgumentNullException(nameof(changes));

        var repositoryRoot = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(repositoryRoot))
            throw new DirectoryNotFoundException($"Repository path does not exist: {repositoryRoot}");

        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(change.Path))
                throw new ArgumentException("File change path is required", nameof(changes));

            if (Path.IsPathRooted(change.Path))
                throw new InvalidOperationException("Absolute paths are not allowed in file changes.");

            var targetPath = Path.GetFullPath(Path.Combine(repositoryRoot, change.Path));
            if (!IsWithinRepository(repositoryRoot, targetPath))
                throw new InvalidOperationException("File change path escapes the repository root.");

            // Block symlink escapes: if parent directories or the file itself are symlinks,
            // verify the resolved path is still within the repository boundary
            var resolvedPath = ResolvePath(targetPath);
            if (!IsWithinRepository(repositoryRoot, resolvedPath))
                throw new InvalidOperationException("File change path follows symlink that escapes the repository root.");

            switch (change.ChangeType)
            {
                case AIWorkerChangeType.Create:
                case AIWorkerChangeType.Modify:
                    var directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        Directory.CreateDirectory(directory);

                    File.WriteAllText(targetPath, change.Content ?? string.Empty);
                    break;
                case AIWorkerChangeType.Delete:
                    if (Directory.Exists(targetPath))
                        throw new InvalidOperationException("File change path points to a directory.");

                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported change type: {change.ChangeType}");
            }
        }

        return Task.CompletedTask;
    }

    public async Task CommitAsync(
        string repositoryPath,
        string runId,
        string authorName,
        string authorEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path is required", nameof(repositoryPath));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required", nameof(runId));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required", nameof(authorName));
        if (string.IsNullOrWhiteSpace(authorEmail))
            throw new ArgumentException("Author email is required", nameof(authorEmail));

        await RunGitAsync(repositoryPath, new[] { "add", "-A" }, cancellationToken);

        var subject = $"AI: Apply changes for {runId}";
        var body = $"Run: {runId}\nAuthor: {authorName} <{authorEmail}>\n{AiAttribution}";

        var arguments = new List<string>
        {
            "-c",
            $"user.name={authorName}",
            "-c",
            $"user.email={authorEmail}",
            "commit",
            "-m",
            subject,
            "-m",
            body,
        };

        await RunGitAsync(repositoryPath, arguments, cancellationToken);
    }

    public async Task PushAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            throw new ArgumentException("Repository path is required", nameof(repositoryPath));
        if (string.IsNullOrWhiteSpace(branchName))
            throw new ArgumentException("Branch name is required", nameof(branchName));

        await RunGitAsync(repositoryPath, new[] { "push", "origin", branchName }, cancellationToken);
    }

    private static string BuildAuthenticatedUrl(string repositoryUrl, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return repositoryUrl;

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
            return repositoryUrl;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return repositoryUrl;

        var builder = new UriBuilder(uri)
        {
            UserName = "x-access-token",
            Password = accessToken,
        };

        return builder.Uri.ToString();
    }

    private static bool IsWithinRepository(string repositoryRoot, string targetPath)
    {
        var normalizedRoot = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return targetPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }

    private static string ResolvePath(string path)
    {
        // Resolve symlinks by getting the target if the file/directory exists and is a link
        // If the path doesn't exist yet (create operation), resolve parent directories only
        if (File.Exists(path) || Directory.Exists(path))
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.LinkTarget is not null 
                ? Path.GetFullPath(fileInfo.LinkTarget) 
                : Path.GetFullPath(path);
        }

        // For non-existent paths, check if any parent directory is a symlink
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var dirInfo = new DirectoryInfo(directory);
            if (dirInfo.LinkTarget is not null)
            {
                // Parent is a symlink; resolve it and reconstruct the full path
                var resolvedParent = Path.GetFullPath(dirInfo.LinkTarget);
                var fileName = Path.GetFileName(path);
                return Path.GetFullPath(Path.Combine(resolvedParent, fileName));
            }
        }

        return Path.GetFullPath(path);
    }

    private static async Task RunGitAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (arguments is null)
            throw new ArgumentNullException(nameof(arguments));

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stdoutTask, stderrTask);

        if (process.ExitCode != 0)
            throw new InvalidOperationException("Git command failed.");
    }
}
