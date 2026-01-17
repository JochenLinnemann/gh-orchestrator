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
- The app should be installed only on the repositories it needs to access
- Permissions must follow the principle of least privilege

No personal access tokens should be required for normal operation.

> **Important:**  
> Do not commit credentials, private keys, or tokens to this repository.
> Secret handling is intentionally left to the deployment environment.

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

Exact commands and configuration options will be documented once the implementation stabilizes.

---

## Manual Webhook Testing (Plan 5)

This section documents the **local/manual test setup** for the `/ai start` webhook flow.
It assumes you have a local webhook receiver that can call the core handler
(`IssueCommentWebhookHandler`) with the raw payload and signature header.
No secrets should be committed to this repository.

### 1) Create a GitHub App (minimal permissions)

1. Go to **GitHub Settings → Developer settings → GitHub Apps → New GitHub App**.
2. Set a **Webhook URL** to your tunnel URL (see step 2) plus your webhook path.
3. Set a **Webhook secret** (store it securely; you will set `GH_WEBHOOK_SECRET` locally).
4. Subscribe to the **Issue comment** webhook event.
5. Set **Permissions** to the minimum needed:
   - **Issues**: Read-only (needed to receive issue comment events)
   - **Metadata**: Read-only (required for all GitHub Apps)
6. Save the app and **install it** on the test repository only.

> If you need to post comments or update state later, increase permissions intentionally
> and document the reason.

### 2) Expose a local webhook receiver

Use a tunneling tool to expose your local receiver (examples: ngrok, smee.io).

Example (ngrok):
```bash
ngrok http 5000
```

Use the generated HTTPS URL as the GitHub App webhook URL.

### 3) Configure required environment variables

The core config loader expects these variables:

- `GH_APP_ID` — GitHub App ID (integer)
- `GH_APP_PRIVATE_KEY` — GitHub App private key (PEM contents)
- `GH_WEBHOOK_SECRET` — Webhook secret configured in the GitHub App

Example (do not commit):
```bash
export GH_APP_ID="123456"
export GH_APP_PRIVATE_KEY="$(cat /path/to/your/private-key.pem)"
export GH_WEBHOOK_SECRET="your-webhook-secret"
```

### 4) Run the orchestrator locally

Start your local webhook receiver (implementation-specific) and ensure it:

1. Listens on the port you exposed via the tunnel.
2. Extracts the raw request body and the `X-Hub-Signature-256` header.
3. Calls the core handler with `(payload, signatureHeader, GH_WEBHOOK_SECRET)`.

```
cd src/GhOrchestrator.Core
dotnet run
```

### 5) Trigger `/ai start`

1. Create or use a test issue in the installed repo.
2. Comment `/ai start` on the issue.
3. Confirm the receiver logs the parsed event data and signature validation status.

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
