namespace GhOrchestrator.Core;

public record AIWorkerFileChange(
    string Path,
    AIWorkerChangeType ChangeType,
    string Content
);
