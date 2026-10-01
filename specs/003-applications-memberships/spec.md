# Feature Specification: Applications and Memberships

**Feature Branch**: `003-applications-memberships`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature 003-applications-memberships for the reusable generic Authentication and Authorization API."

## Clarifications

### Session 2026-10-01

- Q: When a user or application is deactivated, should its existing active memberships be automatically changed to inactive? → A: Keep existing membership states unchanged; deactivation only makes them ineligible until the user/application is active again.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register and manage a consuming application (Priority: P1)

As an administrator or trusted system process, I can register a consuming
application with a stable code and manage whether it is active, so that the
authentication service can establish an explicit context without modelling that
application's business domain.

**Why this priority**: Application context is the prerequisite for every
application-specific relationship and later access decision.

**Independent Test**: Register an application with a valid, unique code;
retrieve it by identifier and code; deactivate and reactivate it; then confirm
the same persisted application remains queryable with its current state.

**Acceptance Scenarios**:

1. **Given** a valid unused application code and display name, **When** an
   application is registered, **Then** a record is created with a stable
   identifier, the supplied stable code, an active state, and creation and
   update timestamps.
2. **Given** an application code already assigned to another application,
   **When** registration is requested using that code, **Then** the request is
   rejected and no duplicate application is created, including when requests
   arrive concurrently.
3. **Given** a registered application, **When** it is deactivated or later
   activated, **Then** its state changes while its identifier, code, and
   historical record remain intact and retrievable.
4. **Given** an application already in the requested state, **When** the same
   state transition is requested again, **Then** the operation succeeds
   idempotently without creating another application or changing unrelated
   data.

---

### User Story 2 - Associate a global user with an application (Priority: P1)

As an administrator or trusted system process, I can associate an existing
global user with an active consuming application, so that participation is
explicit rather than inferred from the user's global identity.

**Why this priority**: Explicit membership is the foundation of strict
application isolation and future application-scoped access decisions.

**Independent Test**: Create an active global user and application, create a
membership, retrieve it by that user and application, and confirm the
relationship is active and unique.

**Acceptance Scenarios**:

1. **Given** an existing active user and an existing active application,
   **When** a membership is created, **Then** an active membership with a
   stable identifier, both relationship identifiers, and timestamps is
   persisted.
2. **Given** a nonexistent user or application identifier, **When** a
   membership is requested, **Then** the request is rejected as not found and
   no membership is created.
3. **Given** an existing membership for the same user and application,
   **When** another membership is requested for that pair, **Then** the
   request is rejected and exactly one relationship remains, including under
   concurrent requests.
4. **Given** an inactive application, **When** a new membership is requested,
   **Then** the request is rejected and no relationship is created.
5. **Given** an inactive user and an active application, **When** a new
   membership is requested, **Then** an inactive membership may be recorded
   to preserve the relationship, but no active membership is created.

---

### User Story 3 - Manage membership eligibility independently (Priority: P2)

As an administrator or trusted system process, I can activate or deactivate a
user's membership in one application and query its state, so that eligibility
is controlled per application without changing the global user, the
application, or memberships in other contexts.

**Why this priority**: Independent lifecycle control is necessary to enforce
isolation after the relationship has been established.

**Independent Test**: Give one global user memberships in two applications,
change one membership's state, and retrieve both relationships to confirm only
the selected one changed.

**Acceptance Scenarios**:

1. **Given** an inactive membership whose user and application are active,
   **When** activation is requested, **Then** that membership becomes active.
2. **Given** a membership whose user or application is inactive, **When**
   activation is requested, **Then** the request is rejected and the
   membership state remains unchanged; an already-inactive membership remains
   inactive and a previously active membership remains active but ineligible.
3. **Given** an active membership, **When** deactivation is requested,
   **Then** it becomes inactive while the user, application, and historical
   membership record remain unchanged.
4. **Given** a user with memberships in two applications, **When** either
   membership changes state, **Then** the other membership retains its prior
   state and a query for one application never treats membership in the other
   application as participation.
5. **Given** a membership already in the requested state, **When** the same
   state transition is requested again, **Then** the operation succeeds
   idempotently without creating a duplicate relationship.

### Edge Cases

- A request with a missing, malformed, or over-limit application code, name,
  user identifier, application identifier, or state change is rejected before
  any record changes.
- An inactive application remains retrievable and retains its memberships;
  deactivation never deletes users or memberships and does not change their
  membership states.
- Deactivating a user does not change any membership state. A membership is
  eligible only when its own state, its user's state, and its application's
  state are all active.
- Deactivating one membership never deactivates its global user, its
  application, or any other membership.
- A request for an unknown application, user, or membership returns a safe
  not-found result without exposing internal storage or diagnostic details.
- Two concurrent attempts to register one code or create one user/application
  relationship leave exactly one corresponding record.
- Requests to list or retrieve memberships must identify the relevant user or
  application context; a global user identifier alone is never evidence of
  access to every application.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST represent a consuming Application independently
  of the business domain it serves, with a stable unique identifier, required
  unique application code, required display name, active/inactive state, and
  creation and update timestamps.
- **FR-002**: Application code MUST be a stable machine-readable identifier:
  3 to 64 lowercase ASCII letters, digits, and single hyphens; it MUST begin
  and end with a letter or digit. Input is normalized by trimming surrounding
  whitespace and converting letters to lowercase before uniqueness is tested.
- **FR-003**: The application code MUST not be derived from the display name
  and MUST remain unchanged by application updates or state changes. This
  feature provides no code-change operation.
- **FR-004**: Application display name MUST be required, contain 1 to 200
  non-whitespace characters after trimming, and remain unchanged by the
  lifecycle operations in this feature. This feature provides no name-change
  operation.
- **FR-005**: The system MUST provide operations to create an application;
  retrieve it by stable identifier or application code; list registered
  applications with their current state; and activate or deactivate it.
- **FR-006**: New applications MUST be active when created. Application
  activation and deactivation MUST be idempotent; deactivation MUST preserve
  the application and all historical relationships without physical deletion.
- **FR-007**: The system MUST represent ApplicationMembership as a distinct
  relationship between exactly one existing global User and exactly one
  existing Application, with a stable unique identifier, active/inactive
  state, and creation and update timestamps.
- **FR-008**: Each user/application pair MUST have at most one membership.
  A user MAY have memberships in multiple applications, and an application
  MAY have memberships for multiple users. Every membership's state MUST be
  independent from every other membership.
- **FR-009**: The system MUST provide operations to create a membership;
  retrieve it by user and application; determine whether the user has an
  active membership in the specified application; retrieve memberships for a
  specified user; retrieve memberships for a specified application; and
  activate or deactivate a specified membership.
- **FR-010**: Membership creation MUST verify that the supplied user and
  application exist. It MUST reject creation for an inactive application. For
  an active application, it MUST create an active membership for an active
  user and an inactive membership for an inactive user; it MUST never create
  an active membership for an inactive user.
- **FR-011**: Activating a membership MUST require that the membership,
  associated user, and associated application all exist and that both user and
  application are active. A rejected activation MUST leave the membership
  state unchanged. Repeating activation of an already-active membership
  succeeds idempotently only while the user and application are active.
- **FR-012**: Deactivating a membership MUST preserve the relationship and
  MUST NOT change the global user's state, the application's state, or any
  membership in another application. Repeating deactivation succeeds
  idempotently.
- **FR-012a**: Deactivating a user or application MUST NOT automatically
  change the state of any existing membership. Membership eligibility requires
  the membership, its user, and its application all to be active; reactivating
  a user or application restores only that entity's state and does not alter
  its memberships.
- **FR-013**: Application code uniqueness and user/application membership
  uniqueness MUST be enforced authoritatively by persistent storage as well
  as checked by the service, so concurrent requests cannot create duplicates.
  Membership storage MUST require valid user and application relationships so
  that no membership can reference a nonexistent record.
- **FR-014**: Every application-context operation MUST explicitly identify its
  relevant application. The system MUST NOT infer participation in one
  application from the user's identity or membership in another application.
- **FR-015**: External input MUST be validated for required values, identifier
  format, application-code format and length, display-name length, and
  reasonable request limits. Every collection listing MUST accept an optional
  opaque cursor and a `limit` from 1 through 100, return no more than that
  limit, and reject an invalid limit. Validation failures MUST not partially
  change state.
- **FR-016**: The system MUST return consistent safe outcomes for invalid
  input, application not found, user not found, membership not found,
  duplicate application code, duplicate membership, and disallowed activation
  due to an inactive user or application. Outcomes MUST not reveal storage
  statements, stack traces, internal persistence state, or sensitive
  configuration.
- **FR-017**: Relevant application and membership operations MAY be
  structurally logged for diagnosis using identifiers and outcome categories,
  but logs MUST avoid unnecessary personal data and full request payloads.
- **FR-018**: Application and membership concepts MUST remain independent of
  delivery, identity, and storage mechanisms. The feature MUST preserve the
  existing project architecture and use its established persistence migration
  process for schema changes.
- **FR-019**: Management operations in this feature are for trusted
  administrative or system callers. The feature MUST NOT introduce a
  substitute authorization model, hard-coded business roles, or caller login
  behavior.
- **FR-020**: This feature MUST NOT introduce application-specific profile
  data, business-domain data, roles, permissions, role assignments, login,
  logout, credentials or tokens, sessions, password recovery/reset, MFA,
  OAuth/OpenID Connect, social login, an administrative frontend, file
  uploads, or placeholder versions of those capabilities.

### Key Entities

- **Application**: A registered system that consumes the authentication and
  authorization service; it supplies context but has no model of that
  system's business data.
- **ApplicationMembership**: A durable, stateful relationship that determines
  whether one global user participates in one explicitly identified
  application.
- **User**: The existing global identity. It is referenced by memberships but
  is not duplicated or made application-specific by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid unused application registration produces one retrievable
  application with a stable identifier, stable code, and active state in 100%
  of validation trials.
- **SC-002**: In 100% of duplicate-code and duplicate-membership trials,
  including paired concurrent submissions, the system retains exactly one
  record for the intended code or user/application pair.
- **SC-003**: In 100% of lifecycle trials, activation or deactivation changes
  only the selected application or membership and preserves its historical
  record.
- **SC-004**: In 100% of multi-application isolation trials, a user's active
  membership in one application does not report that user as an active member
  of any other application without a separate active membership there.
- **SC-005**: In 100% of activation trials involving an inactive user or
  inactive application, the membership state remains unchanged and no
  unrelated record changes.
- **SC-006**: All defined invalid, duplicate, and not-found cases produce a
  consistent safe outcome with no internal diagnostic or sensitive data in the
  response.

## Assumptions

- The global User model and its active/inactive lifecycle from feature 002 are
  available before memberships are created.
- Application codes are immutable after registration. A future code-migration
  capability, if needed, requires a separate explicit specification because
  consuming systems may rely on the code as a stable identifier.
- Application names are intentionally not mutable in this feature; a future
  display-name update capability can be specified without changing code or
  membership semantics.
- A new membership for an inactive application is always rejected. A new
  membership for an inactive user is allowed only as an inactive historical
  relationship; it requires a later successful activation after both the user
  and application are active.
- Existing memberships remain stored when a user or application becomes
  inactive, and their membership state is not changed automatically. A later
  access decision must require active user, active application, and active
  membership. This feature does not define future session or authentication
  consequences beyond preventing membership activation while either is
  inactive.
- Listing is limited to operating and validating this feature: applications,
  memberships of one identified user, and memberships of one identified
  application. It is not a reporting or broad search capability.
