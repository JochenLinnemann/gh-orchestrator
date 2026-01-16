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
    public void ParseAiStartCommand_NotAtBeginning_StillParsed()
    {
        var comment = "Some context\n/ai start\nAdd logging to handler";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Contains("Add logging", result);
    }

    [Fact]
    public void ParseAiStartCommand_WithLeadingWhitespace_Accepted()
    {
        var comment = "  /ai start\nAdd feature";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Equal("Add feature", result);
    }

    [Fact]
    public void ParseAiStartCommand_WithTrailingTextOnSameLine_Included()
    {
        var comment = "/ai start Add logging and fix bug\nMore details here";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Contains("Add logging and fix bug", result);
        Assert.Contains("More details", result);
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
    public void ParseRepositories_LineFormat_ParsesCorrectly()
    {
        var body = "Repos: org/service-a, org/service-b, org/service-c";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(3, repos.Count);
        Assert.Contains("org/service-a", repos);
        Assert.Contains("org/service-b", repos);
        Assert.Contains("org/service-c", repos);
    }

    [Fact]
    public void ParseRepositories_LineFormatNoSpaces_ParsesCorrectly()
    {
        var body = "Repos: owner/repo1,owner/repo2,owner/repo3";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(3, repos.Count);
        Assert.Contains("owner/repo1", repos);
        Assert.Contains("owner/repo2", repos);
        Assert.Contains("owner/repo3", repos);
    }

    [Fact]
    public void ParseRepositories_LineFormatWithLeadingSpace_ParsesCorrectly()
    {
        var body = "  Repos: org/a, org/b";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(2, repos.Count);
        Assert.Contains("org/a", repos);
        Assert.Contains("org/b", repos);
    }

    [Fact]
    public void ParseRepositories_LineFormatTakesPrecedence_IgnoresSectionFormat()
    {
        var body = @"Repos: org/line-format

## Repositories
- org/section-format
";
        var repos = CommandParser.ParseRepositories(body);
        
        // Should use line format and ignore section
        Assert.Single(repos);
        Assert.Contains("org/line-format", repos);
    }

    [Fact]
    public void ParseRepositories_SectionFormatIfNoLineFormat_FallsBack()
    {
        var body = @"## Repositories
- org/section-a
- org/section-b
";
        var repos = CommandParser.ParseRepositories(body);
        
        Assert.Equal(2, repos.Count);
        Assert.Contains("org/section-a", repos);
        Assert.Contains("org/section-b", repos);
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

    [Fact]
    public void ParseAiStartCommand_StopsAtNextAiCommand_IgnoresFollowingCommands()
    {
        var comment = "/ai start\nAdd logging\n/ai plan\nNext command content";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Equal("Add logging", result);
        Assert.DoesNotContain("Next command", result);
        Assert.DoesNotContain("/ai plan", result);
    }

    [Fact]
    public void ParseAiStartCommand_StopsAtNextAiCommand2_IgnoresFollowingCommands()
    {
        var comment = "/ai start\nAdd logging\nAdd configuration\n/ai plan\nNext command content";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Equal("Add logging\nAdd configuration", result);
        Assert.DoesNotContain("Next command", result);
        Assert.DoesNotContain("/ai plan", result);
    }

    [Fact]
    public void ParseAiStartCommand_WithAiCommandOnNextLine_ReturnsEmpty()
    {
        var comment = "/ai start\n/ai plan something else";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.Null(result);
    }

    [Fact]
    public void ParseAiStartCommand_WithAiCommandOnNextLine2_ReturnsEmpty()
    {
        var comment = "/ai start Something else on the same line\n/ai plan something else";
        var result = CommandParser.ParseAiStartCommand(comment);
        
        Assert.NotNull(result);
        Assert.Equal("Something else on the same line", result);
    }

    [Fact]
    public void ParseRepositories_ExactHeaderMatch_IgnoresOtherRepositories()
    {
        var body = "## Other Repositories\n- owner/repo1\n## Repositories\n- owner/repo2";
        var result = CommandParser.ParseRepositories(body);
        
        Assert.Single(result);
        Assert.Equal("owner/repo2", result[0]);
    }

    [Fact]
    public void ParseAcceptanceCriteria_ExactHeaderMatch_IgnoresSimilarHeaders()
    {
        var body = "## Acceptance Criteria Details\nSome text\n## Acceptance Criteria\nMain criteria";
        var result = CommandParser.ParseAcceptanceCriteria(body);
        
        Assert.NotNull(result);
        Assert.Equal("Main criteria", result);
    }
}
