# TODO

Focused, incremental plans for small changes. Each plan lists tasks in order with owner (agent or human).

---

## ✅ Plan 1: Wire preflight into orchestration (pure functions only)

Goal: Keep validation deterministic and fully in-process, no GitHub I/O.

Tasks (in order)
1. (agent) Add a new result type that combines Task Quality Gate + Preflight outcomes.
2. (agent) Update `Orchestrator.ProcessIssueComment()` to run `RunPreflight.Validate()` after `TaskQualityGate.Validate()`.
3. (agent) Add unit tests covering the new combined outcome (success, issue closed, destructive intent escalation).
4. (human) Review behavior and ensure messages are acceptable for user-facing comments.
5. (human) Manual test: run unit tests and report failures or unclear error messages.

---

## ✅ Plan 2: Define thin GitHub boundary (interfaces only)

Goal: Prepare integration without implementing I/O or adding dependencies.

Tasks (in order)
1. (agent) Add minimal interfaces for GitHub operations (read issue, comment, update project, open PR).
2. (agent) Add unit tests for orchestrator wiring using a fake implementation of the interface.
3. (human) Review interface scope to ensure it maps cleanly to the Playbook and ARCHITECTURE.
4. (human) Manual test: confirm interface coverage against Playbook steps 3.3–3.4.

---

## ✅ Plan 3: Add minimal execution flow stub (no external calls)

Goal: Create a skeletal execution pipeline that can later be connected to real GitHub and worker I/O.

Tasks (in order)
1. (agent) Add a `TaskRunPlan` record capturing run id, repo list, and execution steps.
2. (agent) Add pure-function “planner” that validates and produces a `TaskRunPlan`.
3. (agent) Add tests covering multi-repo planning and run-id formatting.
4. (human) Confirm plan structure matches the Playbook’s one-PR-per-repo rule.
5. (human) Manual test: review run-id format and branching convention against Playbook.

---

## ✅ Plan 4: Roadmap and context hygiene

Goal: Align docs with actual implementation status and next steps.

Tasks (in order)
1. (agent) Fill in `ROADMAP.md` with “Now/Next/Later” tied to these plans.
2. (human) Fill in `ai/context.md` with current team and infra assumptions.
3. (human) Confirm that the update does not imply features outside v0 scope.
4. (human) Manual test: verify docs remain consistent with PLAYBOOK and DECISIONS.

---

## ✅ Plan 5: Webhook handling + GitHub App auth (v0 trigger only)

Goal: Accept `/ai start` from issue comments via GitHub App webhooks.

Tasks (in order)
1. (agent) Add a minimal webhook handler and event model for issue comments.
2. (agent) Add GitHub App auth wiring (no secrets in repo; configuration via environment).
3. (agent) Add tests for webhook parsing and signature verification (if implemented).
4. (human) Manual test: trigger `/ai start` on a test issue and confirm the handler receives and logs the event.

---

## ✅ Plan 5.1: Manual test setup instructions (webhooks)

Goal: Document how to set up a local/manual test environment for Plan 5’s `/ai start` webhook flow.

Tasks (in order)
1. (agent) Add explicit setup steps to `SETUP.md` for local webhook testing, including:
   - Creating/configuring a GitHub App with minimal permissions
   - Exposing a local webhook receiver (e.g., via a tunneling tool)
   - Required environment variables and configuration
   - How to run the orchestrator locally
2. (human) Confirm the instructions are sufficient to run the manual test in Plan 5.

---

## 🟧 Plan 6: Task claiming + project updates

Goal: Implement claim logic and Kanban updates per Playbook.

Tasks (in order)
1. (agent) Implement claim operation: set `AI=running`, status to `In Progress`, and write `Run ID`.
2. (agent) Add tests for idempotent claim behavior and error handling.
3. (human) Manual test: confirm project fields update correctly in a test project.

---

## 🟧 Plan 7: Multi-repo execution + PR creation (stubs first)

Goal: Create branch + PR per repo for a task run.

Tasks (in order)
1. (agent) Add a stub executor that creates branch names and PR payloads for each repo.
2. (agent) Add tests for branch naming (`ai/<run-id>/<short-slug>`) and one-PR-per-repo mapping.
3. (human) Manual test: validate generated branch names and PR titles against Playbook.

---

## 🟧 Plan 8: Reporting back to GitHub

Goal: Post summary, PR links, and test instructions to the tracking issue.

Tasks (in order)
1. (agent) Add a formatter for the issue comment report (summary, how to test, PR links, risk notes).
2. (agent) Add tests for report formatting.
3. (human) Manual test: review comment content for clarity and missing risk notes.

---

## 🟧 Plan 9: End-to-end manual verification (v0 MVP)

Goal: Validate the v0 flow with a real GitHub issue and project.

Tasks (in order)
1. (human) Create a test issue with Repos, Acceptance Criteria, Constraints.
2. (human) Add issue to the Project and confirm required fields exist.
3. (human) Comment `/ai start` and verify end-to-end behavior: validation, claim, PRs, and report.
4. (human) Report results and any discrepancies back to the agent.
