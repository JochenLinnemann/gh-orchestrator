# Plan 17 Acceptance Criteria Assessment

Date: 2026-01-24

## Summary

The codebase partially meets Plan 17 acceptance criteria. The core flow is implemented and tested, but **two critical items need attention** before v0 release.

---

## Acceptance Criteria Status

### ✅ 1. One GitHub Issue → claim → branches → PRs with AI-generated changes → report

**Status:** COMPLETE

**Evidence:**
- [Orchestrator.cs](../src/GhOrchestrator.Core/Orchestrator.cs#L128-L319) implements `ProcessTaskAsync()` with full flow
- Flow: Validation → Claim → Plan → Execute → Report
- [TaskRunExecutor.cs](../src/GhOrchestrator.Core/TaskRunExecutor.cs#L150) applies AI-generated file changes via `ApplyFileChangesAsync()`
- [GitOperations.cs](../src/GhOrchestrator.Core/GitOperations.cs#L67-L128) implements file changes (create/modify/delete)
- Changes are committed and pushed before PR creation (lines 150-152 in TaskRunExecutor.cs)

**Tests:**
- [TaskRunExecutionTests.cs](../tests/GhOrchestrator.Core.Tests/TaskRunExecutionTests.cs) covers execution flow
- [GitOperationsTests.cs](../tests/GhOrchestrator.Core.Tests/GitOperationsTests.cs) covers file operations
- [OrchestratorReportTests.cs](../tests/GhOrchestrator.Core.Tests/OrchestratorReportTests.cs) covers reporting

---

### ⚠️ 2. Tests cover Critical Path; non-v0 tests marked as pending where appropriate

**Status:** PARTIALLY COMPLETE

**Evidence:**
- Tests exist for all critical path components:
  - Validation: `RunPreflightTests.cs`, `TaskQualityGateTests.cs`
  - Claim: `TaskClaimServiceTests.cs`
  - Planning: `TaskRunPlannerTests.cs`
  - Execution: `TaskRunExecutionTests.cs`, `GitOperationsTests.cs`
  - Reporting: `IssueCommentReportServiceTests.cs`
  - Orchestration: `OrchestratorRetryTests.cs`, `OrchestratorReportTests.cs`

**Gap:**
- No explicit documentation of which tests are "Critical Path" tests
- No tests marked as `[Fact(Skip = "Post-v0")]` for future features
- No end-to-end integration test that runs the complete `ProcessTaskAsync()` flow

**Recommendation:**
- Add XML comments to test classes indicating Critical Path coverage
- Add one integration test that validates the complete flow with a fake GitHub client

---

### ❌ 3. Plan 9 executed with real repos; outcomes and gaps recorded

**Status:** NOT COMPLETE

**Evidence:**
- Plan 9 is defined in [TODO.md](../docs/iterations/v0/TODO.md#L367-L380)
- No execution results found
- No outcomes recorded

**Recommendation:**
- Execute Plan 9 manual verification
- Create a document (e.g., `TestDebug/plan9-execution-log.md`) with:
  - Test environment setup
  - Execution timestamps
  - Observed behavior
  - Screenshots or logs
  - Gaps or issues discovered

---

### ⏳ 4. ADRs created for any scope changes discovered

**Status:** PENDING (depends on Plan 9)

**Evidence:**
- [DECISIONS.md](../DECISIONS.md) exists and is well-structured
- Cannot assess until Plan 9 is executed

**Recommendation:**
- Execute Plan 9 first
- Document any scope changes in DECISIONS.md

---

### ✅ 5. PRs contain AI-generated code changes, not empty branches

**Status:** COMPLETE

**Evidence:**
- [TaskRunExecutor.cs](../src/GhOrchestrator.Core/TaskRunExecutor.cs#L140-L142) validates worker returns file changes:
  ```csharp
  if (workerRepoResult.FileChanges.Count == 0)
      throw new InvalidOperationException("AI worker returned no file changes.");
  ```
- [GitOperations.cs](../src/GhOrchestrator.Core/GitOperations.cs#L67-L128) applies changes to files
- Changes are committed (line 151 in TaskRunExecutor.cs) before PR creation (line 159)

**Tests:**
- [TaskRunExecutionTests.cs](../tests/GhOrchestrator.Core.Tests/TaskRunExecutionTests.cs#L38-L49) verifies file changes are applied

---

### ❌ 6. Obsolete methods cleaned up from `Orchestrator.cs`

**Status:** NOT COMPLETE

**Evidence:**
- [Orchestrator.cs](../src/GhOrchestrator.Core/Orchestrator.cs#L110-L122) contains obsolete methods:
  - Line 110: `ExecuteTask(TaskSpec task)` - Throws `NotImplementedException`
  - Line 120: `ReportResult(TaskSpec task, bool success, string message)` - Throws `NotImplementedException`
- These are superseded by `ProcessTaskAsync()` (line 128)

**Impact:**
- Code confusion: developers might call wrong methods
- Dead code in production
- Violates ARCHITECTURE principle: "Delete, don't comment out"

**Recommendation:**
- Remove both methods
- If keeping for backward compatibility, mark as `[Obsolete("Use ProcessTaskAsync instead", error: true)]`
- Better: Remove entirely since no usage found in codebase

---

## Recommended Fixes

### Fix 1: Remove obsolete methods from Orchestrator.cs

**Priority:** HIGH (blocks v0 per acceptance criteria)

**Action:**
```csharp
// DELETE lines 104-122 in Orchestrator.cs
// Remove:
// - ExecuteTask()
// - ReportResult()
```

**Verification:**
- Run all tests to ensure no dependencies
- Grep for usage: `grep -r "ExecuteTask\|ReportResult" src/ tests/`

---

### Fix 2: Execute Plan 9 manual verification

**Priority:** HIGH (blocks v0 per acceptance criteria)

**Action:**
1. Set up test GitHub repository with:
   - GitHub App configured
   - Project with required fields
   - Test issue with Repos, Acceptance Criteria, Constraints
2. Comment `/ai start` on issue
3. Observe and document:
   - Validation behavior
   - Claim operation
   - AI worker invocation
   - Branch creation
   - File changes applied
   - PR creation
   - Report comment
4. Record outcomes in `TestDebug/plan9-execution-log.md`

**Verification:**
- Human review of documented outcomes
- Gaps recorded in DECISIONS.md

---

### Fix 3: Add Critical Path test documentation

**Priority:** MEDIUM (quality improvement)

**Action:**
1. Add XML comments to test classes indicating Critical Path coverage:
   ```csharp
   /// <summary>
   /// Critical Path: Tests for task validation (Plan 17, v0 release criteria).
   /// </summary>
   public class RunPreflightTests
   ```

2. Consider adding one integration test:
   ```csharp
   [Fact]
   public async Task ProcessTaskAsync_CompleteFlow_CreatesValidPR()
   {
       // Arrange: fake GitHub client, real orchestrator
       // Act: call ProcessTaskAsync
       // Assert: validation, claim, execution, reporting all succeed
   }
   ```

**Verification:**
- Review test coverage report
- Confirm all Critical Path components have tests

---

## Blockers for v0 Release

1. ❌ **Remove obsolete methods** (Fix 1)
2. ❌ **Execute Plan 9** (Fix 2)
3. ⏳ **Document any scope changes from Plan 9** (depends on Fix 2)

---

## Next Steps

1. **Immediate:** Remove `ExecuteTask()` and `ReportResult()` from Orchestrator.cs
2. **Before v0:** Execute Plan 9 manual verification
3. **After Plan 9:** Review and update DECISIONS.md with any scope changes
4. **Optional (but recommended):** Add Critical Path test documentation

---

## Notes

- Code quality is high: good separation of concerns, testability, safety
- ROADMAP accurately reflects implementation status
- GitOperations properly implements safety (path validation, symlink checks)
- AI worker integration is complete with validation and quality gates
- Reporting includes AI attribution and metadata

The main gaps are **documentation/cleanup** (obsolete methods) and **manual verification** (Plan 9), not implementation issues.
