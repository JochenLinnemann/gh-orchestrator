namespace GhOrchestrator.Core;

public record ProjectTaskStateSnapshot(
    ProjectTaskState State,
    IReadOnlyCollection<string> MissingFields);
