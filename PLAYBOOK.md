# GitHub + Cloud AI Orchestration Playbook

*A practical, GitHub-first operating model for running AI workers (Codex / OpenAI models) against multi-repo projects using a Kanban board and self-hosted orchestration.*

AI guidance here must not conflict with ARCHITECTURE.md, DECISIONS.md, or this Playbook.

> **Implementation Status (v0):** All sections in this playbook have been implemented in code. See `v0_MANUAL_TESTING.md` for current status and testing checklist.

---

## 1) Goal

Build a reliable, auditable workflow where:

- **GitHub is the system of record** (tasks, state, approvals, history).
- **AI workers are disposable executors** (do work, report back, stop).
- **A small self-hosted orchestrator** connects GitHub events to OpenAI and produces PRs.
- The workflow supports **multiple repositories per task**.

### Explicit non-goals

- No autonomous background agents.
- No hidden state outside GitHub unless explicitly justified.
- No destructive actions without explicit human intent.

This design intentionally favors **simplicity, safety, and reviewability** over autonomy.

---

## 2) Core Principles

1. **GitHub owns state**

   - Issues / PRs / Projects store the truth.
   - The orchestrator should be stateless where possible.

2. **Deterministic triggers beat polling**

   - Prefer explicit labels, project fields, or slash commands.

3. **One task, one execution**

   - An AI worker receives a single, bounded task with clear acceptance criteria.

4. **Human-in-the-loop by default**

   - All AI changes are proposed via PRs.
   - Humans approve merges.

5. **Least privilege & safety first**

   - GitHub App auth with minimal scopes.
   - No secrets printed, logged, or exposed to models.
   - No destructive commands without explicit confirmation.

6. **Small, reviewable changes**

   - Prefer incremental improvements over refactors.
   - Avoid introducing new dependencies unless clearly justified.

---

## 3) The Workflow (End-to-End)

> **Assumption (explicit):** Multi-repo tasks are supported from day one. Each task may touch one or more repositories, and this is treated as a first-class concern.

### 3.1 Kanban = GitHub Projects v2

Use a single **GitHub Project** (org or user) as the Kanban board.

**Columns / Status field**

- `Todo`
- `In Progress`
- `Done`

**Recommended custom fields**

- `AI` (single-select): `none | ready | running | blocked | done`
- `Repos` (text): comma-separated list, e.g. `org/repo-a, org/repo-b`
- `Priority` (single-select)
- `Agent` (single-select): `openai-worker | human`
- `Run ID` (text)

**Rules**

- Every card must reference a real **Issue or PR**.
- Multi-repo tasks MUST list all repos explicitly.
- The orchestrator refuses tasks with missing or ambiguous repo lists.

---

### 3.2 Task Authoring

Each task lives as a **GitHub Issue** (preferred) with:

- Problem statement
- Acceptance criteria (explicit checklist)
- Repo(s) involved (also mirrored in the Project `Repos` field)
- Constraints (e.g., “no schema changes”, “touch only /src”)
- Links to context (docs, designs, incidents)

---

### 3.3 Triggering AI Work (v0)

> **v0 constraint:** Only a single, explicit command is supported to keep behavior predictable and easy to review.

**Supported trigger (v0)**

- **Slash command** on the tracking Issue:
  - `/ai start`

**Not supported in v0 (reserved for later)**

- `/ai plan`
- `/ai implement`
- Label-based triggers
- Project-field-based triggers

These may be introduced in later versions **only if justified** by real usage and review burden.

---

### 3.4 Orchestrator Responsibilities

When a trigger occurs, the orchestrator:

1. **Validates** task eligibility

   - Issue open, not already running
   - Required fields present (`Repos`, acceptance criteria)

2. **Claims** the task atomically

   - Set `AI = running`
   - Move Status → `In Progress`
   - Write `Run ID`

3. **Prepares execution workspace**

   - For each repo involved:
     - Create a branch from default branch
     - Ensure CI configuration known

4. **Invokes AI worker**

   - Provide:
     - Task summary + acceptance criteria
     - Repo list and constraints
     - “Definition of Done”
     - Policies (security, formatting, testing)

5. **Applies changes and opens PR(s)**

   - **One PR per repo** (required)

6. **Reports back**

   - Comment on Issue with:
     - What changed
     - How to test
     - Links to PRs
     - Risk notes

7. **Updates Kanban state**

   - `AI = blocked` until PRs are reviewed
   - Status → `Done` only after merge

---

## 3.5 Task Quality Gate (Required)

Before any AI work starts, the orchestrator MUST reject the task unless all of the following are true:

- The Issue exists and is open
- Acceptance criteria are present and explicit
- The `Repos` field is present and non-empty
- Repos listed are unambiguous and accessible
- Constraints are stated (or explicitly marked as `none`)
- No instructions request destructive actions

**Rationale**

- Prevents ambiguous or unsafe execution
- Keeps runs small and reviewable
- Forces planning problems to be solved before execution

Tasks failing this gate must receive a **clear rejection comment** explaining what is missing.

---

## 4) Operating Modes

> **Important:** In v0, there is exactly **one** operating mode. Additional modes are future extensions, not defaults.

### Mode (v0): Implement with Guardrails

- Trigger: `/ai start`
- Behavior:
  - Validate task via the **Task Quality Gate**
  - Execute implementation directly
  - Open PR(s)
  - Report risks and tradeoffs explicitly

This keeps the system simple, deterministic, and easy to reason about.

---

## 4.x Future Operating Modes (Out of Scope for v0)

The following modes are intentionally **not implemented** in v0:

- **Plan-first mode** (`/ai plan` → `/ai implement`)
- **CI-fix loop** (`ai:fix-ci`)

They are documented only as possible future extensions and must not influence v0 architecture or API design.

---

## 5) Multi-Repo Task Pattern (Required)

> **Supported from v0. No single-repo-only shortcut exists.**

### Pattern: One Tracking Issue + One PR per Repo

- A single **tracking Issue** represents the task.
- The Project card points to this Issue.
- The `Repos` field lists all repositories involved.
- The orchestrator creates:
  - one working branch per repo
  - one PR per repo

**Why this pattern is mandatory**

- Keeps repo history clean
- Keeps PRs reviewable
- Avoids cross-repo coupling
- Maps cleanly to GitHub permissions

**Tradeoff**

- Requires reviewers to coordinate merges
- Slightly more orchestration logic

This tradeoff is accepted for clarity and safety.

---

## 6) Guardrails (Non-negotiables)

- **No direct pushes to default branch**
- **PRs required** for all AI changes
- **Branch naming convention**: `ai/<run-id>/<short-slug>`
- **Max scope**: refuse tasks that don’t list repos or acceptance criteria
- **Secrets**: AI never sees production secrets; only CI-safe tokens
- **Safety**: never execute destructive infrastructure changes without explicit approvals

---

## 7) What This Enables

- A clean Kanban experience in GitHub Projects
- Deterministic, auditable AI executions
- Multi-repo coordination without agent “memory”
- Easy rollback and human review

---

## 8) What This Does NOT Try To Do

- No background agent continuously polling the board
- No long-lived memory inside the model
- No “auto-merge everything” by default

---

## 9) Next Steps (Implementation Roadmap)

> Scope is intentionally minimal. Each step must be reviewable in isolation.

### Step 1: Define v0 Architecture (Multi-Repo)

**Components**

- Orchestrator API (single service)
- Worker execution (same process or forked job)

**Explicit exclusions (v0)**

- No queue
- No database unless strictly required
- No Kubernetes dependency at runtime

### Step 2: Choose one trigger

- Issue comment: `/ai start`

The orchestrator ignores all other signals in v0.

### Step 3: GitHub integration

- GitHub App (preferred)
- Webhook receiver
- Minimal permissions only

### Step 4: Execution flow

- Validate Issue
- Parse repo list
- Claim task atomically
- For each repo:
  - create branch
  - apply changes
  - open PR

### Step 5: Reporting

- Comment on Issue with:
  - PR links (one per repo)
  - summary of changes
  - test instructions

### Step 6: Re-evaluate

- Add persistence, queues, or K8s **only if justified by load or failure modes**

---

## Appendix: Explicit Assumptions (v0)

- GitHub is always available and authoritative
- Human review is required before merge
- Multi-repo tasks are coordinated via the tracking Issue, not automation
- Failure in one repo does not auto-roll back others; this is handled manually

---

## Appendix: Document Boundaries

This playbook intentionally does **not** define:

- Orchestrator HTTP APIs or internal schemas
- Deployment manifests (Docker / Kubernetes)
- Detailed OpenAI prompt templates
- GitHub GraphQL query specifics

Those belong in **root-level documents**, not in this playbook:

- `ARCHITECTURE.md` — system structure, trust boundaries, failure modes
- `DECISIONS.md` — architectural decision records (ADRs)
- `SETUP.md` — local development and deployment instructions
- `ai/README.md` — AI usage guidance, prompts, and guardrails

The playbook is the **behavioral contract**, not the implementation.
