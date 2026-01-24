# Setup

This document describes how to run **GH Orchestrator** locally and how it is expected to be deployed.

It intentionally avoids implementation details that are not yet stable.
Secrets, credentials, and destructive operations are never included.

---

## Scope

This document covers:

- Local development setup
- High-level authentication requirements
- Running the orchestrator in a development environment

It does **not** cover:
- Production hardening
- Scaling strategies
- Kubernetes-specific configuration

Those topics are deferred until justified by real usage.

---

## Prerequisites

You will need:

- A GitHub account
- Permission to create a GitHub App (recommended)
- Access to the repositories you want the orchestrator to operate on
- A development environment capable of running the orchestrator (details depend on implementation)

---

## GitHub Integration (High-Level)

GH Orchestrator integrates with GitHub via webhooks and authenticated API access.

### Authentication model

- A **GitHub App** is the preferred authentication mechanism
- The app should be installed only on the organizations (and their repositories) it needs to access
- Permissions must follow the principle of least privilege

No personal access tokens should be required for normal operation.

> **Important:**  
> Do not commit credentials, private keys, or tokens to this repository.
> Secret handling is intentionally left to the deployment environment.

### Projects V2 requirement

GH Orchestrator uses **GitHub Projects V2** to track task state.
- The project **must** be created under an **organization**, not a personal account
  - Personal projects (`https://github.com/users/USERNAME/projects/N`) are not accessible to GitHub Apps
  - Org projects (`https://github.com/orgs/ORGNAME/projects/N`) are accessible
- Your GitHub App must have **Projects** permission set to at least **Read** (or **Read & write** to modify fields)
  - Check this in your app's **Permissions and events** settings
  - Both **Repository permissions** and **Organization permissions** should include Projects

### Critical: Same organization requirement

The **repository**, **project**, and **GitHub App installation** must all be in the **same organization**.

For example:
- ✅ **CORRECT**: App installed on `ExampleOrganization` org, repo `ExampleOrganization/orchestrator`, project in `ExampleOrganization` org
- ❌ **BROKEN**: App installed on `ExampleOrganization` org, repo `ExampleUser/orchestrator` (personal), project in `ExampleOrganization` org
  - The app cannot authenticate to personal repos, so webhooks will fail

**Before testing:**
1. Ensure your test repository is in the organization (not a personal fork)
2. Ensure the project is in the same organization
3. Ensure the app is installed on that organization (not just a personal account)

---

## Local Development (v0)

The expected local workflow is:

1. Clone this repository
2. Configure the orchestrator to receive GitHub webhook events
3. Start the orchestrator in development mode
4. Trigger execution via a GitHub Issue comment (`/ai start`)
5. Observe:
   - issue comments
   - project state updates
   - pull requests created by the orchestrator

### Configuration

The host reads configuration from environment variables or an `appsettings.Development.json`
file (via standard .NET configuration providers).

Required configuration keys:

- `GH_APP_ID` — GitHub App ID (integer)
- `GH_APP_PRIVATE_KEY_PATH` — Path to GitHub App private key file (PEM format)
  - Alternative: `GH_APP_PRIVATE_KEY` — Raw PEM contents
- `GH_WEBHOOK_SECRET` — Webhook secret configured in the GitHub App
- `GH_ALLOWED_ORG` — Organization name where the app is installed (authorization check)
- `GH_PROJECT_ID` — GitHub Projects V2 **project number** from the URL (passed directly to REST endpoints)

Optional AI worker configuration (OpenAI):

- `OPENAI_API_KEY` — OpenAI API key (if unset, the mock worker is used)
- `OPENAI_MODEL` — Model name (e.g. `gpt-4o-mini`)
- `OPENAI_TIMEOUT_SECONDS` — Per-request timeout in seconds (positive integer)
- `OPENAI_MAX_RETRIES` — Retry count for transient failures (non-negative integer)

#### Example `appsettings.Development.json` (do not commit)

```json
{
  "GH_APP_ID": "123456",
  "GH_APP_PRIVATE_KEY_PATH": "/absolute/path/to/gh-app-key.pem",
  "GH_WEBHOOK_SECRET": "your-webhook-secret",
  "GH_ALLOWED_ORG": "ExampleOrganization",
  "GH_PROJECT_ID": "1",
  "OPENAI_API_KEY": "your-openai-key",
  "OPENAI_MODEL": "gpt-4o-mini",
  "OPENAI_TIMEOUT_SECONDS": "30",
  "OPENAI_MAX_RETRIES": "2"
}
```

### Run commands

From the repository root:

```bash
cd src/GhOrchestrator.Host
dotnet run
```

The host listens on `http://localhost:5000` by default and expects webhook POSTs to `/webhook`.

---

## Manual Webhook Testing (Plan 5)

This section documents the **local/manual test setup** for the `/ai start` webhook flow.
It assumes you have a local webhook receiver that can call the core handler
(`IssueCommentWebhookHandler`) with the raw payload and signature header.
No secrets should be committed to this repository.

### 1) Create a GitHub App (permissions for branch + PR creation)

1. Go to **GitHub Settings → Developer settings → GitHub Apps → New GitHub App**.
2. Set a **Webhook URL** to your tunnel URL (see step 2) plus your webhook path.
3. Set a **Webhook secret** (store it securely; you will set `GH_WEBHOOK_SECRET` locally).
4. Subscribe to the **Issue comment** webhook event.
5. Set **Permissions** (branch + PR creation requires these):
   - **Repository permissions**
     - **Contents**: Read & write (needed to create branches)
     - **Pull requests**: Read & write (needed to open PRs)
     - **Issues**: Read (to read issue data)
     - **Metadata**: Read-only (required)
   - **Organization permissions**
     - **Projects**: Read & write (to update Projects V2 fields)
6. Save the app and **install it on your organization**, and ensure it is installed on **each target repository**.

> **Important:** The app must be installed at the organization level for it to access org projects.

---

### 2) Create a test repository and project in the same organization

1. In your organization, **create or transfer** a test repository
   - Do not use a personal fork—it must be under the org
2. Create a **Projects V2** board in the same organization
3. Add your test repo's issues to the board (or create test issues in the org)

**Repository prerequisites for Plan 13 (branch + PR):**
- Each repo must have a **default branch** with at least one commit (cannot create a branch from an empty repo)
- The GitHub App must be **installed on the repo** with the permissions above

> If you use a personal repo or project, the app will not have access and authentication will fail.

### 3) Expose a local webhook receiver

Use a tunneling tool to expose your local receiver (examples: ngrok, smee.io).

Example (ngrok):
```bash
ngrok http 5000
```

Use the generated HTTPS URL as the GitHub App webhook URL.

### 4) Configure required environment variables

The core config loader expects these variables:

- `GH_APP_ID` — GitHub App ID (integer)
- `GH_APP_PRIVATE_KEY_PATH` — Path to GitHub App private key file (PEM format)
  - Alternative: `GH_APP_PRIVATE_KEY` — Raw PEM contents (less recommended; avoid in scripts)
- `GH_WEBHOOK_SECRET` — Webhook secret configured in the GitHub App
- `GH_ALLOWED_ORG` — Organization name where the app is installed (authorization check)
- `GH_PROJECT_ID` — GitHub Projects V2 **project number** from the URL (e.g., `1` from `github.com/orgs/myorg/projects/1`)

#### Finding your Project V2 project number

The project number is the numeric value in your project's URL:
- URL: `https://github.com/orgs/jlits/projects/1` → project number is `1`
- URL: `https://github.com/orgs/mycompany/projects/42` → project number is `42`

Set `GH_PROJECT_ID` to this number (as a string).

**Note:** This is different from the global node ID (like `PVT_...`). The REST API uses the project number.

#### Example environment variables (do not commit):

```bash
export GH_APP_ID="123456"
export GH_APP_PRIVATE_KEY_PATH="$HOME/.ssh/gh-app-key.pem"
export GH_WEBHOOK_SECRET="your-webhook-secret"
export GH_ALLOWED_ORG="ExampleOrganization"
export GH_PROJECT_ID="1"  # Project number from URL, not node ID
export OPENAI_API_KEY="your-openai-key"
export OPENAI_MODEL="gpt-4o-mini"
export OPENAI_TIMEOUT_SECONDS="30"
export OPENAI_MAX_RETRIES="2"
```

### 5) Run the orchestrator locally

```powershell
cd src/GhOrchestrator.Host
dotnet run
```

The app will start listening on `http://localhost:5000`. Your tunnel (ngrok, etc.) should forward to this port.
Webhook payloads should be sent to `http://localhost:5000/webhook`. A health check is available at `http://localhost:5000/healthz`.

### 6) Trigger `/ai start`

1. Create or use a test issue in the organization repository (not a personal fork).
2. Ensure the issue is added to your organization's Projects V2 board.
3. Comment `/ai start` on the issue.
4. Confirm the receiver logs the parsed event data and signature validation status.
5. Check the app logs for successful project field updates (Status, AI=running, Run ID).

#### Validation checklist (manual)

- Issue comment appears with a run summary.
- Project fields update to `In Progress` and `AI=running` with a Run ID value.
- One branch and one PR are created per repository listed in the issue.
- The report comment includes PR links and any reported risks.
- **Note:** The current implementation does not apply AI-generated file changes yet; PRs will be empty scaffolds.

**Issue formatting required for validation and planning:**
- Repositories section:
  - Header: `## Repositories`
  - Bullet items: `- owner/repo`
- Acceptance criteria section:
  - Header: `## Acceptance Criteria`
  - Bullet items (e.g., `- [ ] Task completed`)
- Constraints section:
  - Header: `## Constraints`
  - Value can be `none` if no constraints

**Project field requirements:**
- `Status` (single-select)
- `AI` (single-select)
- `Run ID` (text)

---

## Deployment Notes

- The orchestrator is designed to be **self-hosted**
- It should run as a single service in v0
- No database, queue, or Kubernetes dependency is required in v0

Future deployment options will be evaluated only if operational needs justify them.

---

## Safety Notes

- All AI-generated changes must go through pull requests
- Human review is always required before merge
- Destructive actions are not executed without explicit human intent
- The orchestrator must reject tasks that fail the Task Quality Gate

These constraints are non-negotiable.

---

## Next Steps

For a deeper understanding of the system:

- See `ARCHITECTURE.md` for structure and trust boundaries
- See `DECISIONS.md` for architectural tradeoffs
- See `ai/README.md` for AI usage guidance
