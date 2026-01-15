namespace GhOrchestrator.Core;

/// <summary>
/// Outer-layer preflight validation.
/// 
/// Complements TaskQualityGate (pure task spec checks).
/// 
/// This layer validates contextual checks:
/// - Issue exists and is open
/// - No destructive intent (conservative escalation only)
/// 
/// No GitHub I/O; all inputs provided by caller.
/// </summary>
public static class RunPreflight
{
    /// <summary>
    /// Phrases that suggest destructive operations.
    /// Used for conservative escalation, not enforcement.
    /// 
    /// Case-insensitive match only.
    /// Do NOT run regex or deep parsing.
    /// </summary>
    private static readonly string[] DestructiveKeywords = new[]
    {
        "delete",
        "drop",
        "destroy",
        "wipe",
        "truncate",
        "terraform destroy",
        "rm -rf",
        "format disk",
        "purge",
    };

    /// <summary>
    /// Validate preflight conditions for a task run.
    /// 
    /// Checks:
    /// 1. Issue exists
    /// 2. Issue is open
    /// 3. No obvious destructive intent (conservative escalation)
    /// </summary>
    /// <param name="taskSpec">The task specification.</param>
    /// <param name="issueContext">Contextual metadata about the Issue.</param>
    /// <returns>Preflight validation result.</returns>
    public static PreflightValidationResult Validate(TaskSpec taskSpec, IssueContext issueContext)
    {
        // Check 1: Issue must exist
        if (!issueContext.IssueExists)
            return PreflightValidationResult.Failure(
                PreflightFailureReason.IssueNotFound,
                "Issue does not exist or is inaccessible"
            );

        // Check 2: Issue must be open
        if (!issueContext.IsOpen)
            return PreflightValidationResult.Failure(
                PreflightFailureReason.IssueClosed,
                "Issue is closed. Cannot run task on a closed issue."
            );

        // Check 3: Escalate if destructive intent detected
        var destructiveCheck = DetectDestructiveIntent(taskSpec);
        if (destructiveCheck.IsDetected)
        {
            return PreflightValidationResult.EscalationRequired(
                $"Potential destructive operation detected. Please confirm: {destructiveCheck.Message}"
            );
        }

        return PreflightValidationResult.Success();
    }

    /// <summary>
    /// Conservative detection of destructive phrases.
    /// 
    /// Used for escalation only, not enforcement.
    /// </summary>
    private static (bool IsDetected, string Message) DetectDestructiveIntent(TaskSpec taskSpec)
    {
        var searchTexts = new[]
        {
            taskSpec.Description ?? "",
            taskSpec.AcceptanceCriteria ?? "",
            taskSpec.Constraints ?? "",
        };

        foreach (var text in searchTexts)
        {
            foreach (var keyword in DestructiveKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return (true, $"Found phrase '{keyword}' in task specification.");
                }
            }
        }

        return (false, "");
    }
}
