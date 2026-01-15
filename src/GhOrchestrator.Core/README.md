# GhOrchestrator.Core

Minimal orchestrator stub using .NET 10 standard library only.
Implements Playbook v0 Task Quality Gate (section 3.5).

## Structure

**Pure Functions & Types:**
- `TaskSpec` — Record type representing a bounded task specification from a GitHub Issue
- `CommandParser` — Parses `/ai start` commands, repository lists, acceptance criteria, and constraints from Issue bodies
- `TaskQualityGate` — Validates tasks against Playbook v0 quality constraints
- `ValidationResult` — Structured validation result with error messages
- `Orchestrator` — Stateless coordinator (pure validation only, no GitHub I/O yet)

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

✅ 30 xUnit tests (all passing)

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

## What's NOT Implemented (Intentionally)

❌ GitHub API client  
❌ Webhook handling  
❌ PR/branch creation  
❌ Network I/O  
❌ Worker execution  

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
- xUnit (test framework only)
- **Zero runtime dependencies** — stdlib only
