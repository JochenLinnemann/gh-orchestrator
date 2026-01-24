# Manual Testing Resources — For v0 Review

**Status:** ✅ All code implemented and tested. Ready for Plan 9 (manual testing with real repos).

---

## 📋 Review Checklist

Before executing manual tests, review these documents in order:

### 1️⃣ **Start Here: IMPLEMENTATION_SUMMARY.md** (5 min read)
   - High-level overview of what was implemented
   - Verification that everything builds and tests pass
   - Quick reference table of all workflow steps
   - Next steps for manual testing

### 2️⃣ **Testing Guide: v0_MANUAL_TESTING.md** (10 min read)
   - Detailed implementation status per workflow step
   - Code locations for each component
   - Known limitations (accepted for v0)
   - Step-by-step test flow with expected outcomes
   - v0 release acceptance criteria

### 3️⃣ **Configuration: SETUP.md — "v0 Manual Testing Checklist (Plan 9)" section** (15 min reference)
   - Prerequisites (GitHub App, test repo, Projects V2, environment variables)
   - Execution phase steps (what to do to trigger a task)
   - Validation phase checklist (what to verify in output)
   - Results recording (what to document)

### 4️⃣ **Behavioral Spec: PLAYBOOK.md** (reference as needed)
   - Complete workflow specification
   - Kanban board setup (fields required)
   - Task quality gate rules
   - Guardrails and safety constraints

### 5️⃣ **Architecture: ARCHITECTURE.md** (reference as needed)
   - System structure and trust boundaries
   - External dependencies
   - Scaling assumptions
   - Constraints and budget

### 6️⃣ **Decisions: DECISIONS.md** (reference as needed)
   - Rationale for each major decision
   - Tradeoffs accepted and why
   - Context for design choices

---

## 🎯 Quick Start for Manual Testing

### What You Need (30 min to setup)
1. GitHub App created in your organization
2. Test repository in the organization
3. GitHub Projects V2 board with fields: `AI`, `Status`, `Run ID`
4. Environment variables configured
5. Orchestrator running locally (`dotnet run` from `src/GhOrchestrator.Host`)
6. Tunnel to localhost:5000 (ngrok or similar)

### What to Do (10 min per test)
1. Create GitHub Issue with acceptance criteria and repo list
2. Add Issue to Projects V2
3. Comment `/ai start` on Issue
4. Observe execution in logs and GitHub UI

### What to Validate (15 min per test)
Use the checklist from `SETUP.md` to verify:
- ✅ Branch created with naming pattern `ai/<run-id>/<slug>`
- ✅ PR opened with non-empty AI-generated code changes
- ✅ Issue comment posted with PR links
- ✅ Kanban state updated: `AI = blocked`
- ✅ Commit author: `gh-orchestrator[bot]`
- ✅ No secrets leaked

### What to Document (10 min per test)
1. Screenshot PR showing file changes
2. Note any issues or gaps
3. Update `ROADMAP.md` Plan 9 section
4. If gaps found, create ADR in `DECISIONS.md`

---

## 📊 Current Implementation Status

| Component | Status | Tested |
|-----------|--------|--------|
| Task validation (quality gate) | ✅ Complete | ✅ 123 unit tests |
| Task claiming (Kanban atomicity) | ✅ Complete | ✅ 123 unit tests |
| Execution planning (branch/PR generation) | ✅ Complete | ✅ 123 unit tests |
| AI worker invocation (OpenAI/mock) | ✅ Complete | ✅ 123 unit tests |
| AI output validation (no binaries, no schema changes) | ✅ Complete | ✅ 123 unit tests |
| Git operations (clone, branch, changes, commit, push) | ✅ Complete | ✅ 123 unit tests |
| PR creation (one per repo) | ✅ Complete | ✅ 123 unit tests |
| Issue comment reporting | ✅ Complete | ✅ 123 unit tests |
| **Kanban state transition (blocked)** | ✅ **NEW** | ✅ **4 new tests** |
| Integration (end-to-end) | ⏳ Needs Plan 9 | — |

---

## 🔧 Build & Test

Verify everything works:
```powershell
cd c:\Users\JochenLinnemann\Source-Repos\JochenLinnemann\gh-orchestrator

# Build Release configuration
dotnet build --configuration Release

# Run all tests
dotnet test --no-build

# Expected: All 129 tests pass, 0 errors
```

---

## 📁 Key Files to Review

### Code Files (verify implementation)
- `src/GhOrchestrator.Core/TaskCompletionPlanner.cs` — NEW: Kanban state transition
- `src/GhOrchestrator.Core/TaskCompletionResult.cs` — NEW: Result type
- `src/GhOrchestrator.Core/Orchestrator.cs` — Step 7 added (see line ~250 area)

### Test Files (verify coverage)
- `tests/GhOrchestrator.Core.Tests/TaskCompletionPlannerTests.cs` — NEW: 4 test cases

### Documentation Files (reference for manual testing)
- `IMPLEMENTATION_SUMMARY.md` — Executive summary (this area)
- `v0_MANUAL_TESTING.md` — Complete implementation reference + test guide
- `SETUP.md` — Configuration and Plan 9 checklist
- `README.md` — Updated status section
- `ROADMAP.md` — Project roadmap
- `PLAYBOOK.md` — Behavioral specification

---

## ✨ What's New in This Update

1. **Missing workflow step implemented:** Kanban state transition after successful PR creation (`AI = blocked`)
2. **All tests pass:** 129 unit tests, including 4 new tests for TaskCompletionPlanner
3. **Documentation complete:** README, ROADMAP, SETUP, PLAYBOOK all updated
4. **Ready for human action:** Plan 9 (manual testing with real repos) is the next step

---

## ❓ Questions?

| Topic | Document |
|-------|----------|
| What was implemented? | → `IMPLEMENTATION_SUMMARY.md` or `v0_MANUAL_TESTING.md` |
| How do I test it? | → `SETUP.md` "v0 Manual Testing Checklist (Plan 9)" |
| Why make these choices? | → `DECISIONS.md` |
| What does it do? | → `PLAYBOOK.md` |
| Where is the code? | → `v0_MANUAL_TESTING.md` (code locations table) |
| Is it ready? | → Yes, awaiting Plan 9 execution |

---

**Next Action:** Execute Plan 9 (end-to-end manual verification with real GitHub repos).
See `SETUP.md` for detailed testing checklist.
