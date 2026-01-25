# Manual Testing Results

This document tracks manual testing iterations of the gh-orchestrator system with real GitHub repositories and OpenAI integration.

---

## Iteration 1 - January 25, 2026

**Duration**: ~3 hours  
**Tester**: JochenLinnemann  
**Environment**: Local development with ngrok tunnel  
**Test Repos**: jlits/gh-orchestrator-testing, jlits/gh-orchestrator-testing2  
**Test Issue**: #14 "Create projects"

### Test Objectives
- End-to-end validation of gh-orchestrator with real GitHub repos
- Verify OpenAI integration for code generation
- Test GitHub Projects V2 Kanban board state management
- Validate webhook handling, authentication, and authorization
- Identify issues in real-world usage scenarios

### Setup (30 minutes)
- Created GitHub App with webhook permissions
- Set up 2 test repositories under jlits org
- Configured GitHub Projects V2 board with custom fields (AI, Status, Run ID)
- Configured environment variables (OPENAI_API_KEY, OPENAI_MODEL, project ID)
- Started ngrok tunnel to localhost:5000
- Configured webhook URL in GitHub App settings

### Test Execution Summary

#### ✅ What Worked Well

1. **Quality Gate Validation**
   - Correctly rejected issues missing required sections (Repositories, Acceptance Criteria, Constraints)
   - Detected misspellings ("Acceptance Creteria" → rejected)
   - Enforced org/repo format requirements
   - Validation errors provided clear, actionable feedback

2. **Webhook Integration**
   - Signature verification working correctly
   - Organization authorization filtering working
   - Issue comment event parsing successful
   - Self-comment filtering preventing infinite loops

3. **AI Code Generation**
   - Successfully generated React SPA with custom implementation
   - Created proper .NET 10 Web API project structure
   - Generated 16 files total (8 per repo) with reasonable quality
   - React app worked perfectly in browser with functional counter and styling
   - Execution time: 53.6 seconds with 1 retry

4. **Git Operations**
   - Branch creation working (run-14-20260125120149)
   - File commits successful
   - Push to remote working
   - PR creation successful (2 PRs created)

5. **Run ID Tracking**
   - Proper run ID generation (run-{issue}-{timestamp} format)
   - Consistent logging across all phases
   - Run ID visible in logs and reports

#### ❌ Issues Discovered

1. **GitHub Projects V2 API - Kanban State Management (CRITICAL)**
   - **Status**: Blocked - requires GitHub Projects V2 GraphQL API
   - **Problem**: REST API `GET /orgs/{org}/projectsV2/{projectId}/items/{itemId}` only returns populated fields
   - **Impact**: Custom fields (AI, Run ID) not returned if unpopulated or after updates
   - **Evidence**: Only Title field appeared in response despite 17 fields defined on project
   - **Root Cause**: GitHub Projects V2 REST API has incomplete field coverage
   - **Attempted Fixes**:
     - Tried parsing `fields` array instead of `field_values` object
     - Added field ID to name mapping
     - Added extensive debug logging
     - Discovered REST API limitation
   - **Recommendation**: **Implement GitHub Projects V2 GraphQL API instead of REST API**
     - GraphQL allows explicit field selection
     - More reliable for custom field queries
     - Better documented for Projects V2
   - **Note**: This is blocking v0 completion - without Kanban state management, tasks cannot transition through workflow states

2. **Model Configuration Issues**
   - Initially configured with `gpt-5.2-codex` (non-chat model) → 404 errors
   - Added model compatibility detection (IsChatModel heuristic)
   - Switched to `gpt-4o` → successful
   - **Recommendation**: Document supported model list, add validation at startup

3. **Self-Comment Triggering Validation**
   - Bot's execution report comments triggered validation errors
   - **Fix Applied**: Added early return filter for comments without `/ai start`
   - Status: ✅ Resolved

4. **.NET Project Build Errors**
   - Generated API project missing `Swashbuckle.AspNetCore` package reference
   - Build failed on test machine
   - **Impact**: Generated code not immediately runnable
   - **Recommendation**: Strengthen AI prompt requirements or add post-generation validation

5. **PR Description Quality**
   - PR descriptions show all acceptance criteria for both repos (not repo-specific)
   - Makes it harder to review individual PRs
   - **Recommendation**: Filter criteria by target repo in PR description generation

#### 💡 Ideas & Improvements

1. **Provide Repository Context to AI**
   - Currently AI has no knowledge of existing repo structure
   - **Idea**: Clone repo first, include file tree and key files in prompt
   - Would enable better integration with existing codebases
   - Trade-off: Increased token usage and execution time

2. **PR Feedback Loop**
   - After PR creation, monitor for PR comments, approvals, or merge
   - Use feedback to improve subsequent iterations
   - Enable self-improvement through human review
   - **Idea**: Add `/ai refine` command to trigger improvements based on PR feedback

3. **Docker Container Execution**
   - Run AI worker in isolated container with GitHub token auth
   - Better security (no local git credentials)
   - Easier to parallelize across multiple repos
   - Cloud deployment ready

4. **Enhanced Prompt Engineering**
   - Add examples of good file structures to prompt
   - Include common pitfalls and how to avoid them
   - Consider few-shot learning with curated examples

5. **Model Result Variability**
   - Different runs produce different implementations
   - Good: Shows creativity
   - Bad: Inconsistent quality, hard to test
   - **Idea**: Add temperature/sampling controls to configuration

6. **Async Execution for Multiple Repos**
   - Currently processes repos sequentially
   - Could parallelize AI calls for independent repos
   - Faster execution for multi-repo tasks

7. **Cost Tracking**
   - Log token usage per run
   - Calculate OpenAI API costs
   - Add budget controls

### Test Metrics

| Metric | Value |
|--------|-------|
| Test Duration | 3 hours |
| Setup Time | 30 minutes |
| Successful Executions | 2 (mock + real) |
| Failed Executions | 3 (config issues) |
| PRs Created | 2 |
| Files Generated | 16 |
| AI Execution Time | 53.6s |
| AI Retries | 1 |
| Quality Gate Rejections | 3 |
| Critical Bugs Found | 1 (Kanban state) |
| Medium Bugs Found | 2 (model config, build errors) |
| Minor Issues Found | 2 (PR descriptions, self-comments) |

### Configuration Used

```bash
# Environment Variables
OPENAI_API_KEY=<redacted>
OPENAI_MODEL=gpt-4o  # Initially gpt-5.2 (failed)
OPENAI_TIMEOUT_SECONDS=120  # Increased from 30
OPENAI_MAX_RETRIES=<default>
GH_APP_ID=<redacted>
GH_APP_PRIVATE_KEY_PATH=<path>
GH_WEBHOOK_SECRET=<redacted>
GH_PROJECT_ID=1
GH_ALLOWED_ORG=jlits
```

### Issue Template Requirements (Discovered)

Issues must include these sections with exact formatting:

```markdown
## Repositories
- org/repo-name-1
- org/repo-name-2

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

## Constraints
- Constraint 1
- Constraint 2
```

### Console Output Samples

**Successful Execution:**
```
info: GhOrchestrator.Host[0]
      Orchestration started: repo=jlits/gh-orchestrator-testing, issue=14, runId=run-14-20260125120149
info: GhOrchestrator.Host[0]
      Validation passed: ...
info: GhOrchestrator.Host[0]
      Claiming task: ...
info: GhOrchestrator.Host[0]
      Planning task run: ...
info: GhOrchestrator.Host[0]
      Planning complete: ..., repoCount=2
info: GhOrchestrator.Host[0]
      Executing task run: ...
info: GhOrchestrator.Host[0]
      Posting execution report: ...
info: GhOrchestrator.Host[0]
      Transitioning task to blocked: ...
info: GhOrchestrator.Host[0]
      Orchestration completed: ..., resultCount=2
```

**Quality Gate Rejection:**
```
info: GhOrchestrator.Host[0]
      Validation failed: repo=jlits/gh-orchestrator-testing, issue=14, reason=Issue body validation failed: ...
```

### Generated Code Quality

**React App (Excellent)**
- Custom implementation with useState hooks
- Proper component structure
- Functional styling
- Working counter demo
- Clean, readable code

**.NET API (Needs Work)**
- Proper project structure
- Controllers generated
- Missing NuGet package reference (Swashbuckle.AspNetCore)
- Build failed without manual intervention

### Recommendations for Next Iteration

1. **HIGH PRIORITY**: Switch to GitHub Projects V2 GraphQL API
   - Current REST API blocker prevents v0 completion
   - GraphQL is the recommended API for Projects V2
   - Will enable reliable Kanban state management

2. **MEDIUM PRIORITY**: Add startup validation
   - Validate OpenAI model compatibility at startup
   - Validate GitHub Projects V2 field configuration
   - Fail fast with clear error messages

3. **MEDIUM PRIORITY**: Improve generated code validation
   - Parse generated files for common issues
   - Check for missing dependencies
   - Validate build configuration

4. **LOW PRIORITY**: Enhance PR descriptions
   - Filter acceptance criteria by target repo
   - Add context about what was generated
   - Include build/run instructions

5. **FUTURE**: Implement repo context cloning
   - Provide AI with existing file structure
   - Enable better integration with existing code
   - Consider token usage implications

### Decisions Made

1. **Decision**: Stop debugging REST API, switch to GraphQL
   - **Rationale**: GitHub Projects V2 REST API is incomplete/unreliable for custom fields
   - **Impact**: Requires GraphQL client implementation
   - **Trade-off**: More complex client code, but reliable field access

2. **Decision**: Document findings and defer completion
   - **Rationale**: Working on GitHub API internals is outside project scope
   - **Impact**: v0 remains incomplete pending GraphQL implementation
   - **Trade-off**: Progress paused, but avoiding rabbit hole

3. **Decision**: Keep existing REST API code for now
   - **Rationale**: May be useful for read-only operations or fallback
   - **Impact**: Tech debt, but preserves investigation work
   - **Trade-off**: Code complexity vs. preservation of learning

### Open Questions

1. Does GitHub Projects V2 GraphQL API require different authentication?
2. Should we support both REST and GraphQL for gradual migration?
3. Is there a GitHub API client library that abstracts this?
4. Can we use GitHub CLI (gh) to avoid implementing GraphQL client?
5. Should we add integration tests before continuing manual testing?

### Artifacts

- Test Issue: https://github.com/jlits/gh-orchestrator-testing/issues/14
- Generated PRs: 
  - jlits/gh-orchestrator-testing#1
  - jlits/gh-orchestrator-testing2#1
- Debug logs: Captured in terminal output above
- Configuration: Environment variables documented above

### Next Steps

1. Research GitHub Projects V2 GraphQL API
2. Implement GraphQL client for field management
3. Update GetProjectTaskState to use GraphQL
4. Update UpdateProjectFields to use GraphQL
5. Retest Kanban state transitions
6. Continue manual testing iteration 2

### Status

**Iteration 1: COMPLETE**  
**v0 Status: BLOCKED** - Requires GraphQL API implementation  
**Overall Assessment**: Promising results, one critical blocker identified, multiple improvements needed

---

## Iteration 2 - TBD

*Next iteration will focus on GraphQL API implementation and retest Kanban state management.*
