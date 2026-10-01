# Research: Administrative Operations

Findings come from inspecting the current code (`src/`) and the 002–010 specs. Each entry is Decision / Rationale / Alternatives.

## R1. Where administrative authorization is enforced

- **Decision**: One Application-layer `AdministrativeAuthorizer`, invoked by a single API endpoint filter (`RequireAdministrator(permission, scope)`). It validates the bearer credential through the existing `SessionService.GetAuthenticatedContextAsync`, then decides the outcome: `Unauthenticated`, `Forbidden`, or `Authorized(actor, isGlobal)`.
- **Rationale**: The existing audit query (`securityEventQueryService.service.cs`) already does "session, global policy, effective permission, scope before data". The filter generalizes that pattern once so no route can forget it, and no service re-implements business rules.
- **Alternatives**: ASP.NET authorization policies/handlers (rejected: would need a custom authentication scheme around the app's own session credential, more framework surface for the same result); per-service checks (rejected: duplicated and easy to miss).

## R2. Scope rules

- **Decision**:
  - Application-scoped operation: allowed when the caller is a global administrator, or when the caller's session Application equals the route `applicationId` **and** the caller's effective permissions in that Application contain the required `auth.*` code.
  - Global operation: allowed only for a global administrator.
- **Decision (now FR-004b in the spec)**: A global administrator may perform application-scoped administrative operations on any explicitly named Application, because the clarified model has the global administrator assign the seeded permissions to roles. This authority is administrative only: it never appears in effective permissions or in the 008 authorization context and grants no business permission.
- **Rationale**: Without it an Application could never get its first administrator. It does not contradict "no implicit permission inside an Application", which concerns authorization for consuming business APIs.
- **Alternatives**: Require the global administrator to also hold a role in each Application (rejected: contradicts the clarified "list only" model).

## R3. Cross-scope responses do not leak existence

- **Decision**: Authorization is evaluated before any lookup. An Application administrator targeting another `applicationId` (existing or not) gets the same `403`. Only after authorization passes do `404` results appear; ids belonging to a different Application than the route resolve to `404` through the existing `...AndApplication` lookups.
- **Rationale**: Satisfies FR-029 and FR-024 with no extra mechanism.

## R4. A global administrator needs a session

- **Decision**: A global administrator logs in like any user, through any Application where they hold an active membership (sessions are bound to one Application). Global authority does not depend on which Application that is. This is documented as an operational prerequisite; no special login path is added.
- **Rationale**: Matches how the 009 global audit reviewer already works. A dedicated "Auth" Application was rejected in clarification.

## R5. Global administrator configuration

- **Decision**: Replace `IGlobalAuditReviewerPolicy` with `IGlobalAdministratorPolicy` and read `Administration:GlobalAdministratorUserIds` (array, environment-variable friendly as `Administration__GlobalAdministratorUserIds__0`). The audit query uses the same policy.
- **Decision**: If the legacy single key `SecurityAudit:GlobalReviewerUserId` is present, startup **fails** with a clear message naming the new key. It is not silently honored.
- **Rationale**: A 009 audit reviewer would otherwise be promoted to full global administrator on upgrade without anyone deciding that. Failing fast is the safe, explicit migration. The policy ignores unparseable or empty entries only by failing validation at startup (no silent skip). A listed UserId for an inactive or missing user simply cannot obtain a session, so it confers no usable authority.
- **Alternatives**: Honor both keys (rejected: privilege escalation on upgrade and two sources of authority).

## R6. Actor in audit events

- **Decision**: Add nullable `ActorUserId` to `SecurityEvent` (and to `SecurityEventDraft`). A scoped `IAdministrativeActorContext` is set by the endpoint filter after authorization; `PersistedSecurityEventRecorder` stamps it on every event recorded during that request. Existing events keep `UserId` as the affected user and `ApplicationId`; `SubjectType`/`SubjectId` carry other targets (role, permission, session, credential).
- **Rationale**: Actor comes from the authenticated session only and is never inferred from the target (spec requirement). Existing services keep their signatures. Self-service and system events leave `ActorUserId` null.
- **Target convention (finding fix)**: Existing role, permission, membership, and assignment events record only the Application, so the affected record is not identifiable. Add an `ISecurityEventRecorder` overload carrying `SubjectType`/`SubjectId` (default interface method, so existing test doubles keep compiling) and make the existing services pass: `application`, `membership` (membership id, target user in `UserId`), `role`, `permission`, `role-permission` (assignment id), `user-role` (assignment id, target user in `UserId`), `session` (session id), `consumer-credential` (Application id). Values are identifiers only and satisfy the existing prohibited-term guard.
- **Alternatives**: Add an actor parameter to every service method (rejected: large churn, easy to omit).

## R7. New and corrected security events

- **Decision**: Add `ApplicationRegistered`, `UserCreated`, `RoleUpdated`, `PermissionUpdated`, `ConsumerCredentialGenerated`, `ConsumerCredentialRotated`, `ConsumerCredentialPreviousRetired` (all critical) and `AdministrativeAccessDenied` (operational). `SessionRevoked` (critical) is reused for administrative revocation.
- **Decision**: Fix the existing bug where `PermissionService.UpdateDescriptionAsync` records `PermissionActivated`; check the role equivalent and record `RoleUpdated`/`PermissionUpdated`.
- **Decision (finding fix)**: Existing services record lifecycle and assignment events unconditionally, even when nothing changed (Application, Membership, Role, Permission, RolePermission, UserRole). They are changed to record an event only when state actually changes, so repeated requests are quiet and idempotent.
- **Reliability**: Follows the existing 009 policy (critical events make the operation incomplete on failure; denial events are operational and logged on failure).
- **Denials**: Recorded only for authenticated callers who lack permission (actor, required permission code in `Reason`, target Application). Unauthenticated attempts are only debug-logged to avoid flooding and because there is no actor.

## R8. Route layout and removal of legacy routes

- **Decision**: Management routes move under a `/admin` route group (`/admin/users`, `/admin/applications/...`). The legacy unauthenticated routes (`/users`, `/applications`, `/applications/{id}/roles`, ...) are **removed**, not aliased. `/auth/*`, `/me/*`, `/profile-images/*`, `/health/*`, and `/security-events` stay where they are. `/security-events` is unchanged in path and already protected; only its permission code and global policy change.
- **Rationale**: Leaving the old routes would keep an open back door once administration exists. Existing endpoint classes are reused by passing the group and changing paths, so handlers are not rewritten.
- **Consequence**: Existing tests that call the old routes must be updated to use `/admin/...` with an administrator bearer credential; a shared test fixture makes this mechanical.

## R9. Permission catalog and seeding

- **Decision**: One central `AdministrativePermissionCatalog` (Application) defines every `auth.*` code. Seeded per Application: `auth.memberships.read|manage`, `auth.roles.read|manage`, `auth.permissions.read|manage`, `auth.sessions.read|revoke`, `auth.security.audit.read`. Reserved but not seeded and unassignable in effect (global only): `auth.users.read|manage`, `auth.applications.read|manage`, `auth.consumer-secrets.rotate`.
- **Seeding**: `ApplicationService.CreateAsync` adds the seeded permissions in the same save as the Application (same scoped `DbContext`, atomic). A migration seeds existing Applications with `INSERT ... ON CONFLICT ("ApplicationId","Code") DO NOTHING`, so reruns create no duplicates.
- **Existing `auth.` permissions**: The migration aborts with a clear error if any pre-existing permission already starts with `auth.` (fail closed; the operator renames it and reruns). New Applications cannot have any.
- **Reserved prefix**: `PermissionService.CreateAsync` rejects codes starting `auth.` (`400`). Activating, deactivating, or editing a platform permission (a code in the catalog) through normal operations is rejected so an administrator cannot remove their own seeded capability by accident.

## R10. Audit permission rename

- **Decision**: `auth.security.audit.read` replaces `audit.events.read`. The migration, per Application, seeds the new permission, then copies every active `RolePermissions` row of `audit.events.read` to the new permission (idempotently), then deactivates the old permission. Old assignments are retained historically.
- **Rationale**: Preserves each reviewer's access exactly; no one gains or loses it. The global audit scope remains controlled by the global administrator list.

## R11. Consumer secrets: persistence and flows

- **Finding**: Consumer secret hashes currently live only in configuration (`AuthorizationConsumers:<code>:CurrentSecretHash` / `RetiringSecretHash`), loaded once at startup into a singleton validator, so they cannot be changed at runtime.
- **Atomicity (finding fix)**: Generate and rotate run inside a database transaction that also writes the critical security event, using the existing but still unimplemented `ISecurityAuditTransaction` port (new Infrastructure adapter over the shared scoped `DbContext`). The plaintext is returned only after commit; any failure rolls back and the old secret keeps working, so an audit failure can never leave a changed secret the administrator never received. Other administrative mutations keep the 009 policy (event persisted right after the state change, same transaction where technically practical); this is an accepted limitation recorded in the security review.
- **Decision**: Add a database table `ApplicationConsumerCredentials` (one row per Application) holding the current hash, an optional retiring hash, timestamps, and a concurrency token. A small Domain entity `ConsumerCredential` owns rotation invariants. An Infrastructure `EfConsumerCredentialStore` implements the Application port (generate secret with `RandomNumberGenerator`, hash with the same `PasswordHasher<string>` scheme keyed by Application code, persist). The validator reads the store and **falls back to configured hashes only when the Application has no database record**, so existing deployments keep working unchanged. The validator becomes scoped (it now touches the database).
- **Flows** (global administrator, explicit target Application):
  - **Generate**: only if the Application has neither a database record nor configured hashes; returns the plaintext once.
  - **Rotate**: new secret becomes current and the previous current becomes retiring (any older retiring is discarded). If only configured hashes exist, the configured current hash is imported as the new retiring hash so existing consumers keep working. Returns the plaintext once.
  - **Retire previous**: clears only the retiring hash; the current secret keeps working.
  - **Metadata**: `{ exists, source: "managed" | "configured", createdAtUtc, rotatedAtUtc, hasRetiring }`. Never a value, prefix, length hint, or hash.
- **Concurrency**: optimistic concurrency token; a lost race returns `409` and the administrator retries. No distributed locking.
- **Secret format**: 32 random bytes, base64url, within the existing 512-character credential bound. No custom cryptography.
- **Audit safety**: `SecurityEvent` rejects the words `secret`, `hash`, and `token` in `Reason`/`Metadata`/`SubjectType`; events use `SubjectType = "consumer-credential"` and carry no value. Responses use `Cache-Control: no-store`.
- **Alternatives**: Keep config-only and make rotation an operator procedure (rejected: cannot satisfy the administrative requirement); external secret manager (out of scope per spec).

## R12. Sessions administration

- **Decision**: Extend `ISessionRepository` with a filtered, bounded list (by user, Application, state) and bounded lookups for revocation. New `AdministrativeSessionService` handles list/inspect and revocation; single revoke delegates to the existing `SessionService.RevokeAsync`, user-wide delegates to `RevokeAllForUserAsync`.
- **Scopes**: single session; all sessions of a user within an Application; all sessions of an Application (Application scope with `auth.sessions.revoke`); all sessions of a user across Applications (global only). No identifier-list revocation. All bulk scopes, including the global user-wide one, use the same capped path; the existing unbounded `RevokeAllForUserAsync` stays only for the password-change flow.
- **Bounds**: bulk operations revoke at most `Administration:MaxBulkSessionRevocation` (default 1000) active sessions per request and report `{ revoked, hasMore }` so the operator repeats. One `SessionRevoked` event is written per revoked session (stamped with actor), matching existing per-session auditing.
- **State filter**: `active | revoked | expired` computed from `RevokedAt`/`ExpiresAt` at query time; list is newest first with a composite `(createdAt, id)` cursor, max page 100.
- **Safe metadata only**: id, userId, applicationId, createdAt, expiresAt, revokedAt, state.

## R13. Users administration

- **Decision**: New `ListUsers` query handler: filters `isActive` and exact normalized `email` (global administrators only), ordered by id with the existing cursor convention, max 100. List items are slim (`id`, `email`, `isActive`, `createdAtUtc`). Detail reuses the existing user response (verified to hold no hash, stamp, or reset data). Create, update-profile, activate, and deactivate reuse existing handlers.
- **Deactivation consequences**: unchanged; existing session checks already reject inactive users. Admin deactivation does not add a second revocation path.

## R14. Effective authorization view

- **Decision**: `GET /admin/applications/{applicationId}/users/{userId}/authorization` composes membership state, user and Application state, active assigned roles, and effective permissions using `IUserRoleRepository.GetEffectivePermissionsAsync`, the same call used by 004 and 008. It requires `auth.roles.read`.

## R15. Permission mapping, idempotency, and errors

- **Decision**: Memberships use `auth.memberships.*`; roles and user-role assignment use `auth.roles.*`; permissions and role-permission assignment use `auth.permissions.*`; sessions use `auth.sessions.*`. Full table in [contracts/admin-api.md](contracts/admin-api.md).
- **Idempotency**: Activate/deactivate on an unchanged state returns `200` with the current state and records no event. Only the user lifecycle handlers behave this way today; the other services are changed to match (see R7). Revoking a revoked or expired session returns `200`. Creating an existing record or assigning an active assignment returns `409` (the spec's "duplicate" error). Unique indexes remain authoritative under concurrency.
- **Errors**: Existing `ProblemDetails` shape; `401` unauthenticated, `403` insufficient permission or wrong Application, `404` not found, `409` invalid state or duplicate, `400` invalid request.
- **Spec adjustment**: Retrieving an Application's own state is global-only (`GET /admin/applications/{id}`); Application administrators see Application state through the effective authorization view. The spec Assumptions line was updated accordingly.

## R16. Rate limiting, logging, bounds

- **Decision**: New `administration` rate-limit policy (default 120 per 60 s, configurable under `RateLimiting:Administration`) on the whole `/admin` group. Existing request body limit and string/page bounds apply; new request bodies define explicit maximum lengths. Logging uses stable identifiers only.

## R17. Testing approach

- **Decision**: Tests replace `IGlobalAdministratorPolicy` in the `WebApplicationFactory` with a controllable test policy so a created user can be made global administrator, and raise `RateLimiting:Administration:PermitLimit` by configuration so setup-heavy tests never receive `429`. A shared `AdministrativeTestHost` fixture creates an administrator client; existing tests switch to `/admin/...` through it.
- **Scope**: Essential cases only, mapped to the spec's priority list: authorization and isolation, user lifecycle and no sensitive fields, memberships/roles/permissions including cross-application rejection, session revoke (single and bulk, unusable afterward), consumer-secret rotation and non-disclosure, audit actor/target and absence of secrets, seeding idempotency, audit-permission migration, and reserved-prefix rejection.

## R18. Documentation

- **Decision**: `contracts/admin-api.md` is the administrative contract (operations, required permission, scope, pagination/filters, secret rotation, session revocation, audit, self-service versus admin, never-exposed data). It is re-verified against the implementation during the final task.
