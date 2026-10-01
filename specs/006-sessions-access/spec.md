# Feature Specification: Authenticated Sessions and Access Credentials

**Feature Branch**: `006-sessions-access`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature `006-sessions-access` for the reusable generic Authentication and Authorization API. Implement the authenticated session lifecycle and the access credential mechanism used by consuming applications."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Establish a Session After Successful Login (Priority: P1)

A user who successfully authenticates into an application receives an authenticated session bound to that user and that application, together with an access credential that represents the session and that consuming applications can present to prove authenticated identity.

**Why this priority**: This is the core purpose of the feature; without a session and a credential, a successful login produces nothing a consuming application can use.

**Independent Test**: Authenticate an active user with an active membership in an active application, and confirm that a session is created for exactly that user and application with an explicit creation time and expiration, and that a usable access credential is returned.

**Acceptance Scenarios**:

1. **Given** a successful authentication result for a user and an application, **When** session issuance runs, **Then** a session is created that identifies that user and that application, has a stable unique identifier, an explicit creation time, and an explicit expiration time.
2. **Given** a created session, **When** the access credential is issued, **Then** the credential allows a receiving system to establish the authenticated user, the application, the session, and the credential expiration.
3. **Given** a successful login, **When** the response is produced, **Then** the password is not re-validated for session issuance, and the credential exposes no password data, no password hash, no sensitive profile data, and no business-domain data.
4. **Given** the user, application, or membership is no longer active at the moment of session issuance, **When** session issuance runs, **Then** no session and no credential are created.
5. **Given** a client supplies its own user or application identity claims, **When** a session is requested, **Then** those claims are ignored and only the trusted authentication result is used.

---

### User Story 2 - Validate Access Credentials (Priority: P1)

A consuming application presents an access credential and obtains a trustworthy answer on whether it currently represents valid authenticated access, and for which user, application, and session.

**Why this priority**: A credential has no value unless it can be validated; this is what makes the session usable by consuming applications.

**Independent Test**: Validate a freshly issued credential and confirm acceptance with the correct user, application, and session; then validate a tampered credential, an expired one, and one for a session in an ineligible state, and confirm each is rejected.

**Acceptance Scenarios**:

1. **Given** a credential for an active session, **When** it is validated, **Then** validation succeeds and returns the authenticated user, application, and session identifiers.
2. **Given** a tampered, malformed, or otherwise unrecognized credential, **When** it is validated, **Then** validation fails.
3. **Given** a credential whose session or credential lifetime has elapsed, **When** it is validated, **Then** validation fails, with expiration decided from server-controlled time and never from client-supplied time.
4. **Given** any rejected credential, **When** the external response is inspected, **Then** it does not reveal whether the session existed, was revoked, whether the user was disabled, or any cryptographic detail.
5. **Given** a credential that was issued for an application, **When** it is used in the context of a different application, **Then** validation fails.

---

### User Story 3 - Log Out and Revoke a Session (Priority: P1)

A signed-in user logs out, and the corresponding session is permanently invalidated so that its credential no longer grants access, even though its original expiration has not yet been reached.

**Why this priority**: Logout that only discards the credential on the client is not a real logout; durable revocation is a core security guarantee of the feature.

**Independent Test**: Create a session, confirm its credential validates, log out, and confirm the same credential is now rejected, including after a restart of the service.

**Acceptance Scenarios**:

1. **Given** an active session, **When** the user logs out with that session's credential, **Then** the session is revoked and the credential no longer grants access.
2. **Given** a revoked session whose original expiration has not been reached, **When** its credential is validated, **Then** validation fails.
3. **Given** a revoked session, **When** the service is restarted, **Then** the session remains revoked.
4. **Given** a user with several sessions, **When** one session is revoked, **Then** only that session becomes invalid and all others remain valid.
5. **Given** an already revoked, expired, or unknown session, **When** logout or revocation is requested for it, **Then** the outcome is safe and does not reveal internal state or fail with a generic server error.

---

### User Story 4 - Keep Sessions Isolated by Application and Independent from Each Other (Priority: P1)

A user may hold several simultaneous sessions, in the same or different applications. Each session belongs to exactly one application and one user, and can be inspected and revoked independently; a session for one application never grants access to another.

**Why this priority**: Application isolation is a foundational constraint of this multi-application service, and independent concurrent sessions are expected for normal multi-device usage.

**Independent Test**: Create two sessions for one user in application X and one in application Y; confirm all validate independently, revoking one leaves the others valid, and the application Y session is rejected when used in application X's context.

**Acceptance Scenarios**:

1. **Given** a user with two sessions in application X and one in application Y, **When** each is validated, **Then** each resolves to its own session identifier and its own application.
2. **Given** a user with active memberships in applications A and B and a session issued for A, **When** that session is used in the context of B, **Then** access is rejected.
3. **Given** concurrent sessions for one user, **When** one of them is revoked or expires, **Then** the others are unaffected.
4. **Given** no explicit policy restricting sessions, **When** a user logs in repeatedly, **Then** each login creates a new independent session and none replaces another.

---

### User Story 5 - Reflect Loss of Eligibility in Existing Sessions (Priority: P2)

When a user, an application, or a membership becomes inactive after a session was issued, the existing session must not remain valid indefinitely; likewise, role and permission changes must follow an explicitly defined freshness policy.

**Why this priority**: Eligibility changes are security-relevant, but they refine the behavior of sessions that already work, so they follow the core session lifecycle.

**Independent Test**: Create a session, deactivate the user, then the application, then the membership (each in separate runs), and confirm the effect on the existing session matches the defined policy; change a role or permission and confirm the effect matches the defined freshness policy.

**Acceptance Scenarios**:

1. **Given** an active session, **When** the user becomes inactive, **Then** existing access follows the explicitly defined policy [NEEDS CLARIFICATION: Should deactivation of a user, an application, or a membership invalidate existing sessions immediately, on the next validation, or only when the current credential expires?] and never remains valid indefinitely.
2. **Given** an active session, **When** the application or membership becomes inactive, **Then** the same defined policy applies.
3. **Given** a user, application, or membership that is inactive, **When** a new session is requested, **Then** it is refused.
4. **Given** role or permission changes after a session was issued, **When** access is next evaluated, **Then** the effect follows the explicitly defined authorization-freshness policy, which is application-scoped, never treats inactive roles, inactive permissions, or inactive memberships as effective, and never leaks authorization data from one application to another.

---

### User Story 6 - Observe Session Lifecycle Safely (Priority: P3)

Operators can follow session lifecycle events (creation, revocation, logout, rejected expired or revoked access) through structured logs and recorded security events carrying only stable identifiers, never credential secrets.

**Why this priority**: Observability supports operations and the future security-audit feature but does not change whether sessions behave correctly.

**Independent Test**: Create, use, revoke, and reject sessions, then inspect logs and recorded events and confirm they carry user, application, and session identifiers and contain no credential material.

**Acceptance Scenarios**:

1. **Given** a session is created, revoked, or logged out, **When** the operation completes, **Then** a structured log entry and a security event are recorded with user, application, and session identifiers.
2. **Given** an expired or revoked credential is presented, **When** it is rejected, **Then** the rejection is recorded internally with its specific cause, while the external response stays generic.
3. **Given** any lifecycle operation, **When** logs and events are inspected, **Then** no complete access credential or credential secret appears.

---

### Edge Cases

- What happens when session creation is requested for a user whose membership was deactivated between authentication and issuance? Session issuance is refused and no session or credential exists.
- What happens when the same credential is presented concurrently with a logout? The outcome is consistent with the durable session state at the time of evaluation; once logout completes, all later validations fail.
- What happens when a credential's lifetime would extend past its session's expiration? It MUST NOT; the credential never outlives its session.
- What happens when a revoked or expired session is revoked again? The operation is safe and idempotent from the caller's perspective.
- What happens when the credential signing/secret configuration is missing or invalid? The service fails safely without issuing credentials and without exposing configuration or key material.
- What happens when infrastructure is unavailable during validation? The result is a safe failure that does not grant access and does not expose internals.
- What happens when the session lifetime configuration is zero, negative, or absent? The configuration is rejected rather than silently creating non-expiring sessions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST create a session only from a trusted successful authentication result produced by the existing login capability, and MUST NOT accept client-supplied user or application identity claims as a basis for session creation.
- **FR-002**: System MUST NOT re-validate the password when creating a session after a successful login, and MUST keep credential authentication and session issuance as separate internal concerns even when they are exposed through a single login response.
- **FR-003**: Every session MUST represent exactly one user and exactly one application and MUST have a stable unique identifier, an explicit creation time, an explicit expiration time, and a durable revocation state.
- **FR-004**: Session creation MUST verify, at the moment of creation, that the user is active, the application is active, and the user's membership in that application is active; otherwise no session and no credential MUST be created.
- **FR-005**: Session lifetime MUST be explicit and externally configurable; a session MUST begin at its creation time and expire at creation time plus the configured lifetime, evaluated against server-controlled time only; invalid lifetime configuration MUST be rejected.
- **FR-006**: A session MUST support three conceptual states: Active (not expired, not revoked, and with an eligible user, application, and membership), Expired (expiration time has passed), and Revoked (explicitly invalidated); only an Active session MAY authorize access.
- **FR-007**: System MUST issue an access credential for each newly created session that lets a receiving system establish the authenticated user identifier, the application identifier, the session identifier, and the credential expiration.
- **FR-008**: The access credential MUST NOT contain password data, security stamps, sensitive profile data, unnecessary personally identifiable information, or business-domain data.
- **FR-009**: The access credential's expiration MUST be explicit and MUST NOT exceed the expiration of its parent session.
- **FR-010**: System MUST provide a way to validate an access credential that checks its authenticity, its expiration, the existence and revocation state of its session, and its application context, and MUST NOT rely solely on the cryptographic validity of the credential when revocation or eligibility state must be honored.
- **FR-011**: System MUST reject tampered, malformed, expired, revoked, or unrecognized credentials and credentials presented for a different application than the one they were issued for.
- **FR-012**: External rejection responses MUST be safe and MUST NOT reveal whether a session existed, was revoked, whether the user was disabled, or any internal cryptographic detail; internal diagnostics MAY distinguish causes.
- **FR-013**: System MUST allow a specific session to be revoked, and revocation MUST be durable across restarts and take effect so that the session's credential no longer grants access; the model MUST permit later broader revocation (such as all sessions of a user) without restructuring, but this feature MUST NOT implement such speculative operations.
- **FR-014**: System MUST provide logout that revokes the session identified by the presented credential; logout MUST NOT consist solely of client-side discarding of the credential.
- **FR-015**: A user MAY hold multiple concurrent sessions, including several in the same application; each MUST be independently identifiable, validatable, and revocable, and no single-session restriction MUST be imposed.
- **FR-016**: A session issued for one application MUST NOT authorize access in any other application, even when the user holds an active membership in both.
- **FR-017**: An inactive user, an inactive application, or an inactive membership MUST NOT obtain new sessions.
- **FR-018**: The effect on already existing sessions of user deactivation, application deactivation, and membership deactivation MUST be explicitly defined and implemented, and existing sessions MUST NOT silently remain valid indefinitely after eligibility is lost [NEEDS CLARIFICATION: exact timing of the effect on existing sessions — immediate, on next validation, or at credential expiration — see User Story 5].
- **FR-019**: The authorization-freshness policy for role removal, role deactivation, permission removal, and permission deactivation after issuance MUST be explicitly defined; any role or permission information associated with access MUST be application-scoped, MUST exclude inactive roles and permissions and anything granted through an inactive membership, and MUST NOT leak between applications.
- **FR-020**: Whether sessions can be renewed without re-entering credentials MUST be an explicit decision; absent a concrete need, no renewal mechanism is introduced [NEEDS CLARIFICATION: Is session renewal without re-entering credentials required now, or is re-login on expiry acceptable?]; if a renewal mechanism is adopted, its lifecycle, expiration, revocation, rotation, relationship to the session, secure storage, and replay behavior MUST be specified first.
- **FR-021**: Expected failures (expired or invalid credential, revoked or unknown session, inactive user, application, or membership, application mismatch) MUST produce safe, defined responses and MUST NOT surface as generic server errors; unexpected infrastructure failures MUST use a safe error contract exposing no secrets, token internals, data-store details, or stack traces.
- **FR-022**: Credential secrets and signing material MUST come from external configuration, MUST NOT be committed to the repository, MUST NOT appear in logs, and MUST NOT be stored in recoverable plaintext form where a verifiable derived representation suffices; secret material MUST be generated and protected using established platform cryptographic capabilities, never custom algorithms.
- **FR-023**: Any new anonymous, abuse-prone session endpoint MUST be rate limited consistently with the existing login protection; normal authenticated access MUST NOT be rate limited without a concrete requirement.
- **FR-024**: System MUST emit structured logs and internal security events for session creation, revocation, logout, and rejected expired or revoked access, using stable identifiers (user, application, session) and never full credentials, in a form that a later security-audit feature can consolidate.
- **FR-025**: Session state MUST be persisted durably so revocation, expiration, auditability, and concurrent sessions work; persistence MUST reuse the existing relational data infrastructure and schema changes MUST be applied through migrations.
- **FR-026**: The session domain model and application use cases MUST NOT depend on the concrete credential technology, the web framework, identity framework, data-access framework, or database; those concerns MUST be reached through focused ports implemented by infrastructure.
- **FR-027**: The concrete access-credential strategy and the validation mechanism consuming applications use (local, centralized, or hybrid) MUST be decided and justified in planning without introducing OAuth 2.0 or OpenID Connect behavior.
- **FR-028**: This feature MUST NOT introduce password recovery, password reset, MFA, social login, an OAuth 2.0 authorization server, an OpenID Connect provider, SSO federation, business-domain authorization rules, the complete consuming-API authorization contract, an administrative frontend, a complete audit subsystem, device management features, distributed session caches, or message brokers.

### Key Entities *(include if feature involves data)*

- **Session**: An authenticated relationship between one user and one application created by one successful authentication event, with a stable identifier, creation time, expiration time, and durable revocation state; it may be Active, Expired, or Revoked and is independent of any credential format.
- **Access Credential**: The artifact issued for a session that a consuming application presents to prove authenticated identity; it carries or resolves to user, application, session, and expiration, never outlives its session, and is never stored or logged in recoverable full form beyond initial delivery.
- **Session Security Event**: A record of a session lifecycle outcome (created, revoked, logout, rejected expired, rejected revoked) carrying user, application, and session identifiers and timestamp, and no credential secrets, usable by a future audit feature.
- **Authenticated Identity Context**: The validated information handed to consuming applications after successful validation: user identifier, application identifier, and session identifier, plus any application-scoped authorization data the freshness policy requires.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user who authenticates successfully receives a session and a usable access credential in a single login interaction, in 100% of tested valid logins.
- **SC-002**: 100% of tested sessions identify exactly one user and one application and carry an explicit creation and expiration time.
- **SC-003**: 100% of tested tampered, malformed, expired, and revoked credentials are rejected, and 0% of rejected responses reveal the specific cause to an external caller.
- **SC-004**: After logout, the revoked session's credential is rejected in 100% of tested cases, including after a service restart.
- **SC-005**: In 100% of tested cases, a session issued for one application is rejected in the context of any other application, even when the user is a member of both.
- **SC-006**: Revoking or expiring one of several concurrent sessions of a user leaves every other session of that user valid in 100% of tested cases.
- **SC-007**: In 100% of tested cases, inactive users, inactive applications, and inactive memberships are refused new sessions.
- **SC-008**: For each of user, application, and membership deactivation and each of role and permission changes, the observed effect on existing access matches the explicitly documented policy in 100% of tested scenarios.
- **SC-009**: No issued credential has an expiration later than its session's expiration in any tested scenario.
- **SC-010**: 0 occurrences of complete credentials or credential secrets appear in logs, security events, or error responses across all tested scenarios.
- **SC-011**: Domain and application layers show 0 references to the concrete credential technology, web framework, identity framework, or data-access framework, as verified by architecture tests.
- **SC-012**: The next features (password management and the authorization contract) can build on sessions and credentials without restructuring this feature's session model.

## Assumptions

- Session lifetime and credential lifetime are operational configuration values owned by deployment configuration; their exact durations are chosen during clarification and planning, subject to the rule that a credential never outlives its session.
- Multiple simultaneous sessions per user, including several per application, are allowed by default; no single-session policy is required.
- Session renewal without credentials is not assumed to be needed (YAGNI) unless clarification identifies a concrete requirement.
- Application context for validation comes from the credential itself and is compared with the application the consuming application is acting for; the mechanism for supplying that context to validation is a planning decision.
- Login (`005-authentication-login`) continues to own credential verification, lockout, and login rate limiting; this feature consumes its successful result and does not alter its rules.
- Role and permission data from `004-roles-permissions` remains the single source of effective authorization; this feature decides only how session access interacts with it.
- Session-related security events are recorded through the same internal event mechanism already established for authentication, without creating the full audit subsystem planned for `009-security-audit`.
- The existing PostgreSQL and EF Core infrastructure, and the development container workflow, are reused without creating duplicate infrastructure.
