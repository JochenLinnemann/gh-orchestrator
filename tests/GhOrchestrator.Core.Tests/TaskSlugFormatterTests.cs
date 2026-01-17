namespace GhOrchestrator.Core.Tests;

public class TaskSlugFormatterTests
{
    [Fact]
    public void Format_UsesTitleAndCapsLength()
    {
        var title = "This is a very long issue title meant to exceed the slug limit";

        var slug = TaskSlugFormatter.Format(title, "fallback description");

        Assert.Equal(40, slug.Length);
        Assert.DoesNotContain(' ', slug);
    }

    [Fact]
    public void Format_FallsBackToDescriptionWhenTitleMissing()
    {
        var slug = TaskSlugFormatter.Format("", "Add logging to the API");

        Assert.Equal("add-logging-to-the-api", slug);
    }
}
