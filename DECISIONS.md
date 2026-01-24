# Architectural Decisions

This file records architectural decisions that shape the system over time.

The goal is clarity and traceability, not completeness or perfection.
Decisions may be revised or deprecated as the system evolves.

---

## Decision: GitHub is the System of Record

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
The system needs a clear source of truth for:
- task definition
- task state
- approvals and audit history

Introducing a separate database or task store would duplicate state and increase operational complexity.

**Decision**  
GitHub (Issues, Projects, Pull Requests) is the system of record.
The orchestrator does not own canonical task state.

**Consequences**  
- ✅ Clear audit trail and traceability
- ✅ Fewer moving parts
- ❌ Some logic must adapt to GitHub’s data model and APIs
- ❌ Limited transactional guarantees compared to a database

This tradeoff is accepted for simplicity and transparency.

---

## Decision: Explicit Triggers Only (`/ai start`)

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Allowing AI execution to start implicitly (polling, label changes, background agents) increases the risk of:
- unintended execution
- unclear responsibility
- difficult debugging

**Decision**  
AI execution is triggered explicitly via a slash command:

```
/ai start
```

No other triggers are supported in v0.

**Consequences**  
- ✅ Predictable, auditable behavior
- ✅ Clear human intent
- ❌ Slightly more manual interaction
- ❌ Slower throughput compared to automation-heavy designs

This aligns with the project’s safety-first goals.

---

## Decision: Multi-Repository Support from v0

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Many real-world tasks span multiple repositories.
Deferring multi-repo support would require redesigning the execution and reporting model later.

**Decision**  
Multi-repo tasks are supported from the first version.
Each task may touch one or more repositories, explicitly listed.

**Consequences**  
- ✅ Avoids single-repo shortcuts that don’t scale
- ✅ Forces explicit scope definition
- ❌ Slightly more orchestration complexity
- ❌ Requires coordination during review and merge

This complexity is accepted to avoid later architectural churn.

---

## Decision: One Pull Request per Repository

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Multi-repo changes can be represented as:
- a single combined PR (not natively supported by GitHub), or
- one PR per repository

**Decision**  
The orchestrator creates **one pull request per repository** for each task.

**Consequences**  
- ✅ Reviewable, isolated changes
- ✅ Clean repository history
- ✅ Works with GitHub’s native review model
- ❌ Reviewers must coordinate merges across repos

This is considered the safest and most transparent approach.

---

## Decision: No Queue, Database, or Kubernetes Dependency in v0

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Queues, databases, and Kubernetes add operational overhead.
Early usage is expected to be low-volume and manually triggered.

**Decision**  
v0 intentionally avoids:
- message queues
- persistent databases
- Kubernetes as a required runtime dependency

GitHub state and in-memory execution are sufficient for v0.

**Consequences**  
- ✅ Low operational cost
- ✅ Easier local development
- ❌ Limited concurrency
- ❌ Fewer recovery options for partial failures

These tradeoffs are accepted until real usage justifies additional infrastructure.

---

## Decision: Human Review Is Mandatory

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Automatically merging AI-generated changes increases risk and reduces accountability.

**Decision**  
All AI-generated changes are proposed via pull requests.
Human review and explicit merge are always required.

**Consequences**  
- ✅ Clear ownership and accountability
- ✅ Safer change management
- ❌ Slower end-to-end execution

This is a non-negotiable guardrail.

---

## Decision: Stateless Orchestrator Design

**Status:** Accepted  
**Date:** 2026-01-15  

**Context**  
Maintaining internal state increases complexity and failure modes.

**Decision**  
The orchestrator should be stateless where possible.
Any internal state is transient and used only for idempotency or execution tracking.

**Consequences**  
- ✅ Easier deployment and recovery
- ✅ Fewer data consistency concerns
- ❌ Some edge cases must be handled carefully (e.g. retries)

This supports maintainability and operational simplicity.

---

## Decision: Implementation language is C# (.NET)

**Status:** Accepted  
**Date:** 2026-01-15  

**Rationale**
Maintainer expertise + maintainability + type safety.

**Consequences**
- .NET runtime dependency; avoid extra libs until needed.

---

## Decision: One Type per File

**Status:** Accepted  
**Date:** 2026-01-16  

**Context**  
Having multiple types (classes, interfaces, records, enums) in a single file can make:
- navigation and discovery harder
- merge conflicts more frequent
- code review more difficult
- testing and mocking less straightforward

However, enforcing strict separation increases file count and can feel rigid for tightly coupled helper types.

**Decision**  
Each public type must have its own file, named after the type.
Helper types should be nested as private classes/records/enums within the primary type if:
- they are only used by the primary type, and
- they are small (typically < 20 lines)

**Consequences**  
- ✅ Easier navigation and IDE tooling
- ✅ Cleaner git history and fewer merge conflicts
- ✅ Clearer dependency boundaries
- ❌ More files in the project
- ❌ Slightly more ceremony for small helper types

This rule applies to the core orchestrator codebase. Tests may co-locate helper types more freely when it improves readability.

---

## Decision: GitHub API surface (REST + GraphQL) for v0

**Status:** Superseded  
**Date:** 2026-01-20  
**Superseded by:** "Use REST API for GitHub Projects V2" (2026-01-18)  

**Context**  
The orchestrator must read/write Issues, create PRs, and update Projects v2 fields. GitHub Projects v2 APIs are only fully supported in GraphQL, while Issues and PRs are well-supported in REST.

**Decision**  
Use:
- **REST** for Issues, comments, branches, and pull requests
- **GraphQL** for GitHub Projects v2 field updates

**Consequences**  
- ✅ Clear mapping to GitHub’s supported surfaces
- ✅ Keeps project-field updates aligned with Projects v2 APIs
- ❌ Requires handling two API styles
- ❌ Requires GraphQL parsing and error handling in the client

This tradeoff is accepted for correctness and minimal scope.

**Superseded by:** Investigation revealed GitHub App installation tokens cannot query ProjectV2Item nodes via GraphQL despite having Projects permissions. See "Use REST API for GitHub Projects V2" decision below.

---

## Decision: Use REST API for GitHub Projects V2

**Status:** Accepted  
**Date:** 2026-01-18  

**Context**  
During manual testing of Plan 12, discovered that GitHub App installation tokens cannot query `ProjectV2Item` nodes via GraphQL, even with "Read and write access to organization projects" permission granted. Specifically:
- GraphQL query `project(number: N) { items { nodes { ... } } }` returns empty `nodes` array
- Same query works with personal access tokens (PATs)
- GitHub documentation is unclear about this limitation
- All authentication, permissions, and configuration verified correct

Investigation confirmed this is a scope limitation at the GitHub API level, not a code implementation issue. GraphQL ProjectV2 queries require user-level OAuth scope that GitHub App installation tokens do not have.

**Decision**  
Replace GraphQL-based Projects V2 integration with REST Projects v2 API:
- Use `GET /projects/{project_id}/items` for listing project items
- Use `GET /orgs/{org}/projects/{project_number}` for project metadata
- Use `PATCH /projects/{project_id}/items/{item_id}` for field updates

REST Projects v2 API explicitly documents GitHub App installation token support for organization-owned projects.

**Consequences**  
- ✅ Unblocks Plan 12 and downstream Plans 13-14
- ✅ REST API explicitly supports GitHub Apps with proper documentation
- ✅ Simpler error handling (HTTP status codes vs GraphQL errors)
- ✅ Less code complexity (no GraphQL parser/builder needed)
- ✅ Keeps existing REST client infrastructure
- ❌ REST API is less flexible than GraphQL (but flexibility not needed for v0)
- ❌ Requires pagination for large projects (acceptable tradeoff)
- ❌ Previous GraphQL implementation work discarded (sunk cost)

**Migration Path**  
1. Add REST helpers to `GitHubClient.cs`: `ListProjectItemsAsync()`, `UpdateProjectItemAsync()`
2. Replace `GetProjectTaskState()` implementation to use REST flow instead of GraphQL
3. Keep GraphQL code for project field schema queries if needed, or replace with REST equivalents
4. No configuration changes required (same authentication flow works for REST)

**References**  
- GitHub REST API docs: https://docs.github.com/en/rest/projects/projects
- Limitation analysis: [docs/iterations/v0/GITHUB_GRAPHQL_LIMIT.md](docs/iterations/v0/GITHUB_GRAPHQL_LIMIT.md)
- RFC: [docs/iterations/v0/rfc_unblocking_gh_orchestrator_project_v_2_access.md](docs/iterations/v0/rfc_unblocking_gh_orchestrator_project_v_2_access.md)
---

## Decision: Use OpenAI .NET SDK for AI worker integration

**Status:** Accepted  
**Date:** 2026-01-22  

**Context**  
Plan 22 requires a real AI worker implementation with structured prompts, retries, and timeouts.  
Implementing raw HTTP calls would re-create protocol concerns and increase maintenance overhead.

**Decision**  
Adopt the official OpenAI .NET SDK for v0 AI worker integration.  
Configuration is sourced from environment variables only, and the worker is optional (fallback to mock when not configured).

**Consequences**  
- ✅ Leverages a maintained SDK for authentication and request formatting  
- ✅ Keeps worker implementation small and reviewable  
- ✅ Explicit configuration and timeouts are enforced  
- ❌ Adds a new dependency and supply-chain surface area  
- ❌ Requires OpenAI API credentials at runtime (never stored in repo)


---

## Notes

Future decisions should:
- reference this file
- clearly state tradeoffs
- avoid retroactively justifying implementation shortcuts
