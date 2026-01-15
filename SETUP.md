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
