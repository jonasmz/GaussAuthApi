# Feature Specification: Administrative Operations

**Feature Branch**: `011-admin-operations`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature `011-admin-operations` for the reusable generic Authentication and Authorization API: secure, permission-based administrative use cases over users, applications, memberships, roles, permissions, role assignments, sessions, consumer secrets, and audit access, with no admin UI and no redesign of earlier domain concepts."

## Context

Features 002–004 introduced management operations for trusted callers, 006 introduced sessions, 008 introduced per-application consumer secrets, and 009 introduced a protected audit query with an application scope and a separate global scope. This feature turns those management capabilities into one coherent, permission-protected administrative contract. It reuses the existing domain concepts and use cases; it adds enforcement, scoping, inspection, session administration, consumer-secret administration, and actor/target auditing. It adds no new domain concepts beyond the administrative permissions and the persistence of the consumer credential concept already introduced in 008 and 009, which is needed so secrets can be rotated at runtime.

## Clarifications

### Session 2026-10-01

- Defaults below were chosen from existing precedent (notably the global audit capability in 009) and least privilege. They are recorded under Assumptions and are expected to be confirmed or changed in `/speckit-clarify`.
- Q: How is a global Auth administrator identified? → A: By a small, externally configured list of stable UserIds, using one configuration mechanism shared with the 009 global audit capability (the 009 single-user setting is generalized to a list rather than duplicated). No special "Auth" Application and no persisted global roles or permissions are introduced. Listed users hold the global operations and may also perform application-scoped administrative operations on any explicitly named Application (administrative authority only); membership in the list grants no application permission, never appears in effective permissions or the authorization context, and grants no business permission. A user absent from the list has no global authority. Global operations are audited with actor and target.
- Q: How do the administrative permissions come to exist inside each Application? → A: The system creates a fixed, platform-defined set of administrative permissions, active, automatically on every new Application registration, and adds any missing ones to existing Applications through an idempotent bootstrap that never creates duplicates. Codes are defined centrally and stably by Auth, respect per-Application uniqueness, and coexist with business permissions. The bootstrap creates no roles and assigns nothing to any user or role; administrators assign these permissions to roles through the normal assignment operations.
- Q: Can an Application administrator manage their Application's consumer secret, or only a global administrator? → A: Only a global administrator can generate, rotate, retire, and inspect metadata of consumer secrets, acting only on the explicitly named target Application. No consumer-secret permission is seeded per Application, and Application administrators cannot perform or recover any secret operation. Future delegation is out of scope and can be added without changing consumer authentication.
- Q: Should administrative permission codes carry a reserved prefix? → A: Yes. All administrative codes use the reserved `auth.` prefix (catalog in FR-005); business Applications cannot create permissions with that prefix; administrative authorization evaluates only `auth.*` permissions and never treats an unprefixed business code as equivalent. Only application-scoped codes are seeded; users, applications, and consumer-secrets codes are reserved but global-only (see the earlier answers). The application audit permission becomes `auth.security.audit.read`, replacing the 009 permission through a migration that preserves assignments.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Permission-Protected, Scoped Administration (Priority: P1)

An operator authenticates with a normal session and can perform only the administrative operations their effective permissions allow, either within one Application or, for explicitly global operations, across the Auth platform. Every administrative request is rejected unless the session is valid and the required permission is held in the correct scope.

**Why this priority**: Every other story depends on this boundary. Without it, administrative operations are open to any trusted caller and cannot be safely exposed.

**Independent Test**: Use an operator holding a membership-management permission in Application A. Verify the operator can manage Application A memberships, cannot manage Application B memberships, cannot perform a global operation, and that a same-named role in Application B grants nothing in Application A.

**Acceptance Scenarios**:

1. **Given** an operator with a valid session and the required permission in Application A, **When** they perform that operation on Application A, **Then** it succeeds.
2. **Given** the same operator, **When** they target Application B, **Then** the request is rejected without revealing whether the target exists.
3. **Given** an operator who holds only application-scoped permissions, **When** they attempt a global operation, **Then** it is rejected.
4. **Given** an unauthenticated, expired, or revoked session, **When** any administrative operation is attempted, **Then** it is rejected as unauthenticated.
5. **Given** a role named "Administrator" in any Application, **When** its holder attempts an operation they lack an explicit permission for, **Then** it is rejected; role names are never trusted.
6. **Given** a designated global administrator, **When** they perform a global operation or an application-scoped administrative operation on a named Application, **Then** it succeeds, is audited with them as actor, and their effective permissions and authorization context for that Application remain unchanged (no application or business permission is granted).

---

### User Story 2 - Manage Users and Applications (Priority: P1)

A global administrator can find and inspect users and applications and change their lifecycle state, using the existing lifecycle behavior and consequences.

**Why this priority**: User and Application lifecycle control is the primary operational need and gates access for everything else.

**Independent Test**: Deactivate a user who has an active session and memberships; verify the existing consequences (no login, sessions unusable, no authorization) apply exactly as before and an audit event records actor and target. Repeat deactivation and confirm it is idempotent.

**Acceptance Scenarios**:

1. **Given** a global administrator, **When** they list users with a filter and page size, **Then** results are deterministically ordered, bounded, and contain only safe account-state fields.
2. **Given** an active user, **When** an administrator deactivates them, **Then** the existing deactivation consequences apply and no second invalidation path exists.
3. **Given** an inactive user or application, **When** an administrator activates it, **Then** it becomes active without implicitly reactivating memberships, roles, or sessions.
4. **Given** a new application code, **When** an administrator registers an application, **Then** the existing registration rules apply, including code uniqueness and stability.
5. **Given** an administrative user response, **When** inspected, **Then** it contains no password hash, security stamp, reset material, or secret and no unnecessary personal data.
6. **Given** a request to physically delete a user or application, **When** attempted, **Then** no such operation exists.

---

### User Story 3 - Manage Memberships, Roles, Permissions, and Assignments (Priority: P1)

An application administrator manages one Application's memberships, roles, permissions, user-role assignments, and role-permission assignments, and can inspect a user's effective authorization in that Application.

**Why this priority**: This is the day-to-day work of keeping each consuming application's access model correct and is the core of application-scoped administration.

**Independent Test**: In Application A create a role and permission, assign the permission to the role, assign the role to a user with an active membership, then inspect the user's effective authorization and confirm it matches what the authorization contract (008) returns. Attempt each assignment across applications and confirm rejection.

**Acceptance Scenarios**:

1. **Given** an active Application and an active user, **When** an administrator creates a membership, **Then** it is created subject to all existing membership rules.
2. **Given** a role and permission from different Applications, **When** an assignment is attempted, **Then** it is rejected and nothing changes.
3. **Given** a user without an active membership in the Application, **When** a role assignment is attempted, **Then** it is rejected.
4. **Given** an already-assigned role or already-active state, **When** the same request is repeated, **Then** no duplicate record is created and the result is stable.
5. **Given** an administrator inspecting a user's effective authorization in an Application, **When** the request is made, **Then** the response shows membership state, relevant user and application state, assigned roles, and effective permissions using the same semantics as 004 and 008, and nothing else.
6. **Given** two concurrent identical assignment or membership-creation requests, **When** both execute, **Then** exactly one record results and the outcome is consistent for both callers.
7. **Given** a role or permission is deactivated, **When** effective authorization is inspected, **Then** inactive items no longer contribute, as before.

---

### User Story 4 - Inspect and Revoke Sessions (Priority: P2)

An administrator can see safe metadata about sessions and revoke them, singly or within a bounded, explicit scope, to respond to compromise or access changes.

**Why this priority**: Incident response needs fast, controlled revocation, but depends on the authorization boundary and existing session behavior.

**Independent Test**: List a user's sessions within an Application, revoke one, and confirm it is immediately unusable; revoke all of the user's sessions in that Application and confirm sessions in other Applications are untouched.

**Acceptance Scenarios**:

1. **Given** an administrator with session-read permission for an Application, **When** they list sessions, **Then** they see only that Application's sessions with safe metadata (identifiers, user, application, created, expires, revocation state) and never any credential or secret.
2. **Given** a permitted session, **When** it is revoked, **Then** it stops granting access and an audit event records actor, target session, user, and application.
3. **Given** an already revoked or expired session, **When** revocation is requested again, **Then** the result is stable and no error condition is created.
4. **Given** an application administrator, **When** they revoke all sessions of one user in their Application, or all sessions of their Application, **Then** only that Application's sessions are affected.
5. **Given** a global administrator, **When** they revoke all sessions of one user across Applications, **Then** all of that user's sessions are revoked.
6. **Given** an application administrator, **When** they try to revoke a session belonging to another Application, **Then** it is rejected.

---

### User Story 5 - Administer Consumer Secrets (Priority: P2)

A global administrator can issue and rotate an Application's consumer secret and inspect non-sensitive metadata, without ever being able to read a secret afterwards.

**Why this priority**: Rotation is required for safe operation of the authorization contract, but it affects few operators and builds on existing 008/009 behavior.

**Independent Test**: Rotate the secret of Application A; verify the new value is shown once, the previous value keeps working during its retirement window, metadata never includes secret values, and Application B's secret is unaffected.

**Acceptance Scenarios**:

1. **Given** an Application with no secret, **When** an administrator generates one, **Then** the plaintext value is returned once in that response only.
2. **Given** an Application with an active secret, **When** it is rotated, **Then** the new secret becomes active, the previous one becomes the retiring secret, and the plaintext new value is returned once.
3. **Given** a retiring secret, **When** an administrator explicitly retires it, **Then** only the retiring secret stops working; the active secret keeps working.
4. **Given** any subsequent read of secret information, **When** inspected, **Then** only non-sensitive metadata (existence, timestamps, whether a retiring secret exists) is visible; no value, prefix, hash, or hint is exposed.
5. **Given** concurrent rotation requests for one Application, **When** they execute, **Then** the result is a single consistent active/retiring state with no loss of the active secret.
6. **Given** an application-scoped administrator, **When** they attempt any consumer-secret operation, **Then** it is rejected under the default policy.

---

### User Story 6 - Audited Administration and Audit Access (Priority: P2)

Every administrative state change is recorded with the acting administrator and the target, and authorized reviewers can query that history with the scope their permission allows.

**Why this priority**: Accountability makes administrative power acceptable, and the audit query already exists from 009.

**Independent Test**: Perform one administrative change of each category and query audit history. Confirm each event carries actor, target, application where applicable, result, time, and correlation id, contains no secrets, and is visible only within the reviewer's scope.

**Acceptance Scenarios**:

1. **Given** any administrative state change, **When** it completes, **Then** an event records actor, target, application where applicable, operation, result, time, and correlation/trace id when available.
2. **Given** a rejected administrative attempt due to insufficient permission, **When** it occurs, **Then** a safe denial is recorded without revealing the target's existence to the caller.
3. **Given** a consumer-secret operation, **When** its event is inspected, **Then** it contains no secret material.
4. **Given** an application audit reviewer, **When** they query audit events, **Then** only their Application's events are visible; only the global capability sees multiple applications and global events.
5. **Given** a critical administrative change whose audit event cannot be persisted, **When** the operation runs, **Then** it is not completed, per the existing reliability policy.

---

### Edge Cases

- A user is both a target and an actor (an administrator deactivating themselves): the actor and target are recorded independently; the operation follows existing lifecycle rules and does not bypass them.
- The last administrator of an Application removes their own administrative role or membership: the Application remains manageable by the global administrator; no lockout exemption is created.
- A role or permission is deactivated while assigned: assignments are retained historically but stop contributing, as in 004.
- A request supplies a non-existent identifier: the response is indistinguishable from a cross-application rejection for callers without permission over that scope.
- A list request omits or exceeds the page size: a safe default is applied or the request is rejected; the maximum is never exceeded.
- A secret rotation occurs while a consumer is validating with the previous secret: validation continues to succeed during the retirement window.
- A session is revoked while the user is mid-request: the session is unusable for subsequent requests, consistent with 006.
- Repeated state-transition requests (activate an active membership, deactivate an inactive role, revoke a revoked session): no duplicate records and a stable outcome.
- An Application already holds a permission whose code begins with the reserved `auth.` prefix (created before this feature): it must not silently be treated as platform-defined authority; the bootstrap reports it safely, never overwrites it, and its handling is defined in planning.
- A configured global-administrator UserId refers to an inactive or non-existent user: that user cannot obtain a session, so the entry confers no usable authority.
- Concurrent creation of the same membership: exactly one membership results.
- An Application administrator removes their own administrative role: no lockout exemption exists and a global administrator can restore access.
- A request supplies another user's identifier to a self-service operation: the identifier is ignored and the caller's own session identity is used.
- A request body or string exceeds its bound: it is rejected as invalid before any state change.

## Requirements *(mandatory)*

### Functional Requirements

**Authorization model**

- **FR-001**: Every administrative operation MUST require an authenticated, unexpired, unrevoked session and an explicit required permission; no operation may be authorized by endpoint location, role name, or caller trust alone.
- **FR-002**: Administrative authorization MUST NOT depend on hard-coded role names. Authority MUST derive only from effective permissions in the target Application, or from the explicit global administrative capability for global operations.
- **FR-003**: Administrative permissions MUST be application-scoped by default. Permission held in one Application MUST NOT confer authority over another Application, and identically named roles or permissions in different Applications MUST confer nothing across them.
- **FR-004**: Global operations MUST be explicitly enumerated, require the global administrative capability, and MUST be independent from normal application roles, permissions, memberships, and role names. Authorization scope MUST be evaluated before any data is accessed or changed.
- **FR-004a**: The global administrative capability MUST be held only by users whose stable UserId appears in an externally configured list, which may contain more than one user, may differ per environment, and MUST NOT be stored in source. It MUST use the same single configuration mechanism as the 009 global audit capability, so there is one source of global authority and not two. An empty or absent list MUST mean no global authority. No special Application and no persisted global role or permission model is introduced, and list membership MUST NOT grant any application permission as seen by effective-permission resolution or the authorization contract.
- **FR-004b**: A global administrator MAY perform application-scoped administrative operations on any explicitly named Application, since otherwise an Application could never receive its first administrator. This is administrative authority only: it MUST NOT appear in a user's effective permissions or in the authorization context returned to consuming APIs, MUST NOT grant any business permission, and every such operation MUST be audited with the global administrator as actor.
- **FR-005**: The system MUST define one stable, documented catalog of administrative permission codes, all carrying the reserved `auth.` prefix: `auth.users.read`, `auth.users.manage`, `auth.applications.read`, `auth.applications.manage`, `auth.memberships.read`, `auth.memberships.manage`, `auth.roles.read`, `auth.roles.manage`, `auth.permissions.read`, `auth.permissions.manage`, `auth.sessions.read`, `auth.sessions.revoke`, `auth.security.audit.read`, and `auth.consumer-secrets.rotate`. Only the application-scoped codes (memberships, roles, permissions, sessions, and security audit read) are seeded per Application and assignable to Application roles. The users, applications, and consumer-secrets codes are reserved and documented but are NOT seeded and grant nothing on their own: those operations are global and governed solely by the global administrative capability (FR-004a). Final code spellings may be refined in planning, but the `auth.` prefix and the catalog's single central definition are fixed.
- **FR-005a**: Every Application MUST automatically receive the standard set of application-scoped administrative permissions, created active, when it is registered. Applications that already exist MUST receive any missing ones through a repeatable bootstrap that produces no duplicates however many times it runs. The codes MUST be defined in one central, stable place owned by Auth and MUST respect per-Application uniqueness. The bootstrap MUST NOT create roles and MUST NOT assign permissions to roles or users. A pre-existing permission in an Application that already uses the reserved `auth.` prefix MUST NOT be silently treated as platform-defined authority; its handling is defined in planning and tested.
- **FR-005b**: The `auth.` prefix MUST be reserved exclusively for Auth administrative capabilities. Creating or renaming a permission with that prefix through the normal permission-management operations MUST be rejected, business permissions keep their own namespaces (for example `reservations.create`), and administrative authorization MUST evaluate only `auth.*` permissions defined for that purpose, with no implicit equivalence between a business code such as `roles.manage` and `auth.roles.manage`. This rule MUST be documented as a stable part of the authorization contract.
- **FR-006**: The existing management operations from 002, 003, and 004 MUST be placed under this authorization model rather than duplicated; administrative routes MUST invoke the existing use cases or narrowly scoped administrative use cases and MUST NOT re-implement business rules.
- **FR-007**: Self-service operations (a user acting on their own account, session, or profile) MUST remain distinct from administrative operations; an administrative permission MUST be required whenever an externally supplied identifier targets another identity, and self-service protections MUST NOT be weakened.

**Users and Applications (global)**

- **FR-008**: A global administrator MUST be able to retrieve a user, list users with bounded pagination and filters, and activate or deactivate a user, using the existing lifecycle behavior and consequences for login, sessions, memberships, and authorization without a second invalidation path.
- **FR-009**: Administrative user provisioning MUST reuse the existing provisioning use case from 002; no second incompatible provisioning path is created.
- **FR-010**: A global administrator MUST be able to register, retrieve, list, activate, and deactivate Applications through the existing rules. An ApplicationCode MUST remain a stable identifier; this feature MUST NOT add code mutation.
- **FR-011**: No physical deletion of users, applications, roles, or permissions MUST be introduced; deactivation and history preservation apply.
- **FR-012**: Administrative user and application responses MUST contain only high-level safe state and MUST NOT contain password hashes, security stamps, reset material, secrets, or unnecessary personal data.

**Application-scoped administration**

- **FR-013**: An administrator with the relevant permission MUST be able, within one Application, to list memberships for that Application, list a user's membership in that Application, and create, activate, and deactivate memberships, subject to all existing active-user, active-application, and isolation rules.
- **FR-014**: A global administrator MUST additionally be able to list all memberships of a given user across Applications.
- **FR-015**: Within one Application, an administrator MUST be able to create, retrieve, list, activate, deactivate, and update allowed mutable fields of roles and permissions, preserving application-scoped uniqueness and inactive-state semantics.
- **FR-016**: Within one Application, an administrator MUST be able to assign and remove roles for users with an active membership and assign and remove permissions for roles, with cross-application assignment always rejected.
- **FR-017**: An administrator MUST be able to inspect a user's effective authorization in one Application: membership state, relevant user and Application state, assigned roles, and effective permissions, using exactly the semantics of 004 and 008 and exposing no persistence internals.

**Sessions**

- **FR-018**: An administrator MUST be able to list sessions for a user or an Application with bounded pagination and filters for state, exposing only safe metadata and never access credentials, refresh material, session secrets, or cryptographic material.
- **FR-019**: An administrator MUST be able to revoke a single session, all sessions of one user within one Application, and all sessions of one Application, within their own Application scope. A global administrator MUST additionally be able to revoke all sessions of one user across Applications. No other bulk revocation scope and no arbitrary identifier-list revocation is provided.
- **FR-020**: Session revocation MUST use the existing revocation behavior so revoked sessions are immediately unusable, and MUST be idempotent.

**Consumer secrets (global)**

- **FR-021a**: Consumer-secret operations MUST be global-only and MUST require an explicitly named target Application; no Application-level consumer-secret permission exists, and Application administrators MUST be rejected for every secret operation.
- **FR-021**: A global administrator MUST be able to generate an initial consumer secret for an Application, rotate it, explicitly retire the retiring secret, and inspect non-sensitive metadata, preserving the independent per-Application secret and the active-plus-retiring policy from 008 and 009.
- **FR-022**: A plaintext consumer secret MUST be returned only in the response that creates or rotates it; it MUST never be retrievable afterwards and MUST never appear in logs, audit events, error responses, or metadata. Rotation MUST NOT invalidate both the active and retiring secret unless that is explicitly requested, and an operation on one Application MUST NOT affect another. A consumer-secret change and its security event MUST persist atomically, and the plaintext MUST be returned only after that commit succeeds, so a failed audit write never leaves a changed secret that the administrator never received.

**Audit**

- **FR-023**: Every administrative state change (user and application lifecycle, membership changes, role and permission changes, assignments and removals, administrative session revocation, consumer-secret operations) MUST produce a SecurityEvent following the existing reliability policy, recording actor user, target identifier (the affected user, and for other targets a typed subject such as role, permission, membership, assignment, session, or consumer credential with its id), Application where applicable, operation, result, time, and correlation/trace id when available. The actor MUST come from the authenticated session, never be inferred from target state, and no secret material may be recorded.
- **FR-024**: Insufficient-permission and cross-application attempts MUST be safely recorded, with the caller receiving no information beyond rejection.
- **FR-025**: Audit access MUST reuse the 009 query capability and its application scope and global scope, with no new unrestricted cross-application audit access. Application-scoped audit access MUST be governed by `auth.security.audit.read`; the existing 009 application audit permission is replaced by it, and planning MUST define a migration that preserves existing assignments so no reviewer silently loses or gains access. The global audit scope remains governed by the global administrative capability (FR-004a).

**Cross-cutting**

- **FR-026**: Every list endpoint MUST use pagination, a documented default and explicit maximum page size, deterministic ordering, and only necessary filters; no generic query language is provided.
- **FR-027**: State transitions and assignments MUST be idempotent where appropriate, create no duplicate records on repeated requests, record a security event only when state actually changes, and remain correct under concurrent requests using persistent uniqueness and concurrency controls, without distributed locking.
- **FR-028**: Every request MUST be bounded: strings, identifiers, page sizes, and request bodies have explicit limits, and invalid state transitions are rejected.
- **FR-029**: Errors MUST use the existing consistent error contract for unauthenticated, insufficient permission, cross-application, not found, invalid state transition, duplicate assignment, and invalid request, and MUST NOT expose SQL, persistence or identity internals, filesystem paths, secrets, or stack traces. Caller-visible results MUST NOT reveal the existence of targets outside the caller's authorized scope.
- **FR-030**: Logging MUST be structured, use stable identifiers, and MUST NOT include passwords, secrets, access credentials, reset credentials, or full sensitive request bodies.
- **FR-031**: Administrative endpoints MUST be clearly distinguishable from self-service and public endpoints. All management routes live under a dedicated administrative route group. The existing 009 audit query keeps its established path for compatibility but is protected by the administrative authorization model. Further route structure belongs to planning.
- **FR-032**: The feature MUST publish an administrative contract document stating each operation, its required permission, whether it is global or application-scoped, pagination and filter behavior, secret-rotation behavior, session-revocation scopes, audit behavior, the self-service versus administration distinction, and the information intentionally never exposed. The document MUST match the implementation.
- **FR-033**: Domain MUST remain independent of web, identity, persistence, and infrastructure concerns. Administrative functionality MUST be organized as feature-oriented use cases, not a single all-purpose administrative service.
- **FR-034**: The feature MUST NOT introduce an administrative frontend, impersonation, delegated administration hierarchies, organization or tenant hierarchies, approval workflows, bulk import/export, reporting, SCIM, LDAP, directory integration, OAuth/OIDC administration, MFA administration, mTLS, external secret management, or compliance deletion workflows, and MUST NOT include placeholders for them.

### Key Entities *(include if feature involves data)*

- **Administrative Permission**: A stable, namespaced capability code (for example, membership management or session revocation) assigned through existing roles, scoped to the Application where it is held.
- **Global Administrative Capability**: An explicit authority independent of any Application's roles, permissions, or memberships, required for operations affecting the Auth platform as a whole and also sufficient for application-scoped administration of any named Application; it never produces application or business permissions.
- **Administrative Actor**: The authenticated user performing an operation, identified from the session.
- **Administrative Target**: The user, application, membership, role, permission, assignment, session, or consumer secret affected by an operation.
- **Effective Authorization View**: A read-only summary of a user's membership, state, roles, and effective permissions in one Application.
- **Session Summary**: Safe session metadata (identifier, user, application, created, expires, revocation state) with no credential material.
- **Consumer Secret Metadata**: Non-sensitive facts about an Application's secret (existence, timestamps, whether a retiring secret exists).
- **SecurityEvent (extended use)**: The existing audit record, used with distinct actor and target fields for administrative actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of administrative operations are rejected when the session is missing, expired, or revoked, or when the required permission is not held in the correct scope.
- **SC-002**: In cross-application tests, 0 administrative operations succeed or disclose data across Applications, including with identically named roles and permissions.
- **SC-003**: 100% of administrative state changes produce an audit event with the correct actor and target, and 0 audit events, logs, or responses sampled contain passwords, hashes, security stamps, credentials, or consumer secrets.
- **SC-004**: An administrator can deactivate a user and have all of that user's access fail on the next attempt, with 0 divergence from the lifecycle behavior established in earlier features.
- **SC-005**: An administrator can revoke a single session, a user's sessions in an Application, or an Application's sessions, and 100% of affected sessions are unusable on the next request while 0 sessions outside the chosen scope are affected.
- **SC-006**: After issuance or rotation, 0 later administrative responses reveal a consumer secret, and a rotation never leaves an Application without a working secret.
- **SC-007**: 100% of list operations enforce a maximum page size and deterministic ordering, and no tested request returns more than the maximum.
- **SC-008**: 100% of repeated state-transition requests produce no duplicate records, and concurrent identical requests produce exactly one record.
- **SC-009**: An operator can find, in one document, the operation, required permission, scope, and exposure rules for every administrative operation, and the document matches the implemented behavior.
- **SC-010**: The next feature (`012-hardening-release`) can proceed without restructuring the administrative boundary.

## Assumptions

- **Global administrator representation (clarified)**: A small externally configured list of UserIds shared with the 009 global audit capability; see FR-004a. Configured identifiers are assumed to refer to existing users, and a listed UserId that does not correspond to an active user confers no usable authority.
- **Global administrators sign in normally**: a global administrator obtains a session through any Application where they hold an active membership; global authority does not depend on which Application that is, and no special sign-in path is introduced.
- **Global operations**: user lifecycle and provisioning, user listing, listing a user's memberships across Applications, application registration, listing, activation and deactivation, all consumer-secret operations, user-wide session revocation across Applications, and global audit access.
- **Application-scoped operations** (also available to global administrators on any named Application): memberships, roles, permissions, assignments, effective-authorization inspection (which includes the Application's state), session listing and revocation within the Application, and application audit access. Retrieving or listing Applications themselves is global-only.
- **Users and applications are managed globally**: application-scoped administrators see users only through membership and session views in their own Application.
- **Consumer-secret administration is global only**, because the secret authenticates a service to Auth platform-wide; delegation to application administrators is not part of this feature unless clarification changes it.
- **Bulk session revocation** is limited to the scopes in FR-019 because they map to concrete operational cases; whole-Application revocation is permitted to that Application's administrator for incident response.
- **User search by email** is permitted only for global administrators; application-scoped views use stable identifiers.
- **Permission codes** are the `auth.`-prefixed catalog in FR-005, assigned through ordinary roles in the Application and never equivalent to unprefixed business codes.
- **Existing behavior is authoritative**: lifecycle consequences, uniqueness, membership rules, effective-permission semantics, session revocation, consumer-secret policy, audit reliability, rate limiting, and error contracts are reused as established in 002–010.
- **Deactivation, not deletion**: privacy or compliance deletion is a separate, future concern.
- **Development environment** is unchanged: the existing .NET and PostgreSQL 17 containers are reused, with no new infrastructure.
