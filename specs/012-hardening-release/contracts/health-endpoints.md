# Contract: Health Endpoints

Both endpoints are **unauthenticated**, **not rate-limited by a named policy** (orchestrators must not be locked out), return **empty bodies**, and never reveal server names, versions, configuration, dependency identity or causes. Exposure to the Internet is controlled by the deployment (network, reverse proxy, firewall, ingress) and documented in `docs/deployment.md`. Any future detailed diagnostics view is a separate, explicitly protected capability and is not part of this contract.

## `GET /health/live`

Process liveness. Never touches an external system.

| Case | Status | Body |
|------|--------|------|
| Process responding | `204 No Content` | empty |

Unchanged from today (existing test `Liveness_returns_empty_204_without_database_access` remains valid).

## `GET /health/ready`

Whether the service can serve requests safely.

| Case | Status | Body |
|------|--------|------|
| Database reachable, no pending migrations, profile storage usable | `204 No Content` | empty |
| Database unreachable, **or** schema behind the release (pending migrations), **or** profile storage unusable | `503 Service Unavailable` | empty |

Behavior rules:
- The 503 never states which check failed. The cause is written to the server log once per state transition with the request/trace correlation where available.
- Evaluation results are reused for at most a few seconds (≤ 5 s); a dependency outage is therefore visible within 10 s and recovery returns to 204 within 10 s without a restart.
- Each check has a short timeout so a hung dependency yields 503 rather than a hung probe.
- Methods other than `GET`/`HEAD` follow default routing behavior (405).
- Responses include the standard security headers (`X-Content-Type-Options`, `X-Correlation-Id`) added by the pipeline; they include no dependency-specific headers.

## Probe guidance (documented, not enforced)

| Orchestrator concept | Endpoint |
|----------------------|----------|
| liveness / restart | `/health/live` |
| readiness / traffic gating | `/health/ready` |
| startup gating after migration | readiness 204 (also proves migrations were applied) |
