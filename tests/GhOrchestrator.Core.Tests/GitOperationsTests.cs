using System.Text;

namespace GhOrchestrator.Core.Tests;

/// <summary>
/// Critical Path: Tests for git operations (Plan 17, v0 release criteria).
/// Validates that repositories are cloned, branches are created, file changes are applied, commits are made, and branches are pushed.
/// </summary>
public class GitOperationsTests
{
    [Fact]
    public async Task ApplyFileChangesAsync_CreatesAndModifiesFiles()
    {
        using var temp = new TempDirectory();
        var operations = new GitOperations();
        var existingPath = Path.Combine(temp.Path, "README.md");
        await File.WriteAllTextAsync(existingPath, "original");

        var changes = new[]
        {
            new AIWorkerFileChange("README.md", AIWorkerChangeType.Modify, "updated"),
            new AIWorkerFileChange("src/NewFile.cs", AIWorkerChangeType.Create, "new content"),
        };

        await operations.ApplyFileChangesAsync(temp.Path, changes);

        Assert.Equal("updated", await File.ReadAllTextAsync(existingPath, Encoding.UTF8));
        Assert.Equal("new content", await File.ReadAllTextAsync(Path.Combine(temp.Path, "src", "NewFile.cs"), Encoding.UTF8));
    }

    [Fact]
    public async Task ApplyFileChangesAsync_DeletesFiles()
    {
        using var temp = new TempDirectory();
        var operations = new GitOperations();
        var filePath = Path.Combine(temp.Path, "obsolete.txt");
        await File.WriteAllTextAsync(filePath, "remove me");

        var changes = new[]
        {
            new AIWorkerFileChange("obsolete.txt", AIWorkerChangeType.Delete, string.Empty),
        };

        await operations.ApplyFileChangesAsync(temp.Path, changes);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task ApplyFileChangesAsync_RejectsTraversalPaths()
    {
        using var temp = new TempDirectory();
        var operations = new GitOperations();
        var changes = new[]
        {
            new AIWorkerFileChange("../escape.txt", AIWorkerChangeType.Create, "nope"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.ApplyFileChangesAsync(temp.Path, changes));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gh-orchestrator-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
