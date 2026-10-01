# Data Model: Administrative Operations

No existing domain concept is redesigned. Changes are additive.

## SecurityEvent (extended)

| Field | Type | Notes |
|---|---|---|
| `ActorUserId` | `Guid?` (new) | Authenticated administrator who performed the operation. Null for self-service and system events. Never inferred from the target. |
| `UserId` | existing | The affected (target) user where one exists. |
| `ApplicationId` | existing | Application in scope where applicable. |
| `SubjectType` / `SubjectId` | existing | Typed target id: `application`, `membership`, `role`, `permission`, `role-permission` (assignment id), `user-role` (assignment id), `session`, `consumer-credential` (Application id). Identifiers only. |

- Index: `(ActorUserId, OccurredAtUtc, Id)`.
- Invariants unchanged: append-only, UTC, bounded, prohibited-term guard on `Reason`/`Metadata`/`SubjectType`.
- Audit query responses include `actorUserId`.

New event types (stable names, reliability):

| Type | Name | Reliability |
|---|---|---|
| `ApplicationRegistered` | `application.registered` | Critical |
| `UserCreated` | `user.created` | Critical |
| `RoleUpdated` | `role.updated` | Critical |
| `PermissionUpdated` | `permission.updated` | Critical |
| `ConsumerCredentialGenerated` | `consumer.credential.generated` | Critical |
| `ConsumerCredentialRotated` | `consumer.credential.rotated` | Critical |
| `ConsumerCredentialPreviousRetired` | `consumer.credential.previous-retired` | Critical |
| `AdministrativeAccessDenied` | `administration.access.denied` | Operational (outcome `rejected`) |

`SessionRevoked` is reused for administrative revocation (now with an actor).

## ConsumerCredential (new Domain entity)

Persisted in `ApplicationConsumerCredentials`, one row per Application.

| Field | Type | Rule |
|---|---|---|
| `ApplicationId` | `Guid` | Primary key; one credential per Application. |
| `CurrentHash` | `string` (≤ 512) | One-way verifiable representation; never the secret. |
| `RetiringHash` | `string?` (≤ 512) | Present only after rotation; must differ from `CurrentHash`. |
| `CreatedAtUtc` | `DateTimeOffset` | Set at first generation. |
| `RotatedAtUtc` | `DateTimeOffset?` | Updated on rotation. |
| `RetiredAtUtc` | `DateTimeOffset?` | When the previous secret was last retired explicitly. |
| concurrency token | `xmin` | Optimistic concurrency for concurrent rotations. |

Transitions:

- `Create(hash)` → current only.
- `Rotate(newHash)` → `RetiringHash = CurrentHash`, `CurrentHash = newHash` (older retiring is discarded).
- `RetirePrevious()` → `RetiringHash = null`; no-op if none.
- Import from configuration on first rotate: configured current hash becomes the retiring hash of the new record.

The plaintext secret never reaches the Domain, persistence, logs, events, or any response other than the creating call.

## Permission (extended use, no schema change)

- Platform permissions: codes in `AdministrativePermissionCatalog`, prefix `auth.`, seeded per Application, active, not editable or deactivatable through normal operations.
- Business permissions: any other code; the `auth.` prefix is rejected on create.

Seeded per Application (9): `auth.memberships.read`, `auth.memberships.manage`, `auth.roles.read`, `auth.roles.manage`, `auth.permissions.read`, `auth.permissions.manage`, `auth.sessions.read`, `auth.sessions.revoke`, `auth.security.audit.read`.

Reserved, not seeded (global only): `auth.users.read`, `auth.users.manage`, `auth.applications.read`, `auth.applications.manage`, `auth.consumer-secrets.rotate`.

## Migrations

1. `addSecurityEventActor`: `SecurityEvents.ActorUserId` + index `IX_SecurityEvents_ActorUserId_OccurredAtUtc_Id`.
2. `seedAdministrativePermissions`: idempotent seeding for existing Applications (aborts if a pre-existing `auth.`-prefixed permission exists).
3. `addApplicationConsumerCredentials`: table `ApplicationConsumerCredentials`.
4. `moveAuditPermissionToAuthNamespace`: transfers `audit.events.read` assignments to `auth.security.audit.read`.

## Configuration

| Key | Purpose | Default |
|---|---|---|
| `Administration:GlobalAdministratorUserIds` | Global administrators (list of UserIds, replaces `SecurityAudit:GlobalReviewerUserId`) | empty = no global authority |
| `Administration:MaxBulkSessionRevocation` | Sessions revoked per bulk request | 1000 (valid 1–10000) |
| `RateLimiting:Administration:PermitLimit` / `WindowSeconds` | Administrative rate limit | 120 / 60 |

Startup fails if the legacy `SecurityAudit:GlobalReviewerUserId` key is present or an entry is not a valid non-empty GUID.

## Transient application types (not persisted)

- `AdministrativeAuthorizationResult`: outcome, `ActorUserId`, `IsGlobal`.
- `AdministrativeActorContext`: scoped holder of the current actor for audit stamping.
- `SessionSummary`: id, userId, applicationId, createdAtUtc, expiresAtUtc, revokedAtUtc, state.
- `EffectiveAuthorizationView`: application and user state, membership state, active roles, effective permission codes.
- `ConsumerCredentialMetadata`: exists, source, createdAtUtc, rotatedAtUtc, hasRetiring.
