# Feature Specification: Application-Scoped Authentication Login

**Feature Branch**: `005-authentication-login`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature `005-authentication-login` for the reusable generic Authentication and Authorization API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authenticate Into an Active Application (Priority: P1)

A registered user with an active account, an active target application, and an active membership in that application signs in with their email and password to prove their identity for that application context.

**Why this priority**: This is the entire purpose of the feature; without it there is no authentication capability to build the future session feature on.

**Independent Test**: Create an active user, an active application, and an active membership between them, then submit the application context, the user's normalized email, and the correct password, and confirm the login succeeds with the stable user and application identifiers.

**Acceptance Scenarios**:

1. **Given** an active user with an active membership in an active application, **When** the user submits the correct email and password for that application, **Then** authentication succeeds and the result includes the stable user identifier, the stable application identifier, and a successful outcome.
2. **Given** an active user whose email was registered in mixed case or with surrounding whitespace, **When** the user submits the same email in a different case or form that normalizes to the same identity, **Then** authentication succeeds using normalized email matching.
3. **Given** a successful authentication, **When** the result is produced, **Then** no password, password hash, or other Identity credential detail is included in the result, in logs, or in any exception.

---

### User Story 2 - Reject Authentication for Invalid Credentials or Unknown Accounts (Priority: P1)

A user submits an email and password that do not correspond to a valid, matching, active account, and the system rejects the attempt without revealing which specific check failed.

**Why this priority**: Credential rejection is as fundamental as acceptance; without it, the endpoint would authenticate anyone, and without a uniform failure contract it would leak account existence.

**Independent Test**: Attempt login with a non-existent email and separately with a valid email paired with a wrong password, and confirm both attempts fail with the same externally observable outcome shape.

**Acceptance Scenarios**:

1. **Given** no user is registered with the submitted email, **When** a login is attempted, **Then** authentication fails with the same generic failure outcome used for an incorrect password.
2. **Given** a registered user, **When** a login is attempted with an incorrect password, **Then** authentication fails with the same generic failure outcome used for an unknown email.
3. **Given** a failed authentication attempt, **When** the failure response is inspected, **Then** it does not indicate whether the email existed, the password was wrong, the user was inactive, the application was invalid, or the membership was missing or inactive.

---

### User Story 3 - Enforce Account, Application, and Membership State Boundaries (Priority: P1)

Authentication must fail whenever the user, the target application, or the user's membership in that application is not active, even when the submitted credentials are otherwise correct, and membership state in one application must never affect authentication in another application.

**Why this priority**: This enforces the access boundaries established by prior features (user activation, application activation, membership activation) and prevents authentication from silently bypassing them.

**Independent Test**: Build a user with valid credentials and an active membership in application A but no membership (or an inactive membership) in application B, and confirm login succeeds only for application A; then deactivate the user, the application, or the membership individually and confirm each independently blocks login while correct credentials remain unchanged.

**Acceptance Scenarios**:

1. **Given** an active user with valid credentials and an active membership only in application A, **When** the user attempts to log in to application B, **Then** authentication fails even though the credentials are correct.
2. **Given** an inactive user with otherwise correct credentials, an active application, and an active membership, **When** login is attempted, **Then** authentication fails and the user is not automatically reactivated.
3. **Given** an active user with correct credentials and an active membership, but the target application is inactive, **When** login is attempted, **Then** authentication fails even though the membership record still exists.
4. **Given** an active user, an active application, but no membership or an inactive membership for that application, **When** login is attempted, **Then** authentication fails.

---

### User Story 4 - Protect the Login Endpoint from Abuse (Priority: P2)

Repeated invalid login attempts against one account must eventually lock that account out of further attempts, and the login endpoint itself must limit the rate of requests it accepts, so that brute-force and credential-stuffing attempts are constrained.

**Why this priority**: The login endpoint is the first public, unauthenticated, internet-facing surface introduced by the authorization API and is a direct target for automated abuse; this protection is necessary for the feature to be safely exposed, but it is secondary to the core authenticate/reject behavior.

**Independent Test**: Submit repeated incorrect passwords for one account until lockout engages and confirm a subsequent correct-password attempt still fails while locked out; separately, submit login requests beyond the configured rate limit from one source and confirm later requests in the same window are rejected before reaching credential validation.

**Acceptance Scenarios**:

1. **Given** repeated invalid password attempts against one account reach the configured lockout threshold, **When** a further login attempt is made with the correct password, **Then** authentication still fails because the account is locked out.
2. **Given** a locked-out account, **When** lockout state is inspected internally, **Then** it is managed through the existing Identity lockout mechanism rather than a custom mechanism.
3. **Given** login requests from one source exceed the configured rate limit within the configured window, **When** an additional request is submitted in that window, **Then** the request is rejected by rate limiting before credential validation occurs.
4. **Given** a rate-limited or locked-out rejection, **When** the response is inspected, **Then** it remains a safe, non-revealing response consistent with other authentication failures where practical for the mechanism involved.

---

### User Story 5 - Record Authentication Security Events (Priority: P3)

Every meaningful authentication attempt produces an internal security event capturing enough information for auditing, without ever capturing the submitted password or any derived credential secret.

**Why this priority**: Security observability is important for operating the system safely, but it does not change whether authentication itself behaves correctly, so it is the lowest priority among the required behaviors.

**Independent Test**: Trigger one successful login and one failed login, and confirm that both produce an internally recorded event carrying only safe metadata (such as user identifier when known, application identifier when known, timestamp, and outcome category) with no password or credential content present.

**Acceptance Scenarios**:

1. **Given** a successful login, **When** the attempt completes, **Then** a security event recording the successful outcome is captured with safe metadata only.
2. **Given** a failed login for any reason, **When** the attempt completes, **Then** a security event recording the failed outcome is captured with safe metadata only, and the event never contains the submitted password or a password hash.
3. **Given** an account reaches lockout, **When** that occurs, **Then** a security event recording the lockout is captured where practical.

---

### Edge Cases

- What happens when the submitted application identifier does not correspond to any registered application? Authentication must fail using the same safe, generic failure contract as other rejections, without revealing that the application itself does not exist.
- What happens when the email is syntactically invalid or missing, or the password is missing or empty? The request must be rejected as invalid input before any credential or state validation occurs, without echoing the submitted password.
- What happens when two concurrent login attempts for the same account occur at the same time (for example, one with a correct password and one with an incorrect password)? Each attempt must be evaluated independently against the current user, application, membership, and Identity lockout state without bypassing any of those checks, and the outcome of each must be consistent with that state at the time it was evaluated.
- What happens when a user is a member of multiple applications with different states? Each login request is evaluated strictly within its own stated application context; membership state in one application never influences the outcome for another.
- What happens when login succeeds for a user who has no roles or permissions in the application? Authentication succeeds based on identity, application, and membership validity alone; the presence or absence of roles or permissions does not affect the authentication decision.
- What happens when an unexpected infrastructure failure occurs during authentication (for example, the database is unreachable)? The endpoint must report this as a distinct, safe internal failure rather than as a credential rejection, and must not expose internal diagnostic details externally.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a login request that specifies, at minimum, the target application context, an email, and a password.
- **FR-002**: System MUST normalize the submitted email using the same normalization semantics already established for user identity in `002-users-profiles` before resolving the corresponding account.
- **FR-003**: System MUST validate the submitted password against ASP.NET Core Identity's existing credential-verification capability and MUST NOT implement or substitute any custom password hashing or comparison logic.
- **FR-004**: System MUST require, for authentication to succeed, that the user exists, the user is active, the target application exists, the target application is active, the user has an application membership for the target application, that membership is active, and the submitted password is valid; authentication MUST fail if any one of these conditions is not satisfied.
- **FR-005**: System MUST NOT automatically reactivate an inactive user, an inactive application, or an inactive membership as a side effect of a login attempt.
- **FR-006**: System MUST evaluate membership state independently per application; an active membership in one application MUST NOT permit authentication in a different application, and an inactive or missing membership in one application MUST NOT affect authentication in another application where the user holds an active membership.
- **FR-007**: System MUST return a uniform, safe failure outcome for every expected rejection reason — including unknown email, incorrect password, inactive user, invalid or inactive application, and missing or inactive membership — such that the externally observable response does not reveal which specific condition caused the rejection.
- **FR-008**: System MUST NOT log, persist outside existing Identity credential storage, return in any response, or include in any exception or telemetry the plaintext password submitted during a login attempt.
- **FR-009**: System MUST produce, for a successful authentication, a result that includes at minimum the stable user identifier, the stable application identifier, and a successful outcome indicator, sufficient for a subsequent session-establishment feature to act upon.
- **FR-010**: System MUST keep authentication and authorization as separate concerns; a successful login MUST establish identity and application/membership validity only, and MUST NOT be required to embed roles or effective permissions into the authentication result.
- **FR-011**: System MUST apply ASP.NET Core Identity's account lockout behavior to the login flow, such that repeated invalid password attempts against one account can contribute to lockout, and MUST reject authentication for a currently locked-out account even when the submitted password is correct.
- **FR-012**: System MUST apply rate limiting to the login endpoint, independent of Identity lockout, such that excessive login requests from one source within a configured window are rejected before reaching credential validation.
- **FR-013**: Lockout thresholds, lockout duration, and rate-limit thresholds and windows MUST be externally configurable rather than fixed in domain logic.
- **FR-014**: System MUST record a security event for each meaningful authentication attempt outcome, at minimum successful login, failed login, and account lockout where practical, and these events MUST NOT contain the submitted password, a password hash, or any other authentication secret.
- **FR-015**: System MUST validate login request input — including presence of application context, presence and basic format of the email, presence of the password, and reasonable maximum field lengths — and MUST reject invalid input without echoing the submitted password back to the caller.
- **FR-016**: System MUST distinguish internally between an expected authentication rejection and an unexpected infrastructure failure, and MUST NOT expose internal diagnostic details (such as stack traces, data-store errors, or Identity internals) in the external response for either case.
- **FR-017**: The Domain and Application layers MUST NOT depend on, instantiate, or directly reference ASP.NET Core Identity infrastructure types; credential verification MUST be reached only through a minimal, focused abstraction that Infrastructure implements.
- **FR-018**: System MUST NOT introduce a complete session, access-token, refresh-token, OAuth 2.0, OpenID Connect, or multi-factor authentication capability as part of this feature.

### Key Entities *(include if feature involves data)*

- **Authentication Attempt**: A transient request to prove identity for one user within one application context, composed of the submitted application context, normalized email, and password; it is not persisted as submitted and exists only for the duration of the request.
- **Authentication Result**: The outcome of an authentication attempt; on success it carries the stable user identifier, the stable application identifier, and a success indicator; on failure it carries only a uniform, safe rejection indicator with no detail about the specific cause.
- **Authentication Security Event**: A record of a meaningful authentication outcome (success, failure, or lockout) carrying safe metadata such as user identifier when known, application identifier when known, timestamp, and outcome category, and never carrying password or credential secret content. This reuses the user, application, and membership entities already defined in `002-users-profiles` and `003-applications-memberships` without introducing new identity or membership concepts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with valid credentials, an active account, an active target application, and an active membership can authenticate successfully in a single request.
- **SC-002**: 100% of tested rejection scenarios (unknown email, wrong password, inactive user, inactive or missing application, missing or inactive membership) return the same externally observable failure shape, with zero scenarios distinguishable from one another by an external caller.
- **SC-003**: 100% of tested successful and failed authentication attempts produce zero occurrences of the submitted password or any password-derived secret in logs, responses, or recorded security events.
- **SC-004**: A user holding an active membership in one application and no active membership in a second application can authenticate in the first and is rejected in the second, in 100% of tested cases, with no cross-application leakage.
- **SC-005**: An account that reaches the configured invalid-attempt threshold becomes unable to authenticate even with the correct password until lockout clears, in 100% of tested lockout scenarios.
- **SC-006**: Requests to the login endpoint that exceed the configured rate limit within the configured window are rejected before credential validation occurs, in 100% of tested rate-limit scenarios.
- **SC-007**: The authentication result produced for a successful login contains sufficient stable identifiers (user and application) for the next feature to establish a session without requiring any architectural change to this feature.

## Assumptions

- Email normalization reuses the identity-resolution semantics already established in `002-users-profiles`; no new normalization rule is introduced by this feature.
- The application context in the login request may be satisfied by either the stable application identifier or the stable application code already defined in `003-applications-memberships`; the precise request shape is a planning-level detail, not a specification-level constraint, since both identifiers already uniquely resolve one application.
- "Safe, generic failure response" means the rejection reason is not distinguishable externally; it does not require the response body to be byte-for-byte identical across all failure types where incidental differences (such as standard HTTP status semantics) are themselves unavoidable artifacts of the underlying web framework rather than a disclosed cause. Internal logs and security events may still distinguish the cause for operators.
- Lockout thresholds and rate-limit thresholds are operational configuration values owned by deployment configuration, not values this specification fixes.
- Effective permissions from `004-roles-permissions` are not required in the login result for this feature to be considered complete; a future feature may choose to consult them for a clearly justified purpose without this specification mandating it.
- This feature produces identity/application/membership validation and a minimal success/failure outcome only; it does not define or imply any particular session, token, or claims representation, temporary or otherwise, as a public contract.
- The existing PostgreSQL and EF Core persistence infrastructure, and the existing ASP.NET Core Identity credential store, are reused without new business-domain persistence entities beyond what is strictly necessary to preserve authentication security-event information.
