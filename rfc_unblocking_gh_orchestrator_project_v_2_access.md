# RFC: Unblocking GH Orchestrator ProjectV2 Access (GitHub App)

## Status
Proposed

## Context
GH Orchestrator treats **GitHub as the system of record** and uses **Projects v2** as the canonical Kanban for work orchestration.

Plan 12 is currently blocked because **GitHub App installation tokens cannot reliably read ProjectV2 items via GraphQL**. Specifically, GraphQL queries against `ProjectV2.items` return empty results when authenticated as a GitHub App, even though the same queries succeed with a user PAT.

This RFC proposes a minimal, safe change that preserves the architecture and avoids introducing PATs.

---

## Problem Statement
- We need to **discover and update ProjectV2 items** (claim tasks, move status, write Run IDs).
- GitHub Apps **lack sufficient GraphQL access** to ProjectV2 items.
- Switching to PATs would violate the security and ownership model of GH Orchestrator.

---

## Proposed Solution
**Replace GraphQL ProjectV2 reads and writes with the official Projects v2 REST API**, which explicitly supports **GitHub App installation access tokens** for **organization-owned projects**.

Key idea:
- **GitHub remains the system of record**
- **AI remains a constrained worker**
- We change *how* we talk to Projects v2, not *what* we do

---

## Scope (Minimal / Plan-12 Sized)
This RFC only affects:
- Project item discovery
- Project item field updates ("claim" operation)

No changes to:
- Repo operations
- Issue lifecycle
- Playbook semantics
- Execution guardrails

---

## Technical Design

### Assumptions
- Projects v2 are **organization-owned** (not user-owned)
- GitHub App has:
  - Access to the org
  - Access to the repos referenced by the project

---

### API Changes

#### Before (Blocked)
- GraphQL:
  - `node(id: ProjectV2) { items { ... } }`
  - Returns empty for GitHub App tokens

#### After (Proposed)
- REST Projects v2 API:
  - List project items (paginated)
  - Update project item fields

Both operations are supported with **GitHub App installation tokens** for org projects.

---

### Item Discovery Flow
1. Fetch the Issue via REST:
   - `/repos/{owner}/{repo}/issues/{number}`
2. Extract the Issue identifier used by Project items
3. List Project items:
   - `/orgs/{org}/projectsV2/{project_number}/items`
4. Match Project item → Issue via the `content` reference

This replaces `GetProjectTaskState()`'s GraphQL dependency.

---

### Claim / Update Flow
Once the Project item is identified:
- Update fields via REST:
  - Status → `In Progress`
  - AI state → `running`
  - Write Run ID / metadata

All updates are done via REST against the Project item.

---

## Why This Works
- GitHub explicitly supports **Projects v2 REST APIs with GitHub Apps**
- Avoids undocumented GraphQL behavior
- Avoids PATs
- Keeps Projects v2 as the single source of truth
- Aligns with existing Playbook intent

---

## Risks & Mitigations

### Risk: Item matching ambiguity
- Mitigation: Match strictly on Issue identifiers exposed in Project item `content`

### Risk: Pagination performance
- Mitigation: Acceptable for MVP; filtering and caching can be added later

### Risk: User-owned projects
- Mitigation: Explicitly out of scope; org-owned projects only

---

## Rollout Plan
1. Add config flag: `ProjectsApiMode = RestV2`
2. Implement REST helpers:
   - `ListProjectItems()`
   - `UpdateProjectItem()`
3. Switch `GetProjectTaskState()` to REST
4. Enable by default in dev
5. Promote to prod after validation

---

## Alternatives Considered

### Use PATs
- Rejected: breaks security model and ownership guarantees

### Hybrid GraphQL + REST
- Rejected: still blocked on read path

### Wait for GitHub GraphQL fix
- Rejected: timeline unknown

---

## Open Questions
- Exact identifier used to correlate Issue ↔ Project item (node ID vs database ID)
- Whether we want to enforce org-owned project validation at startup

---

## Summary
This RFC unblocks Plan 12 with a **small, explicit, well-supported change**:

> **Use Projects v2 REST APIs instead of GraphQL for Project item reads and writes when authenticated as a GitHub App.**

No architectural pivots. No PATs. No agents.

Just an execution-layer fix with guardrails.

