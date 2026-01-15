# Architecture Overview

This document describes the v0 architecture of **GH Orchestrator**.

It focuses on structure, responsibilities, and trust boundaries.  
It intentionally avoids implementation details, APIs, or deployment specifics.

---

## System Goals

The system is designed to optimize for:

- **Simplicity**  
  Prefer the smallest architecture that satisfies requirements.

- **Predictability**  
  All behavior must be explicit, deterministic, and easy to reason about.

- **Safety and reviewability**  
  All AI-generated changes flow through pull requests and human review.

- **GitHub-first state management**  
  GitHub Issues, Projects, and PRs are the system of record.

- **Low operational overhead**  
  Avoid always-on infrastructure unless justified by real usage.

---

## Non-Goals

This system is explicitly **not** designed to:

- Run autonomous or self-directing AI agents
- Continuously poll GitHub or act without explicit triggers
- Maintain hidden or implicit state outside GitHub
- Automatically merge AI-generated changes
- Optimize for high throughput or large-scale concurrency in v0
- Replace human ownership or accountability

---

## High-Level View

At a high level, GH Orchestrator sits between GitHub and AI execution.

```
GitHub (Issues / Projects / PRs)
    |
    | (explicit trigger: /ai start)
    v
Orchestrator Service
    |
    | (bounded task + repo list)
    v
AI Worker
    |
    | (branches + PRs)
    v
GitHub (Pull Requests + Comments)
```

GitHub remains the authoritative source for:
- task definition
- task state
- approvals and review

The orchestrator coordinates execution but does not own long-term state.

---

## Major Components

### 1. Orchestrator

**Responsibility**
- Receive GitHub webhook events
- Validate and claim tasks
- Enforce guardrails and task quality gates
- Coordinate execution across multiple repositories
- Report results back to GitHub

**Inputs**
- GitHub webhook events (Issue comments)
- Task metadata from Issues and Projects

**Outputs**
- Task state updates in GitHub Projects
- Comments on Issues
- Creation of execution runs

**Data Ownership**
- Does not own canonical task state
- Uses GitHub as the source of truth
- May maintain minimal transient state for idempotency

---

### 2. Worker (Execution)

**Responsibility**
- Execute a single task run
- Apply changes to one or more repositories
- Produce pull requests
- Report results and risks

**Inputs**
- Bounded task description
- Explicit repository list
- Acceptance criteria and constraints

**Outputs**
- One pull request per repository
- Execution summary (commented back to the Issue)

**Data Ownership**
- No persistent state
- Workspace is ephemeral and disposable

---

### 3. GitHub

**Responsibility**
- Task definition (Issues)
- Task tracking (Projects / Kanban)
- Review and approval (PRs)
- Audit trail

GitHub is the system of record.

---

## External Dependencies

### Required (v0)

- **GitHub**
  - Issues
  - Projects v2
  - Pull Requests
  - Webhooks
  - GitHub App authentication

- **AI provider**
  - Used only for bounded execution
  - No memory or autonomous behavior

### Explicitly excluded in v0

- Databases
- Message queues
- Background schedulers
- Kubernetes as a runtime dependency

These may be introduced later **only if justified by real needs**.

---

## Scaling Assumptions

### v0 assumptions

- Low to moderate task volume
- Tasks triggered manually and explicitly
- One execution per task at a time
- Multi-repo tasks are common but bounded

### Implications

- Concurrency is limited by design
- Throughput is less important than correctness
- Horizontal scaling is not required in v0

---

## Constraints

v0 core logic must be framework-agnostic and testable (pure functions). Webhook/HTTP integration is a thin outer layer later.

### Budget
- Minimize infrastructure and operational costs
- Avoid always-on services unless necessary

### Team size
- Designed for small teams or individual maintainers
- Architecture should be understandable by one engineer

### Security & Compliance
- Least-privilege GitHub App permissions
- No secrets exposed to AI models
- No destructive actions without explicit human intent
- All changes traceable to GitHub artifacts

---

## Summary

This architecture deliberately favors:

- clarity over cleverness
- explicit coordination over autonomy
- reviewability over speed

Future extensions must not compromise these properties.
