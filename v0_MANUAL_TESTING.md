# v0 Implementation Status & Manual Testing Guide

**Date:** January 24, 2026  
**Status:** Ready for Plan 9 (End-to-End Manual Testing)

---

## What Has Been Implemented

The complete orchestration loop from PLAYBOOK §3.4 is now implemented:

### 1. **Validation** ✅
- Parse `/ai start` command with optional description
- Extract repos, acceptance criteria, and constraints from Issue body
- Run Task Quality Gate (issue exists, repos listed, criteria explicit, no destructive operations)
- Run RunPreflight (issue open, conservative escalation on destructive intent)

**Code:** `Orchestrator.ProcessIssueComment`, `TaskQualityGate`, `RunPreflight`

### 2. **Claiming** ✅
- Atomically set `AI = running`, `Status = In Progress`, `Run ID` in Projects V2
- Prevent concurrent execution of same task

**Code:** `TaskClaimService`, `TaskClaimPlanner`

### 3. **Planning** ✅
- Generate unique run ID: `{issue}-{timestamp}-{random}`
- Plan per-repo branch names: `ai/<run-id>/<slug>`
- Build PR payloads with task metadata

**Code:** `TaskRunPlanner`, `TaskRunExecutor.BuildPullRequestPlans`

### 4. **AI Execution** ✅
- Invoke bounded AI worker with task context
- Support OpenAI (GPT-4) or mock worker
- Parse file changes (create/modify/delete)

**Code:** `AIWorker`, `OpenAIWorker`, `MockAIWorker`, `AIPromptBuilder`

### 5. **Validation of AI Output** ✅
- Check no duplicate repos
- Check no undeclared repos
- Check all repos have results
- Validate file changes:
  - No binary files
  - No schema changes (block migrations/*.sql, *.prisma, etc.)
  - Max delete ratio (50%) and max delete count (20)
  - Attribution metadata present

**Code:** `WorkerResultValidator`, `WorkerResultValidationSettings`

### 6. **Git Operations** ✅
- Clone repo with shallow depth
- Create working branch from default branch
- Apply file changes (create/modify/delete) with symlink escape detection
- Commit with AI attribution
- Push to origin

**Code:** `GitOperations` (CloneRepositoryAsync, CheckoutBranchAsync, ApplyFileChangesAsync, CommitAsync, PushAsync)

### 7. **PR Creation** ✅
- One PR per repository
- PR title: `AI: {task.Title}`
- PR body includes: run ID, repo, issue link, task description
- PRs contain non-empty, AI-generated code changes

**Code:** `TaskRunExecutor.ExecuteAsync`, `gitHubClient.CreatePullRequest`

### 8. **Reporting** ✅
- Post issue comment with:
  - PR links (one per repo)
  - Summary of changes
  - Testing instructions
  - Risk notes (constraints)
  - Token usage (from AI worker)

**Code:** `IssueCommentReportService`, `IssueCommentReportFormatter`

### 9. **Kanban State Transition** ✅ (NEW)
- After successful PR creation, set `AI = blocked` (waiting for review)
- Preserves `Status = In Progress` (Status → Done only after merge, outside v0)

**Code:** `TaskCompletionPlanner`, `TaskCompletionResult` (NEW)

---

## Known Limitations (Accepted for v0)

| Item | Status | Reason |
|------|--------|--------|
| **Merge automation** | Out of scope | Humans must review and merge manually |
| **Status → Done** | Out of scope | Merge tracking requires webhook polling or database |
| **Concurrent tasks** | Limited by design | One task per issue at a time (enforced by claim logic) |
| **Large codebases** | Tested small, not optimized | Shallow clone helps; may need chunking for >10K files |
| **Streaming output** | Out of scope | Full execution output returned after completion |
| **Cost tracking** | Partial | Token usage logged but not aggregated |

---

## Files Changed (v0 Implementation)

**New Files:**
- `src/GhOrchestrator.Core/TaskCompletionPlanner.cs` — Kanban state transition logic
- `src/GhOrchestrator.Core/TaskCompletionResult.cs` — Result type for completion planning
- `tests/GhOrchestrator.Core.Tests/TaskCompletionPlannerTests.cs` — Unit tests

**Modified Files:**
- `src/GhOrchestrator.Core/Orchestrator.cs` — Added step 7 (Kanban state transition after PR creation)

**Documentation Updates:**
- `README.md` — Updated status section, added v0 manual testing guide
- `ROADMAP.md` — Marked critical path items complete, moved Plan 9 to required section
- `SETUP.md` — Added v0 manual testing checklist

---

## How to Execute Plan 9 (Manual Testing)

### Prerequisites
1. GitHub App created and installed on organization
2. Test repository in same organization
3. Projects V2 board with `AI`, `Status`, `Run ID` fields
4. Orchestrator running locally (`dotnet run` from `src/GhOrchestrator.Host`)
5. Tunnel to localhost:5000 (ngrok or similar)

### Test Flow

1. **Create a GitHub Issue** with:
   ```markdown
   ## Task
   Implement a simple utility function that validates email addresses.
   
   ## Repositories
   - owner/test-repo
   
   ## Acceptance Criteria
   - [ ] Utility function in /src/utils/
   - [ ] Unit tests in /tests/
   - [ ] No external dependencies
   
   ## Constraints
   none
   ```

2. **Add Issue to Projects V2 board**

3. **Comment `/ai start` on Issue**

4. **Observe:**
   - ✅ Project fields update: `AI=running`, `Status=In Progress`, `Run ID` populated
   - ✅ Logs show task validation, planning, AI execution
   - ✅ Branch created with pattern `ai/{run-id}/implement-simple-utility-function`
   - ✅ PR opened with non-empty code changes
   - ✅ Issue comment posted with PR link and testing instructions
   - ✅ Project field updated: `AI=blocked` (awaiting review)

5. **Validate PR:**
   - Check branch exists in repo
   - Check PR title: `AI: Implement a simple utility function that validates email addresses.`
   - Check PR body contains run ID, repo, issue link
   - Check PR files tab shows actual code additions/modifications
   - Check commit message: `AI: Apply changes for {runId}`
   - Check commit author: `gh-orchestrator[bot]`

6. **Record Results:**
   - Screenshot of PR with file changes
   - Notes on file quality and correctness
   - Any issues encountered
   - Update ROADMAP.md Plan 9 section with outcomes

---

## Acceptance Criteria for v0 Release

- [ ] Plan 9 executed with 1+ real test tasks
- [ ] All PRs contain non-empty AI-generated code
- [ ] No secrets leaked in commits or comments
- [ ] Kanban state transitions correctly (`running` → `blocked`)
- [ ] Branch/PR naming follows convention
- [ ] Issue comment posted with accurate PR links
- [ ] Git operations work on real repositories
- [ ] AI worker produces reasonable code (per human review)
- [ ] No unit test failures
- [ ] Outcomes documented in ROADMAP.md

---

## Next Steps After Plan 9

1. **Incorporate findings** into code or documentation as needed
2. **Create final ADRs** for any scope changes discovered
3. **Execute Plan 17:** v0 acceptance criteria cutover
4. **Tag v0.0.1 release**

---

## Questions?

Refer to:
- `ARCHITECTURE.md` — System structure and trust boundaries
- `DECISIONS.md` — Architectural decisions and rationale
- `PLAYBOOK.md` — Behavioral specification
- `ai/README.md` — AI usage guidance and policies
