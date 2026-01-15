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

## Notes

Future decisions should:
- reference this file
- clearly state tradeoffs
- avoid retroactively justifying implementation shortcuts
