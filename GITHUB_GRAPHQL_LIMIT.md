# GitHub GraphQL ProjectV2 Limitation with GitHub Apps

**Date:** January 18, 2026  
**Status:** Blocker for Plan 12 (Claiming + Project updates)  
**Severity:** High  

---

## Summary

GitHub Apps **cannot query ProjectV2Item nodes** through GraphQL, even when granted "Projects" permissions. This blocks the ability to:
1. Find issues added to Projects V2 boards
2. Read/update custom field values on project items

The same queries work perfectly with **personal access tokens (PAT)**, indicating a limitation specific to GitHub App token scopes.

---

## What We Tried

### 1. Permission Configuration ✅
- Set GitHub App permissions: **Projects** (Read & Write)
- Applied to both **Repository** and **Organization** permissions
- Installed app on both organization and specific repository
- Uninstalled and reinstalled app multiple times
- **Result:** No effect. App tokens still blocked from querying items.

### 2. Query Structure ✅
- Verified GraphQL query syntax is valid
- Query works with personal access tokens
- Query fails consistently with app tokens
- **Result:** Query is correct; token scope is the issue.

### 3. Authentication ✅
- Confirmed app is properly installed
- Confirmed installation tokens are being generated correctly
- Confirmed app can access other GitHub resources (issues, branches, etc.)
- **Result:** Authentication works; it's a scope limitation.

---

## Root Cause

GitHub Apps have **restricted access to Projects V2 through GraphQL**.

### Evidence

**With GitHub App token:**
```graphql
query($projectId: ID!) {
  node(id: $projectId) {
    ... on ProjectV2 {
      items(first: 100) { nodes { id content { ... on Issue { number } } } }
    }
  }
}
```
**Response:** `{"data":{"node":{"items":{"nodes":[]}}}` (empty, even when issues are visible on the board)

**With personal access token (same query):**
```json
{"data":{"node":{"items":{"nodes":[{"id":"...", "content":{"number":1}}]}}}}
```
**Response:** Returns items correctly

### Why?

GitHub Apps use **installation tokens** which are scoped by:
- Repository membership
- Explicitly granted permissions
- GitHub's internal access control rules

ProjectV2Item queries appear to require **elevated access** that GitHub Apps don't automatically get, even with "Projects" permission. This may be by design (security) or an unimplemented feature.

---

## Impact

### Plan 12: Task Claiming (Blocked)
- **Goal:** Read project task state and update Status/AI/RunId fields
- **Blocked at:** Cannot find issue on project board via `GetProjectTaskState()`
- **Error:** `"Project item for issue N not found"` (even though issue is on the board)

### Code Location
- [src/GhOrchestrator.Host/GitHubClient.cs](src/GhOrchestrator.Host/GitHubClient.cs#L194-L232) - `GetProjectMetadata()` method
- [src/GhOrchestrator.Core/TaskClaimService.cs](src/GhOrchestrator.Core/TaskClaimService.cs#L5-L45) - Uses `GetProjectTaskState()`

### Downstream Plans
- Plan 13 (Multi-repo branch + PR creation) - Requires Plan 12 to succeed first
- Plan 14 (Issue reporting) - Depends on claiming flow
- v0 Release - Cannot complete without working task claim flow

---

## Options Forward

### Option 1: Use Personal Access Token (PAT) Locally, Document for Production
**Effort:** Low (1-2 hours)  
**Risk:** Low  
**Pros:**
- Unblocks Plan 12 testing immediately
- Validates that claiming logic works correctly
- Clear path to production decision later

**Cons:**
- Not production-ready (PATs are user-scoped, not app-scoped)
- Defers the real architectural decision

**Recommendation:** ✅ **Do this first** to validate the implementation works

---

### Option 2: Use GitHub App + Issue Labels Instead of Projects V2
**Effort:** Medium (4-6 hours)  
**Risk:** Medium  
**Pros:**
- GitHub Apps have full REST API support for labels
- No GraphQL needed; REST API is simpler
- Standard GitHub workflow (labels are familiar)

**Cons:**
- Loses Projects V2 board visibility (fields, status columns, etc.)
- Requires redesign of state tracking model
- Users must check issue labels, not project board

**Implementation:**
- Replace `Status`/`AI`/`RunId` project fields with issue labels
- Use REST API to read/write labels (no app token restrictions)
- Example: `ai-running`, `ai-completed`, `ai-run-20260118083045`

---

### Option 3: Hybrid Approach (GraphQL for Updates, REST for Discovery)
**Effort:** Medium (3-4 hours)  
**Risk:** Medium  
**Pros:**
- Keep Projects V2 board visibility
- Use REST API for issue lookup (no token restrictions)
- Avoids item querying bottleneck

**Cons:**
- Adds complexity (two API surfaces)
- Still relies on GitHub eventually fixing ProjectV2Item scopes

**Implementation:**
```csharp
// 1. Look up issue via REST API (app tokens work fine)
GET /repos/{owner}/{repo}/issues/{issue_number}

// 2. Verify it exists and get node ID
// 3. Query project separately (read fields)
// 4. Update fields via GraphQL mutation (if this works with app tokens)
```

**Risk:** Mutations might also be blocked. Unproven.

---

### Option 4: Wait for GitHub to Fix App Token Scopes
**Effort:** None  
**Risk:** Very High  
**Pros:**
- No code changes needed

**Cons:**
- No timeline from GitHub
- Blocks all v0 testing indefinitely
- Not viable for deadline

---

## Recommendation

**Phase 1 (Immediate):** Use **Option 1** (PAT locally)
- Validates claiming logic works
- Takes 1-2 hours
- Unblocks Plan 13-14 testing

**Phase 2 (Before Production):** Evaluate **Option 2** (Labels) or **Option 3** (Hybrid)
- Make this decision based on UX requirements
- Option 2 is simpler; Option 3 keeps Projects V2 board
- Requires architect input on priorities

---

## References

- GitHub API: https://docs.github.com/en/graphql
- Projects V2: https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects
- GitHub Apps Auth: https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app
- Known limitation (undocumented): GitHub Apps restricted from ProjectV2Item queries

---

## Questions for Architecture Review

1. **Priority:** Is Projects V2 board visibility a hard requirement, or is hidden state tracking acceptable?
2. **Scope:** Should we support Projects V2 in v0, or defer to v1 after GitHub fixes token scopes?
3. **UX:** If we switch to labels, how should users interact with task state (CLI, issue comments, custom dashboard)?
4. **Timeline:** Can we unblock Plan 12 with PATs locally and decide on production path later?

