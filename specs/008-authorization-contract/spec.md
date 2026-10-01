# Feature Specification: Authorization Contract

**Feature Branch**: `008-authorization-contract`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature `008-authorization-contract` for the reusable generic Authentication and Authorization API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Establish Current Authorization Context (Priority: P1)

A consuming business API receives a user's access credential and obtains a current, minimal authorization context for the application it serves, without reading the authentication service's persistence.

**Why this priority**: A business API cannot safely identify its caller or enforce permissions until it has an authoritative, application-scoped context.

**Independent Test**: Present a valid credential for an active user and application to the published contract; verify the returned context identifies the same user, application, session, and expiration, and does not contain profile or internal data.

**Acceptance Scenarios**:

1. **Given** a valid, current credential for an active user in an active application with an active membership, **When** the consumer resolves authorization context for that application, **Then** it receives the authenticated UserId, ApplicationId, SessionId, relevant expiration, active roles, and effective permissions.
2. **Given** a malformed, expired, revoked, or otherwise invalid credential, **When** context is resolved, **Then** access is rejected with a safe result that does not disclose session, persistence, or signing details.
3. **Given** a context resolution succeeds, **When** its public representation is inspected, **Then** it contains no email, profile data, password data, security stamp, cryptographic material, or persistence entity.

---

### User Story 2 - Enforce Application Isolation (Priority: P1)

A consuming API accepts authorization only for its own application and never treats a credential, role, or permission from another application as valid.

**Why this priority**: Isolation between independent applications is a core security boundary of the service.

**Independent Test**: Issue credentials and assignments for two applications, then attempt to use a credential from Application A while resolving context for Application B; verify rejection and no role or permission leakage.

**Acceptance Scenarios**:

1. **Given** a credential issued for Application A, **When** a consumer configured for Application B requests context, **Then** the request is rejected.
2. **Given** a user with roles and permissions in Applications A and B, **When** context is resolved for A, **Then** only active roles and effective permissions from A are returned.
3. **Given** repeated role paths grant the same permission, **When** context is resolved, **Then** that permission appears once.

---

### User Story 3 - Authorize a Business Operation by Permission (Priority: P1)

A consuming API uses the published context to decide whether a caller has a named generic permission, while retaining ownership of its own business rules and data.

**Why this priority**: Permission evaluation is the contract's practical purpose; it lets independent APIs protect their own operations without coupling to Auth internals.

**Independent Test**: A test consumer receives a valid context and allows an operation only when the required permission is present, without querying the Auth database or referencing Auth persistence models.

**Acceptance Scenarios**:

1. **Given** a valid context containing a required permission, **When** the consumer evaluates that permission, **Then** it authorizes the operation.
2. **Given** a valid context lacking the required permission, **When** the consumer evaluates that permission, **Then** it denies the operation without invoking a business-specific rule in the Auth service.
3. **Given** a role, permission, role-permission relationship, membership, user, or application becomes inactive or changes after credential issuance, **When** context is next resolved, **Then** the result reflects the current authorization state according to the existing freshness policy.

---

### User Story 4 - Integrate Without Auth Persistence Coupling (Priority: P2)

A consuming API integrates using documented stable identifiers and authorization data, and can retain UserId references for its own audit fields without database links to the authentication service.

**Why this priority**: A reusable contract must permit independent deployment and persistence of business APIs.

**Independent Test**: An equivalent test consumer obtains and evaluates a published authorization context using only its credential and configured application identity; it has no Auth database access or references to internal models.

**Acceptance Scenarios**:

1. **Given** a consumer stores an actor reference, **When** it persists that reference, **Then** it stores the stable UserId as a logical value and creates no cross-database relationship.
2. **Given** the public contract evolves additively, **When** an existing consumer reads the fields it requires, **Then** it remains able to use the established fields without reliance on volatile internal structures.

### Edge Cases

- A credential that is cryptographically valid but whose session has been revoked, expired, or become ineligible is rejected because authoritative session state remains required.
- A credential presented for the wrong configured application is rejected even if the same user is active in both applications.
- A permission made inactive, removed from a role, or removed through membership changes is absent on the next authorization-context resolution; no permission cache delays that result.
- A context request for an unknown or inactive user, application, membership, session, or authorization relationship produces a safe rejection and reveals no internal state.
- A failed or unavailable context resolution never grants access based solely on an unverified or stale credential.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST publish a stable, dedicated authorization-context contract through which a consumer can establish authenticated UserId, ApplicationId, SessionId, credential expiration, active roles, and effective permissions.
- **FR-002**: The contract MUST build on the existing signed short-lived credential and authoritative session-state validation policy; signature validity alone MUST NOT be treated as enough when revocation or eligibility state must be checked.
- **FR-003**: A consumer MUST be able to verify that the context belongs to its configured ApplicationId, and a credential or context for one application MUST be rejected for every other application.
- **FR-004**: Context validity MUST reject expired credentials and sessions, revoked sessions, inactive users, inactive applications, inactive memberships, and application mismatches according to the established sessions policy.
- **FR-005**: The context MUST contain only current active roles belonging to its application and MUST never include roles from another application.
- **FR-006**: The context MUST contain only current effective permissions for its application, derived from active user, application, membership, roles, permissions, and assignments; duplicate permissions MUST be removed.
- **FR-007**: Roles and permissions MUST be resolved dynamically from the current authorization model and MUST NOT be embedded in the access credential; authorization changes MUST apply on the next context resolution.
- **FR-008**: The public context MUST use stable logical identifiers and dedicated contract data. It MUST NOT expose persistence entities, identity-framework models, password data, security stamps, cryptographic material, email, profile data, or other unnecessary personal information.
- **FR-009**: The contract MUST let a consumer evaluate whether the authenticated context contains a named permission. The Auth service MUST state generic capabilities only and MUST NOT implement business-operation rules.
- **FR-010**: The consumer integration contract MUST not require direct access to the Auth database, foreign keys to Auth persistence, credential verification duplication, or direct modification of Auth roles or permissions.
- **FR-011**: Context-resolution failures MUST use a consistent safe outcome and MUST NOT expose token, session-store, identity, SQL, or other internal details.
- **FR-012**: Logging and security events for contract validation MUST use stable identifiers and result categories where available, and MUST NOT contain complete access credentials, profile data, or other secrets.
- **FR-013**: The documented contract MUST define the stable meanings of UserId, ApplicationId, SessionId, issue/expiration data, roles, and permissions; future additions MUST not alter those established meanings.
- **FR-014**: Consumer documentation MUST explain credential receipt, consumer application identification, validation and context resolution, permission checks, expiration and revocation behavior, untrusted pre-validation data, and the prohibition on Auth database access.
- **FR-015**: The contract MUST not introduce a cache that weakens the existing authorization-freshness or revocation policy.
- **FR-016**: System MUST define a minimal mechanism that permits Auth to distinguish an authorized consuming API requesting authoritative authorization context from an arbitrary caller. [NEEDS CLARIFICATION: Which consumer authentication approach is approved: a per-application shared secret, mutual TLS, or another explicitly provided mechanism?]

### Key Entities *(include if feature involves data)*

- **Authorization Context**: The minimal current authorization information trusted by a consumer only after validation: UserId, ApplicationId, SessionId, expiration data, active roles, and unique effective permissions.
- **Consumer Application Identity**: The registered application identity a consumer uses to establish which application context it is allowed to request and accept; it is distinct from an end user's credential.
- **Permission Requirement**: A consumer-owned declaration that a business operation requires one named generic permission; it does not encode a business rule in the Auth service.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested valid cases, a consumer obtains the correct UserId, ApplicationId, SessionId, and expiration information from the authorization context.
- **SC-002**: In 100% of tested cross-application attempts, a credential issued for one application is rejected for another and exposes no roles or permissions from either application.
- **SC-003**: In 100% of tested contexts, roles and permissions are scoped to the target application, inactive authorization elements are excluded, and duplicate permissions are absent.
- **SC-004**: In 100% of tested expired, revoked, inactive, malformed, and unavailable-access cases, the consumer receives a safe rejection and grants no access.
- **SC-005**: In 100% of tested authorization changes, the next context resolution reflects the current effective permission state.
- **SC-006**: At least one independent consumer-boundary scenario validates a required permission using the published contract with no direct Auth persistence access.
- **SC-007**: Public contract responses contain 0 instances of password data, security stamps, cryptographic material, or personal profile fields in the tested scenarios.
- **SC-008**: Consumer integration documentation covers all ten required integration responsibilities and matches the implemented contract.

## Assumptions

- The existing access strategy remains in force: credentials carry only identity and expiration, while session validity and authorization data are checked authoritatively when current access is needed.
- Consumers are configured with their expected stable ApplicationId and reject a context for any other application.
- A small explicit context contract may evolve additively; no version-negotiation system is needed now.
- Consumers own business endpoint rules and use permissions as generic capabilities; role names are optional coarse-grained information.
- No distributed authorization cache, OAuth/OIDC service, business-domain model, or profile projection is introduced by this feature.
