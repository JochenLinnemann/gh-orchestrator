namespace GhOrchestrator.Core.Tests;

public class CommandParserTests
{
    [Fact]
    public void ParseAiStartCommand_ValidCommand_ReturnsDescription()
    {
        var comment = "/ai start\nAdd logging to handler.py";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Equal("Add logging to handler.py", result);
    }

    [Fact]
    public void ParseAiStartCommand_WithExtraWhitespace_HandlesCorrectly()
    {
        var comment = "/ai start  \n  Refactor config loader  ";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Equal("Refactor config loader", result);
    }

    [Fact]
    public void ParseAiStartCommand_MultilineDescription_PreservesContent()
    {
        var comment = "/ai start\nAdd logging\n\n- To handler.py\n- Verbose mode";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Contains("Add logging", result);
        Assert.Contains("handler.py", result);
    }

    [Fact]
    public void ParseAiStartCommand_NoCommand_ReturnsNull()
    {
        var comment = "This is just a comment";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Null(result);
    }

    [Fact]
    public void ParseAiStartCommand_PartialCommand_ReturnsNull()
    {
        var comment = "/ai status";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Null(result);
    }

    [Fact]
    public void ParseRepositories_ValidSection_ReturnsRepos()
    {
        var body = @"
## Repositories
- org/service-a
- org/service-b
- org/service-c
";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(3, repos.Count);
        Assert.Contains("org/service-a", repos);
        Assert.Contains("org/service-b", repos);
        Assert.Contains("org/service-c", repos);
    }

    [Fact]
    public void ParseRepositories_WithOtherSections_ExtractsCorrectly()
    {
        var body = @"
## Context
Some context here.

## Repositories
- owner/repo1
- owner/repo2

## Acceptance Criteria
- It should work
";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(2, repos.Count);
        Assert.Contains("owner/repo1", repos);
        Assert.Contains("owner/repo2", repos);
    }

    [Fact]
    public void ParseRepositories_NoSection_ReturnsEmpty()
    {
        var body = "## Context\nSome text";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Empty(repos);
    }

    [Fact]
    public void ParseRepositories_InvalidFormat_SkipsInvalidLines()
    {
        var body = @"
## Repositories
- org/valid-repo
- invalid-format
- another/valid-repo
";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Contains("org/valid-repo", repos);
        Assert.Contains("another/valid-repo", repos);
        Assert.DoesNotContain("invalid-format", repos);
    }

    [Fact]
    public void ParseAcceptanceCriteria_SectionFormat_ReturnsContent()
    {
        var body = @"
## Acceptance Criteria
- Code compiles without errors
- Tests pass
- Documentation updated
";
        var criteria = CommandParser.ParseAcceptanceCriteria(body);
        
        Assert.NotNull(criteria);
        Assert.Contains("Code compiles", criteria);
        Assert.Contains("Tests pass", criteria);
    }

    [Fact]
    public void ParseAcceptanceCriteria_SingleLineFormat_ReturnsValue()
    {
        var body = "Acceptance Criteria: All tests must pass";
        var criteria = CommandParser.ParseAcceptanceCriteria(body);
        
        Assert.Equal("All tests must pass", criteria);
    }

    [Fact]
    public void ParseAcceptanceCriteria_NoSection_ReturnsNull()
    {
        var body = "## Context\nSome text";
        var criteria = CommandParser.ParseAcceptanceCriteria(body);
        
        Assert.Null(criteria);
    }

    [Fact]
    public void ParseConstraints_SectionFormat_ReturnsContent()
    {
        var body = @"
## Constraints
- No schema changes
- Touch only /src
";
        var constraints = CommandParser.ParseConstraints(body);
        
        Assert.NotNull(constraints);
        Assert.Contains("No schema changes", constraints);
        Assert.Contains("Touch only /src", constraints);
    }

    [Fact]
    public void ParseConstraints_SingleLineFormat_ReturnsValue()
    {
        var body = "Constraints: none";
        var constraints = CommandParser.ParseConstraints(body);
        
        Assert.Equal("none", constraints);
    }

    [Fact]
    public void ParseConstraints_NoSection_ReturnsNull()
    {
        var body = "## Context\nSome text";
        var constraints = CommandParser.ParseConstraints(body);
        
        Assert.Null(constraints);
    }
}
