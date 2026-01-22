namespace GhOrchestrator.Core.Tests;

public class OpenAIWorkerResponseParserTests
{
    [Fact]
    public void Parse_ValidResponse_ReturnsRepoResults()
    {
        var response = """
            {
              "repoResults": [
                {
                  "repository": "org/service-a",
                  "summary": "Updated logging",
                  "changes": [
                    {
                      "path": "src/Logging.cs",
                      "changeType": "modify",
                      "content": "// updated content"
                    }
                  ]
                }
              ]
            }
            """;

        var result = OpenAIWorkerResponseParser.Parse(response, new[] { "org/service-a" });

        Assert.Single(result.RepoResults);
        Assert.True(result.RepoResults[0].IsSuccess);
        Assert.Equal("org/service-a", result.RepoResults[0].Repository);
        Assert.Equal("Updated logging", result.RepoResults[0].ExecutionLog);
        Assert.Equal("src/Logging.cs", result.RepoResults[0].FileChanges[0].Path);
        Assert.Equal(AIWorkerChangeType.Modify, result.RepoResults[0].FileChanges[0].ChangeType);
    }

    [Fact]
    public void Parse_MissingRepository_ReturnsFailureResult()
    {
        var response = """
            {
              "repoResults": []
            }
            """;

        var result = OpenAIWorkerResponseParser.Parse(response, new[] { "org/service-a" });

        Assert.Single(result.RepoResults);
        Assert.False(result.RepoResults[0].IsSuccess);
        Assert.Equal("No response returned for repository.", result.RepoResults[0].FailureReason);
    }

    [Fact]
    public void Parse_InvalidChangeType_Throws()
    {
        var response = """
            {
              "repoResults": [
                {
                  "repository": "org/service-a",
                  "summary": "Bad change type",
                  "changes": [
                    {
                      "path": "src/Logging.cs",
                      "changeType": "rename",
                      "content": "// updated content"
                    }
                  ]
                }
              ]
            }
            """;

        Assert.Throws<FormatException>(() => OpenAIWorkerResponseParser.Parse(response, new[] { "org/service-a" }));
    }
}
