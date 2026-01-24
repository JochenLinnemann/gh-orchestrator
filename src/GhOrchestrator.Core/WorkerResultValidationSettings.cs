namespace GhOrchestrator.Core;

public record WorkerResultValidationSettings(
    int MaxDeleteCount,
    decimal MaxDeleteRatio,
    int MinTotalChangesForRatio,
    IReadOnlyList<string> SchemaPathTokens,
    IReadOnlyList<string> SchemaFileExtensions,
    string CommitAuthorName,
    string CommitAuthorEmail)
{
    public static WorkerResultValidationSettings Default(string commitAuthorName, string commitAuthorEmail) =>
        new(
            MaxDeleteCount: 20,
            MaxDeleteRatio: 0.5m,
            MinTotalChangesForRatio: 10,
            SchemaPathTokens: new[] { "migrations", "schema", "schemas" },
            SchemaFileExtensions: new[] { ".sql", ".ddl", ".prisma", ".db", ".sqlite" },
            CommitAuthorName: commitAuthorName,
            CommitAuthorEmail: commitAuthorEmail);
}
