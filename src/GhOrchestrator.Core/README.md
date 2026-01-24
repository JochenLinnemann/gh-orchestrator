# GhOrchestrator.Core

Core orchestration logic for GH Orchestrator (net10.0).
Implements Playbook v0 Task Quality Gate (section 3.5) and the task run flow used by the host.

## Structure

**Pure Functions & Types:**
- `TaskSpec` — Record type representing a bounded task specification from a GitHub Issue
- `CommandParser` — Parses `/ai start` commands, repository lists, acceptance criteria, and constraints from Issue bodies
- `TaskQualityGate` — Validates tasks against Playbook v0 quality constraints
- `ValidationResult` — Structured validation result with error messages
- `TaskValidationResult` — Combined result for Task Quality Gate + preflight checks
- `TaskRunPlan` — Planned execution run (run ID, repos, and execution steps)
- `TaskRunPlanner` — Deterministic planner that produces a task run plan
- `TaskSlugFormatter` — Formats a short task slug for branch naming (issue title first, description fallback, capped length)
- `BranchNameFormatter` — Formats branch names using the Playbook convention
- `RepoPullRequestPlan` — Per-repository branch + pull request payload
- `TaskRunExecutor` — Builds per-repo PR payloads and executes branch/PR creation via the GitHub client boundary
- `IGitHubClient` — Interface boundary for GitHub operations (read issue, comment, update project, create branch, open PR)
- `Orchestrator` — Stateless coordinator that validates, claims, plans, and executes via the GitHub client boundary

## What's Implemented

✅ Parsing `/ai start` command from Issue comments  
✅ Parsing multi-repo list from `## Repositories` section in Issue body  
✅ Parsing acceptance criteria from `## Acceptance Criteria` section or single-line format  
✅ Parsing constraints from `## Constraints` section or single-line format  
✅ Task quality gate validation per Playbook section 3.5:
  1. Acceptance criteria must be present and explicit
  2. Repos must be present and non-empty
  3. Repos must be unambiguous in format (`owner/repo`)
  4. Constraints must be stated (or explicitly marked as `none`)

✅ xUnit tests for parser and validation behaviors
✅ Task run planning (run ID formatting + per-repo execution steps)
✅ Branch name + pull request payload planning
✅ Branch + PR creation via the GitHub client boundary (real I/O lives outside Core)
✅ OpenAI-backed AI worker (optional; falls back to mock worker when not configured)
✅ Issue comment reporting format + posting helper
✅ Task claim planning and Projects V2 field update requests
✅ Git operations to apply AI changes, commit with attribution, and push branches
✅ Worker result validation (repo matching, destructive change checks, schema-change detection)

## Issue Body Format

The parser supports both section-based and single-line formats:

**Section format:**
```markdown
## Repositories
- org/service-a
- org/service-b

## Acceptance Criteria
- Code compiles without errors
- Tests pass
- Documentation updated

## Constraints
- No schema changes
- Touch only /src
```

**Single-line format:**
```markdown
## Repositories
- org/service-a

Acceptance Criteria: All tests must pass
Constraints: none
```

## Validation Layers

### Inner Layer: TaskQualityGate (Pure Function)

Validates the task specification itself:
- Acceptance criteria present and explicit
- Repos present and non-empty
- Repos valid format (`owner/repo`)
- Constraints stated (or marked `none`)

No external context needed. Deterministic. Safe to run offline.

### Outer Layer: RunPreflight (Contextual)

Validates external conditions before execution starts:
- **Issue exists** — GitHub issue found and accessible
- **Issue is open** — Not closed
- **No destructive intent** — Conservative escalation for phrases like `delete`, `drop database`, `terraform destroy`, etc.

Complements TaskQualityGate. Takes `TaskSpec` + `IssueContext`.

Returns structured result:
- `IsValid` — All checks passed
- `NeedsHumanConfirmation` — Destructive intent detected; requires explicit approval
- `FailureReason` — Enum: `IssueNotFound`, `IssueClosed`, `DestructiveIntentDetected`
- `ErrorMessage` — Human-readable explanation

**Destructive Intent Detection** is conservative and explicit:
- Only checks for a small, maintained list of phrases (case-insensitive):
  - `delete`, `drop`, `destroy`, `wipe`, `truncate`, `terraform destroy`, `rm -rf`, `format disk`, `purge`
- No regex parsing, code execution, or "smart" analysis
- Used for escalation, not enforcement
- False positives are acceptable (human confirms). The detector is not a safety guarantee; human review remains mandatory.

## What's NOT Implemented (Intentionally)

❌ Binary file changes (text-only writes; binary content is rejected)  
❌ Full lint/format enforcement (only basic text validation is applied)  
❌ Additional operating modes (`/ai plan`, CI fix loops, etc.)  

These will be added incrementally as separate changes.

## Build & Test

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal
```

## Dependencies

- .NET 10.0 SDK
- OpenAI .NET SDK (used by `OpenAIWorker`)
- xUnit (test framework only)
