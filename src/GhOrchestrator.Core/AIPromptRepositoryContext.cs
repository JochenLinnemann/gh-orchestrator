namespace GhOrchestrator.Core;

/// <summary>
/// Repository context used for AI prompt construction.
/// </summary>
public record AIPromptRepositoryContext(
    string Repository,
    string? PrimaryLanguage,
    IReadOnlyList<string> KeyFiles,
    IReadOnlyList<string> FileStructure
);
