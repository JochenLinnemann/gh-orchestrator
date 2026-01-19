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

## ✅ Plan 6: Task claiming + project updates

Goal: Implement claim logic and Kanban updates per Playbook.

Tasks (in order)
1. (agent) Implement claim operation: set `AI=running`, status to `In Progress`, and write `Run ID`.
2. (agent) Add tests for idempotent claim behavior and error handling.
3. (human) Manual test: confirm project fields update correctly in a test project.

---

## ✅ Plan 7: Multi-repo execution + PR creation (stubs first)

Goal: Create branch + PR per repo for a task run.

Tasks (in order)
1. (agent) Add a stub executor that creates branch names and PR payloads for each repo.
2. (agent) Add tests for branch naming (`ai/<run-id>/<short-slug>`) and one-PR-per-repo mapping.
3. (human) Manual test: validate generated branch names and PR titles against Playbook.

---

## ✅ Plan 8: Reporting back to GitHub

Goal: Post summary, PR links, and test instructions to the tracking issue.

Tasks (in order)
1. (agent) Add a formatter for the issue comment report (summary, how to test, PR links, risk notes).
2. (agent) Add tests for report formatting.
3. (human) Manual test: review comment content for clarity and missing risk notes.

---

## ✅ Plan 10: Host wiring + webhook endpoint (minimal runnable service)

Goal: Make `GhOrchestrator.Host` accept GitHub App webhooks and dispatch to Core.

Tasks (in order)
1. (agent) Add configuration binding for required environment (GitHub App ID, private key path, webhook secret, allowed org).
2. (agent) Implement minimal HTTP endpoint `POST /webhook` with request logging and 200/401 handling.
3. (agent) Integrate `GitHubWebhookSignatureVerifier` and reject invalid signatures early.
4. (agent) Parse `issue_comment` events into `IssueCommentEvent` and call `IssueCommentWebhookHandler`.
5. (human) Manual test: run locally with a tunnel, confirm `/ai start` events reach the handler.

---

## ✅ Plan 11: GitHub App auth + `IGitHubClient` implementation (v0 scope)

Goal: Implement minimal GitHub operations with GitHub App installation tokens.

Tasks (in order)
1. (agent) Implement JWT creation for GitHub App and installation token retrieval (cache short-lived tokens).
2. (agent) Implement `IGitHubClient` methods used in v0: read issue, post issue comment, update Project fields, create branch, open PR.
3. (agent) Choose API surface per Playbook: REST for issues/PRs, GraphQL for Projects v2 field updates; document in `DECISIONS.md`.
4. (agent) Add unit tests using a fake HTTP handler; avoid real network calls.
5. (human) Review scope against Playbook 3.4–3.5 and `ARCHITECTURE.md` boundaries.

---

## ✅ Plan 12: Claiming + Project updates (real GitHub I/O)

Goal: Move from stub to real Kanban updates per Playbook.

Tasks (in order)
1. (agent) Implement `TaskClaimPlanner` integration with `IGitHubClient` to set `AI=running`, status `In Progress`, and write `Run ID`.
2. (agent) Implement idempotency checks to avoid double-claim on retried webhooks.
3. (agent) Add tests for error handling and partial failures (e.g., missing fields).
4. (human) Manual test: verify field updates in a test Project via webhook trigger.

---

## ✅ Plan 13: Multi-repo branch + PR creation (real I/O)

Goal: One branch and one PR per repo using GitHub APIs.

Tasks (in order)
1. (agent) Implement default branch discovery and branch creation per repo with naming `ai/<run-id>/<short-slug>`.
2. (agent) Create PRs per repo with consistent titles and body (includes run metadata).
3. (agent) Handle per-repo errors without blocking others; report partial success.
4. (agent) Add tests for naming and payload shaping; use fakes.
5. (human) Manual test: run against two repos, confirm PRs and branch names.

---

## ✅ Plan 14: Reporting back on Issue (real I/O)

Goal: Post summary, PR links, how to test, and risk notes to the tracking Issue.

Tasks (in order)
1. (agent) Wire `IssueCommentReportFormatter` to `IGitHubClient` comment posting.
2. (agent) Include per-repo PR links and status in the comment.
3. (agent) Add tests for formatter content and escaping.
4. (human) Manual review: verify clarity and alignment to Playbook 3.6.

---

## 🟧 Plan 15: Setup, configuration, and runbooks (complete v0)

Goal: Ensure local/manual setup is fully documented and operable.

Tasks (in order)
1. (agent) Expand `SETUP.md` with environment variable names, example `appsettings.Development.json`, and local run commands.
2. (agent) Add a quick-start in `README.md` linking to setup and manual test steps.
3. (human) Validate setup instructions end-to-end with tunnel tooling and a test GitHub App.
4. (human) Update `ops/runbooks.md` with webhook retry handling and common failure modes.

---

## 🟧 Plan 16: Observability and reliability (v0 hardening)

Goal: Add basic health, structured logs, and guardrails without new infra.

Tasks (in order)
1. (agent) Add `/healthz` endpoint to Host for basic liveness.
2. (agent) Add structured logging to key stages (receive, verify, validate, claim, PR, report).
3. (agent) Apply `ai/checklists/reliability.md` and `ai/checklists/security.md`; document any intentional exceptions.
4. (human) Manual test: simulate invalid signatures and flaky webhook retries; confirm safe behavior.

---

## 🟧 Plan 17: v0 release criteria and cutover

Goal: Define and meet acceptance criteria for a “functional v0”.

Tasks (in order)
1. (human) Define v0 acceptance in `ROADMAP.md`: one Issue → claim → branches → PRs → report.
2. (agent) Ensure tests cover Critical Path; mark non-v0 tests as pending where appropriate.
3. (human) Execute Plan 9 with real repos; record outcomes and gaps.
4. (human) Create ADRs for any scope changes discovered.

---

## ⏭️ Plan 18: Post-v0 refinements (later)

Goal: Capture future enhancements without expanding v0.

Tasks (in order)
1. (human) Document candidates: label triggers, `/ai plan` mode, queue/persistence, CI-fix loop.
2. (human) Prioritize after v0 feedback; keep `DECISIONS.md` up to date.

---

## 🟧 Plan 9: End-to-end manual verification (v0 MVP)

Goal: Validate the v0 flow with a real GitHub issue and project.

Tasks (in order)
1. (human) Create a test issue with Repos, Acceptance Criteria, Constraints.
2. (human) Add issue to the Project and confirm required fields exist.
3. (human) Comment `/ai start` and verify end-to-end behavior: validation, claim, PRs, and report.
4. (human) Report results and any discrepancies back to the agent.
