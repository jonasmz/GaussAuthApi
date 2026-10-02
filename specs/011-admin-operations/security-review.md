# Security Review: Administrative Operations

## Protected routes

All management routes live under `/admin` (rate limit `administration`, default 120 per 60 s). Legacy `/users` and `/applications/...` routes no longer exist (`404`). `/auth/*`, `/me/*`, `/profile-images/*`, `/health/*` are unchanged; `GET /security-events` stays outside `/admin` but uses the same authorization model.

| Area | Authority |
|---|---|
| Users, Applications, user memberships, user sessions, consumer secrets | Global administrator |
| Memberships, roles, permissions, assignments, authorization view, sessions of one Application | `auth.*` permission in that Application, or global administrator |
| Security audit | `auth.security.audit.read` (own Application only) or global administrator (all) |

Permissions seeded per Application: `auth.memberships.read|manage`, `auth.roles.read|manage`, `auth.permissions.read|manage`, `auth.sessions.read|revoke`, `auth.security.audit.read`.

## Reserved prefix

`auth.` is reserved. Creating such a permission is rejected (`400`); editing, activating, or deactivating a platform permission is rejected (`409`). The seeding migration fails closed if an `auth.`-prefixed permission already exists. A business permission named like a platform one (for example `roles.manage`) grants nothing.

## Global administrator model

- Configured by `Administration:GlobalAdministratorUserIds`; an empty list gives no global authority. The legacy `SecurityAudit:GlobalReviewerUserId` and invalid entries stop startup.
- Global authority is administrative only: it never appears in effective permissions or the 008 authorization context.
- A global administrator needs an active membership in an active Application to sign in, and loses access if deactivated or if the session is revoked or expired.
- Authorization happens before any data is read; a caller outside the target Application gets the same `403` whether or not it exists. Authenticated denials are audited (`administration.access.denied`); unauthenticated ones are not.

## Secrets

Consumer secrets are 32 random bytes (base64url), stored only as hashes, shown once with `Cache-Control: no-store`, and never present in events, logs, metadata, or later responses. Generate, rotate, and retire write the change and the critical event in one transaction.

## Accepted limitations

- Bulk session revocation is capped by `Administration:MaxBulkSessionRevocation` (default 1000); callers repeat while `hasMore`.
- Non-secret administrative mutations keep the 009 event-reliability policy (change, then event) rather than one transaction.
- Consumer secrets configured through `AuthorizationConsumers:*` cannot be retired at runtime; rotate first.

## Deferred decisions

No administrative frontend, impersonation, approval workflow, or enterprise IAM features; no external secret manager; no per-user (non-Application) administrative roles beyond the global list.
