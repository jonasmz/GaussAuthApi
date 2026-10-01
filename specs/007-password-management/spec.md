# Feature Specification: Password Management

**Feature Branch**: `007-password-management`

**Created**: 2026-10-01

**Status**: Draft

**Input**: Secure self-service password change, password recovery, and password reset for the reusable authentication API.

## Clarifications

### Session 2026-10-01

- Q: ¿Qué política debe aplicarse a las sesiones después de un cambio de contraseña o un reset olvidado? → A: Revocar todas las sesiones después de ambos flujos.
- Q: ¿Un reset exitoso de contraseña debe limpiar un bloqueo temporal de inicio de sesión? → A: Limpiar el bloqueo tras un reset exitoso.
- Q: ¿Qué mecanismo autorizado debe entregar instrucciones de recuperación durante desarrollo y pruebas? → A: Archivo local protegido y excluido del repositorio.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Change My Password (Priority: P1)

An authenticated user changes their own password after proving knowledge of their current password. The user cannot nominate another account, and the new password is accepted only when it satisfies the configured password requirements.

**Why this priority**: Account owners need a safe way to respond to suspected password exposure without administrative intervention.

**Independent Test**: With a valid session, submit a correct current password and an acceptable new password; the old password no longer authenticates, the new password does, and the configured session policy is applied.

**Acceptance Scenarios**:

1. **Given** an active user with a valid session and current password, **When** they submit the correct current password and an acceptable new password, **Then** their password changes and all of their active sessions are revoked.
2. **Given** a valid session, **When** the submitted current password is incorrect, **Then** no password or session state is changed and the response does not reveal sensitive credential detail.
3. **Given** a valid session, **When** the proposed password violates configured requirements, **Then** the password remains unchanged and the user receives useful validation feedback without exposing security internals.
4. **Given** an inactive user, **When** they attempt to change their password, **Then** they do not obtain active access and their account remains inactive.

---

### User Story 2 - Request Password Recovery (Priority: P1)

A person who forgot their password can request recovery instructions using an email address. The public outcome is the same whether the account is unknown, inactive, or eligible.

**Why this priority**: Recovering access without support intervention is essential, but the public flow must not disclose which accounts exist.

**Independent Test**: Submit requests for an eligible account, an unknown email, and an inactive account; verify that all have the same public status and response shape, while only an eligible account can receive usable instructions.

**Acceptance Scenarios**:

1. **Given** an eligible account, **When** a recovery request is submitted with its email, **Then** recovery instructions are sent through the configured delivery boundary and the public response remains generic.
2. **Given** an unknown or inactive account, **When** a recovery request is submitted, **Then** the same generic public response is returned and no account is activated.
3. **Given** repeated or abusive recovery requests, **When** the applicable configured limit is exceeded, **Then** further requests are limited without revealing account state.

---

### User Story 3 - Reset a Forgotten Password (Priority: P1)

A person who possesses valid recovery instructions establishes a new password. Invalid, expired, malformed, or reused recovery credentials fail safely and do not disclose account or token details.

**Why this priority**: Recovery is only complete when the owner can securely establish a new credential and contain an account compromise.

**Independent Test**: Use a valid delivered recovery credential to set an acceptable new password; verify that the old password no longer authenticates, the new one does, all affected sessions follow the defined policy, and invalid credentials fail safely.

**Acceptance Scenarios**:

1. **Given** a valid recovery credential for an eligible account, **When** a compliant new password is submitted, **Then** the password changes, any temporary login lockout is cleared, all of the user's active sessions are revoked, and a password-reset security event is recorded without secrets.
2. **Given** an invalid, expired, malformed, or already-used recovery credential, **When** reset is attempted, **Then** the password and account state remain unchanged and the public failure is safe and non-revealing.
3. **Given** an inactive account, **When** it presents a recovery credential, **Then** password reset does not activate the account or grant active access.

---

### User Story 4 - Security Operations Observe Password Events (Priority: P2)

Security operations can observe password-management outcomes through safe events and session-revocation events, without receiving passwords, reset credentials, hashes, or security metadata.

**Why this priority**: Password changes and recovery are account-security signals that must be available to the later audit capability.

**Independent Test**: Exercise successful change, recovery request, successful reset, invalid reset, and policy-driven session invalidation; verify appropriate events include identifiers and outcome categories only.

**Acceptance Scenarios**:

1. **Given** a successful password change or reset, **When** it completes, **Then** the corresponding security event and any caused session-revocation events are recorded with identifiers only.
2. **Given** an invalid recovery credential, **When** reset is attempted, **Then** a safe failure event may be recorded without the credential or account-existence information being exposed to clients.

### Edge Cases

- A missing, oversized, malformed, or non-email recovery request must fail validation without echoing submitted sensitive data.
- A missing, oversized, malformed, or reused reset credential must not alter account or session state.
- Concurrent password changes or resets must leave one consistent credential state and must not restore revoked sessions.
- A delivery failure after creating recovery instructions must not reveal account existence or expose the recovery credential; the protected local delivery file must be unavailable to public clients and excluded from source control.
- Password changes and resets affect the user globally; they must not alter memberships, roles, permissions, or application-specific data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let an active user change only their own password after validating a current authenticated session and the current password; the request MUST NOT accept a target user identifier.
- **FR-002**: The system MUST validate a proposed password against the configured password requirements and preserve the existing password when validation fails.
- **FR-003**: The system MUST keep password hashing, storage, current-password verification, recovery-credential generation, and recovery-credential validation within the established identity credential service; application and domain data MUST NOT store password hashes or recovery credentials.
- **FR-004**: The system MUST offer a public recovery-request capability accepting an email address and MUST return the same intentionally generic public outcome for eligible, unknown, and inactive accounts.
- **FR-005**: The system MUST issue usable recovery instructions only for an eligible, active account and MUST send those instructions through a focused recovery-delivery boundary separate from credential generation.
- **FR-006**: The system MUST let a holder of a valid, temporary recovery credential establish a new password; invalid, expired, malformed, and reused credentials MUST fail without changing credential, user-activation, membership, role, permission, or session state.
- **FR-007**: The system MUST apply configurable abuse protection to the public recovery-request and reset capabilities.
- **FR-008**: The system MUST revoke all active sessions for the affected user after both password change and password reset, using the established session-revocation capability and no separate revocation mechanism.
- **FR-009**: The system MUST keep passwords, reset credentials, password hashes, security stamps, and complete sensitive request bodies out of logs, events, responses, and application-controlled persistence.
- **FR-010**: The system MUST record safe security events for successful password changes, recovery requests where appropriate, successful resets, useful safe reset failures, and session revocations caused by credential changes.
- **FR-011**: The system MUST not automatically activate an inactive user after recovery or reset, and MUST keep password credentials global to the user across all applications.
- **FR-012**: The system MUST preserve configured login-lockout protections and clear a temporary login lockout only after a successful password reset; password reset MUST NOT activate an inactive user.
- **FR-013**: Recovery delivery MUST be testable without selecting a production provider; in development and tests, instructions MUST be written only to a protected local file excluded from source control and public responses. Delivery failure behavior MUST be safe and non-enumerating.
- **FR-014**: The feature MUST NOT add MFA, passkeys, social login, OAuth, OpenID Connect, SSO, administrative password override, password history, account activation through reset, a complete audit subsystem, or business-domain behavior.

### Key Entities

- **Password credential**: The globally owned authentication secret for one user; its representation, hashing, and lifecycle remain inside credential infrastructure.
- **Recovery credential**: A temporary, user-bound proof used only to establish a new password; it is never application-domain data or client-visible server state.
- **Recovery instruction**: A delivery payload that permits an authorized recipient to use a recovery credential; its delivery is separated from credential generation.
- **Session set**: The user’s active sessions across applications, which are affected only through the explicit policy in FR-008.
- **Security event**: A safe record of a password-management outcome containing only permitted identifiers and event categories.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In automated acceptance tests, 100% of successful self-service password changes require the current password and invalidate or preserve sessions exactly as the approved policy specifies.
- **SC-002**: In automated acceptance tests, recovery requests for eligible, unknown, and inactive email addresses return identical public status codes and response shapes.
- **SC-003**: In automated acceptance tests, a valid recovery credential changes the password, while invalid, expired, malformed, and reused credentials leave the prior password and session state unchanged.
- **SC-004**: In automated acceptance tests, public recovery and reset requests exceeding configured limits are rejected, while requests within the configured limit remain usable.
- **SC-005**: Security-event and log inspection across all password-management scenarios finds zero occurrences of submitted passwords, recovery credentials, password hashes, or security stamps.
- **SC-006**: Password-management acceptance tests confirm that memberships, roles, permissions, user activation, and application-specific data are unchanged by change or reset operations.

## Assumptions

- Existing users are already provisioned with global credentials through the established user-creation flow.
- Password rules remain externally configurable; this feature introduces no password-history or reuse policy.
- Recovery applies to the global user identity and does not require application membership or application selection.
- Existing session validation and revocation are the sole mechanism for revoking all active sessions after a password change or reset.
- Production delivery provider selection is deferred; development and tests use a protected local file excluded from source control, while the plan specifies a focused delivery adapter without selecting a production provider.
- Public responses use safe, generic wording and do not distinguish account existence, activity, credential validity, delivery outcome, or identity-service internals.
