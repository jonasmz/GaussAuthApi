# Administrative API Contract

All administrative routes are under `/admin` and require `Authorization: Bearer <access credential>` from a normal session. Every request is authorized before any data is read or changed. This document was verified against the implementation (integration tests in `tests/GaussAuth.Foundation.Tests/administrationTests.test.cs`).

## Authorization model

- **Global (G)**: caller's UserId is in `Administration:GlobalAdministratorUserIds`. Required for operations marked **G**.
- **Application-scoped (A)**: the route `applicationId` must equal the caller's session Application and the caller must hold the listed `auth.*` permission through an active role there. Operations marked **A** may also be performed by a global administrator on any named Application.
- Global authority is administrative only: it never appears in effective permissions or in the authorization context for consuming APIs and grants no business permission. Application-scoped operations performed by a global administrator are audited with that administrator as actor.
- The `auth.` prefix is reserved. Business permissions cannot use it, and platform permissions cannot be edited, activated, or deactivated through normal operations. `auth.roles.manage` is not equivalent to a business permission named `roles.manage`.
- A global administrator signs in through any Application where they hold an active membership.

## Error contract

`ProblemDetails` with a safe title. No SQL, persistence, identity, path, or secret detail.

| Status | Meaning |
|---|---|
| 400 | Invalid request (bounds, format, reserved prefix, bad cursor) |
| 401 | Missing, malformed, expired, or revoked credential, or ineligible user/Application/membership |
| 403 | Authenticated but not permitted, or the target Application is outside the caller's scope (identical whether or not it exists) |
| 404 | Target not found within an authorized scope |
| 409 | Duplicate or invalid state, or concurrent change |
| 429 | Administrative rate limit |

## Pagination and filters

All lists: `limit` default 50, maximum 100 (more is `400`); opaque `cursor`; deterministic order; only the filters listed. Responses: `{ "items": [...], "nextCursor": "..." | null }`.

## Idempotency

- Activate/deactivate on an unchanged state returns `200` with current state and records no event.
- Revoking an already revoked or expired session returns `200`.
- Creating an existing record, or assigning an already active assignment, returns `409`; unique indexes remain authoritative under concurrency.

## Users (G)

| Operation | Route | Notes |
|---|---|---|
| Create | `POST /admin/users` | Existing provisioning use case |
| List | `GET /admin/users?isActive=&email=&cursor=&limit=` | `email` is an exact, normalized match; order by id |
| Get | `GET /admin/users/{userId}` | Existing safe user response |
| Update profile | `PUT /admin/users/{userId}/profile` | Existing use case |
| Activate / Deactivate | `POST /admin/users/{userId}/activate` · `/deactivate` | Existing lifecycle consequences |
| Memberships of a user | `GET /admin/users/{userId}/memberships` | All Applications |
| Sessions of a user | `GET /admin/users/{userId}/sessions?state=&cursor=&limit=` | All Applications |
| Revoke all sessions of a user | `POST /admin/users/{userId}/sessions/revoke` | Bounded; see Sessions |

List item: `{ id, email, isActive, createdAtUtc }`. Never returned: password hash, security stamp, reset material, secrets.

## Applications (G)

| Operation | Route |
|---|---|
| Register | `POST /admin/applications` (seeds platform permissions atomically) |
| List | `GET /admin/applications?cursor=&limit=` |
| Get | `GET /admin/applications/{applicationId}` · `GET /admin/applications/by-code/{code}` |
| Activate / Deactivate | `POST /admin/applications/{applicationId}/activate` · `/deactivate` |

`ApplicationCode` is not modifiable.

## Consumer secrets (G, explicit target Application)

| Operation | Route | Result |
|---|---|---|
| Generate | `POST /admin/applications/{applicationId}/consumer-secret` | `201` with plaintext `secret` once; `409` if one already exists (managed or configured) |
| Rotate | `POST /admin/applications/{applicationId}/consumer-secret/rotate` | `200` with new plaintext `secret` once; previous becomes retiring |
| Retire previous | `POST /admin/applications/{applicationId}/consumer-secret/retire-previous` | `200` metadata; current secret keeps working; `409` when the Application has no managed credential (configured hashes cannot be changed at runtime) |
| Metadata | `GET /admin/applications/{applicationId}/consumer-secret` | `{ exists, source, createdAtUtc, rotatedAtUtc, hasRetiring }` |

Plaintext appears only in the generate/rotate response, with `Cache-Control: no-store`. It is never retrievable later and never logged or audited. Application administrators are always rejected here.

## Memberships (A)

| Operation | Route | Permission |
|---|---|---|
| List | `GET /admin/applications/{applicationId}/memberships?cursor=&limit=` | `auth.memberships.read` |
| Get | `GET /admin/applications/{applicationId}/memberships/{userId}` | `auth.memberships.read` |
| Create | `POST /admin/applications/{applicationId}/memberships` body `{ userId }` | `auth.memberships.manage` |
| Activate / Deactivate | `POST /admin/applications/{applicationId}/memberships/{userId}/activate` · `/deactivate` | `auth.memberships.manage` |

## Roles (A)

| Operation | Route | Permission |
|---|---|---|
| Create | `POST /admin/applications/{applicationId}/roles` | `auth.roles.manage` |
| Get / List | `GET .../roles/{roleId}` · `GET .../roles` | `auth.roles.read` |
| Update description | `PUT .../roles/{roleId}/description` | `auth.roles.manage` |
| Activate / Deactivate | `POST .../roles/{roleId}/activate` · `/deactivate` | `auth.roles.manage` |
| Assign / Remove for user | `POST .../users/{userId}/roles/{roleId}` · `.../remove` | `auth.roles.manage` |
| List user's roles | `GET .../users/{userId}/roles` | `auth.roles.read` |
| Effective permissions | `GET .../users/{userId}/effective-permissions` | `auth.roles.read` |
| Effective authorization view | `GET .../users/{userId}/authorization` | `auth.roles.read` |

The authorization view returns `{ application: { id, code, isActive }, user: { id, isActive }, membership: { isActive } | null, roles: [{ id, name }], permissions: [code] }` using the same effective-permission semantics as 004/008. `...` is `/admin/applications/{applicationId}`.

## Permissions (A)

| Operation | Route | Permission |
|---|---|---|
| Create | `POST .../permissions` | `auth.permissions.manage` (code must not start with `auth.`) |
| Get / List | `GET .../permissions/{permissionId}` · `GET .../permissions` | `auth.permissions.read` |
| Update description | `PUT .../permissions/{permissionId}/description` | `auth.permissions.manage` (not for platform permissions) |
| Activate / Deactivate | `POST .../permissions/{permissionId}/activate` · `/deactivate` | `auth.permissions.manage` (not for platform permissions) |
| Assign / Remove on role | `POST .../roles/{roleId}/permissions/{permissionId}` · `.../remove` | `auth.permissions.manage` |
| List role's permissions | `GET .../roles/{roleId}/permissions` | `auth.permissions.read` |

Cross-application assignment (role and permission from different Applications) is rejected.

## Sessions

Safe metadata only: `{ id, userId, applicationId, createdAtUtc, expiresAtUtc, revokedAtUtc, state }` where `state` is `active | revoked | expired`. Never returned: access credentials, refresh material, session secrets, cryptographic material.

| Operation | Route | Scope / permission |
|---|---|---|
| List | `GET /admin/applications/{applicationId}/sessions?userId=&state=&cursor=&limit=` | A · `auth.sessions.read`; newest first |
| Get | `GET /admin/applications/{applicationId}/sessions/{sessionId}` | A · `auth.sessions.read` |
| Revoke one | `POST /admin/applications/{applicationId}/sessions/{sessionId}/revoke` | A · `auth.sessions.revoke` |
| Revoke user in Application | `POST /admin/applications/{applicationId}/users/{userId}/sessions/revoke` | A · `auth.sessions.revoke` |
| Revoke all in Application | `POST /admin/applications/{applicationId}/sessions/revoke` | A · `auth.sessions.revoke` |
| Revoke user everywhere | `POST /admin/users/{userId}/sessions/revoke` | G |

Bulk revocation, including the global user-wide route, revokes at most `Administration:MaxBulkSessionRevocation` active sessions per request and returns `{ "revoked": n, "hasMore": bool }`; repeat while `hasMore`. A revoked session is unusable on the next request. No arbitrary identifier-list revocation exists.

## Security audit

`GET /security-events` (path kept from 009 for compatibility; it is outside the `/admin` group but protected by the same authorization model). Application scope requires `auth.security.audit.read` and returns only that Application's events; global scope (all Applications and global events) requires the global administrator. Events include `actorUserId`, target identifiers, `applicationId`, type, outcome, time, and correlation id.

## Audit behavior

Every administrative state change writes a critical security event with `ActorUserId` (from the session), the target (`UserId` for the affected user; `ApplicationId`; and `SubjectType`/`SubjectId` of `membership`, `role`, `permission`, `role-permission`, `user-role`, `session`, or `consumer-credential`), outcome, time, and correlation id when available; events are written only when state actually changes. Consumer-secret generate/rotate persist the change and the event in one transaction and return the plaintext only after commit; other mutations follow the 009 reliability policy. Permission-denied attempts by authenticated callers write an operational `administration.access.denied` event. No event, log, or response contains passwords, hashes, tokens, session secrets, or consumer secrets.

## Self-service versus administration

`/auth/*`, `/me/*`, and public reads remain self-service and take identity from the caller's own session. A caller-supplied `userId` is honored only under `/admin` with the required permission. The legacy unauthenticated management routes (`/users`, `/applications/...`) no longer exist.

## Never exposed

Password hashes, security stamps, reset material, access or refresh credentials, session secrets, signing material, consumer secrets (after the one-time response), consumer secret hashes, and unnecessary personal data.

## As built

- Every consumer-secret response (generate, rotate, retire, metadata) is sent with `Cache-Control: no-store`; a concurrent change returns `409` and loses no data.
- `GET /security-events` now includes `actorUserId` on each event.
- Migrations, in order: `addSecurityEventActor`, `seedAdministrativePermissions`, `addApplicationConsumerCredentials`, `moveAuditPermissionToAuthNamespace`. The legacy `audit.events.read` permission is deactivated (not deleted) by the last one.
- Configuration through the development stack: `GLOBAL_ADMINISTRATOR_USER_ID`, `ADMIN_MAX_BULK_SESSION_REVOCATION`, `ADMIN_RATE_LIMIT_PERMITS`, `ADMIN_RATE_LIMIT_WINDOW_SECONDS` (mapped in `compose.dev.yml`).

## Operational notes

- Configuration: `Administration:GlobalAdministratorUserIds`, `Administration:MaxBulkSessionRevocation`, `RateLimiting:Administration:*`. Startup fails if the legacy `SecurityAudit:GlobalReviewerUserId` is set.
- Rate limit `administration` applies to the whole `/admin` group (default 120 per 60 s).
