# Runbooks

This document describes how to respond to common operational issues.

## Principles
- Prefer simple, repeatable steps
- Optimize for clarity under pressure
- Assume the reader is tired

## Common Scenarios
### Service is unhealthy
- How to confirm
- How to mitigate
- How to escalate

### Webhook retries and duplicate delivery
- Symptoms
  - Repeated `issue_comment` deliveries for the same event
  - Multiple attempts logged for a single delivery ID
- Likely causes
  - Receiver returned non-2xx response
  - Network timeout between GitHub and the host
  - Host restarted mid-request
- Immediate handling
  1. Check the host logs for the delivery ID and response status.
  2. Confirm the webhook endpoint returned `200 OK` for valid signatures.
  3. If the host rejected the signature, verify the configured `GH_WEBHOOK_SECRET`.
  4. If the host crashed or timed out, restart the service and re-send the event from GitHub.
- Expected system behavior
  - The orchestrator should be idempotent for claim and update operations.
  - Duplicate deliveries should not create duplicate PRs or repeated project updates.
- Escalation
  - If duplicates still create side effects, capture logs and file a bug with the delivery ID.

### Webhook signature failures
- Symptoms
  - `401` responses on `/webhook`
  - Log entries indicating invalid `X-Hub-Signature-256`
- Likely causes
  - `GH_WEBHOOK_SECRET` mismatch
  - Webhook payload modified by a proxy
- Immediate handling
  1. Confirm the secret in the GitHub App matches `GH_WEBHOOK_SECRET`.
  2. Ensure the receiver uses the raw request body for signature verification.
  3. Re-send the delivery from GitHub after fixing configuration.

### Missing or unauthorized organization
- Symptoms
  - Requests rejected with organization not allowed
  - Logs mention `GH_ALLOWED_ORG`
- Likely causes
  - App installed on the wrong org
  - `GH_ALLOWED_ORG` does not match the issue repo org
- Immediate handling
  1. Confirm the GitHub App is installed on the org that owns the repo.
  2. Verify `GH_ALLOWED_ORG` matches the org name exactly.

### Project updates fail
- Symptoms
  - Issue comment posted but project fields do not change
  - Logs indicate missing project items or `404` responses
- Likely causes
  - Project is not an org Project V2
  - `GH_PROJECT_ID` is incorrect (project number vs node ID)
  - App lacks Projects permission
- Immediate handling
  1. Confirm the project is an org Project V2 board.
  2. Verify `GH_PROJECT_ID` is the project number from the URL.
  3. Confirm the GitHub App has Projects read/write permissions.

### Branch or PR creation fails
- Symptoms
  - No PR links in the report comment
  - Logs indicate permission errors on branch creation or PR open
- Likely causes
  - App not installed on the repo
  - Missing repository permissions (Contents, Pull requests)
  - Repo default branch is empty
- Immediate handling
  1. Confirm the app is installed on each target repo.
  2. Verify repo permissions in the GitHub App settings.
  3. Ensure the default branch has at least one commit.

### Dependency failure
- Symptoms
- Workarounds
- Long-term fixes
