using System.Text.RegularExpressions;

namespace GhOrchestrator.Core;

/// <summary>
/// Task quality gate validation per Playbook section 3.5.
/// Pure function checks for task constraints.
/// </summary>
public static class TaskQualityGate
{
    private static readonly Regex RepoFormatPattern = new(@"^[a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validate a task specification against quality gate rules.
    /// Enforces Playbook v0 Task Quality Gate:
    /// 1. Acceptance criteria must be present and explicit
    /// 2. Repos must be present and non-empty
    /// 3. Repos must be unambiguous in format (owner/repo)
    /// 4. Constraints must be stated or explicitly marked as 'none'
    /// </summary>
    /// <param name="task">The task specification to validate.</param>
    /// <returns>Validation result with structured error if invalid.</returns>
    public static ValidationResult Validate(TaskSpec task)
    {
        // 1. Acceptance criteria must be present and explicit
        if (string.IsNullOrWhiteSpace(task.AcceptanceCriteria))
            return ValidationResult.Failure("Acceptance criteria must be present and explicit");

        // 2. Repos must be present and non-empty
        if (task.Repos.Count == 0)
            return ValidationResult.Failure("Repos must be present and non-empty");

        // 3. Repos must be unambiguous in format (owner/repo)
        foreach (var repo in task.Repos)
        {
            if (!RepoFormatPattern.IsMatch(repo))
                return ValidationResult.Failure($"Invalid repository format: {repo} (expected owner/repo)");
        }

        // 4. Constraints must be stated or explicitly marked as 'none'
        if (string.IsNullOrWhiteSpace(task.Constraints))
            return ValidationResult.Failure("Constraints must be stated (or explicitly marked as 'none')");

        return ValidationResult.Success();
    }
}
