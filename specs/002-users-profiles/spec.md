# Feature Specification: Global User Identity and Profile

**Feature Branch**: `002-users-profiles`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Implement the global user identity and reusable global profile model of the authentication service: administrative creation of a global user with a reusable profile, retrieval, permitted profile updates, activation/deactivation, unique normalized email, PostgreSQL/EF Core persistence, and ASP.NET Core Identity credential integration without coupling the Domain to Identity. Functional login remains out of scope."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Provision a global user identity and profile (Priority: P1)

As an administrator or system process provisioning access to the authentication
service, I can create a global user with a unique login email and a reusable
profile in a single operation, so that the identity exists consistently and is
ready for later features such as authentication and application memberships.

**Why this priority**: Nothing else in this feature, or in later features that
depend on it, is testable without a way to create the identity and profile.

**Independent Test**: Submit a creation request with a unique email, an initial
password, and required profile fields, then confirm a user and its associated
profile exist with a stable identifier and no duplicate identity is created for
the same normalized email.

**Acceptance Scenarios**:

1. **Given** a unique, validly formatted email, an initial password, and
   required profile fields, **When** a user-creation request is submitted,
   **Then** the user and its profile are persisted together, a stable
   identifier is returned, and no password or credential internal is exposed
   in the response.
2. **Given** an email that differs from an existing user's login email only by
   case, surrounding whitespace, or other normalization differences, **When**
   a creation request is submitted, **Then** the request is rejected as a
   duplicate identity and no new user is created.
3. **Given** a creation request missing a required field, using an invalid
   email format, or supplying a password that does not meet the credential
   policy, **When** the request is submitted, **Then** it is rejected with
   validation feedback and no partial user or profile record is persisted.
4. **Given** two creation requests for the same normalized email are submitted
   at the same time, **When** both are processed, **Then** exactly one user is
   created and the other request is rejected as a duplicate.

---

### User Story 2 - Retrieve a user and profile (Priority: P1)

As a caller of the service (administrative or a later feature), I can retrieve
a previously created user by its stable identifier and see both identity and
profile information, so that the stored identity can be confirmed and reused.

**Why this priority**: Creation is unverifiable and unusable by later features
without a reliable way to read back the resulting identity and profile.

**Independent Test**: Retrieve a known user by identifier and confirm the
response exposes identity and profile fields while withholding credential
internals; retrieve an unknown identifier and confirm a safe not-found result.

**Acceptance Scenarios**:

1. **Given** an existing user, **When** it is retrieved by its stable
   identifier, **Then** the response includes the identifier, login email,
   active/inactive state, timestamps, and profile fields, and excludes
   password hashes, security stamps, and other internal credential details.
2. **Given** an identifier that does not correspond to any user, **When** a
   retrieval is attempted, **Then** a not-found result is returned without
   revealing persistence internals.

---

### User Story 3 - Update permitted profile information (Priority: P2)

As an administrator or the service acting on a user's behalf, I can update a
user's reusable profile fields without affecting their login identity or
credentials, so that profile information can be kept current.

**Why this priority**: Profile accuracy matters, but creation and retrieval
must exist first for an update to have any effect to verify.

**Independent Test**: Update an existing user's profile fields with valid
values and confirm only the profile changes; attempt to change the login
email through the same operation and confirm it is rejected.

**Acceptance Scenarios**:

1. **Given** an existing user, **When** permitted profile fields (first name,
   last name, display name, phone number, avatar reference) are updated with
   valid values, **Then** the profile reflects the new values, its update
   timestamp advances, and the login email and credential state remain
   unchanged.
2. **Given** an update request with a field value exceeding its defined limit
   or otherwise invalid, **When** the request is submitted, **Then** it is
   rejected with validation feedback and the existing profile values remain
   unchanged.
3. **Given** an update request attempts to change the login email, **When** it
   is submitted, **Then** the request is rejected because login email is
   immutable through profile-update operations in this feature.

---

### User Story 4 - Activate and deactivate a user (Priority: P2)

As an administrator, I can deactivate a user so they become ineligible for
future authentication without losing their historical record, and reactivate
a user when access should resume.

**Why this priority**: Lifecycle control over an existing identity is
important but depends on a user already existing and being retrievable.

**Independent Test**: Deactivate an active user and confirm its state changes
while the record and profile remain intact and retrievable; reactivate it and
confirm it returns to active state.

**Acceptance Scenarios**:

1. **Given** an active user, **When** deactivation is requested, **Then** the
   user's state becomes inactive, its record and profile are preserved without
   physical deletion, and the user remains retrievable in its inactive state.
2. **Given** an inactive user, **When** reactivation is requested, **Then**
   the user's state returns to active.
3. **Given** a user already in the requested state (already active or already
   inactive), **When** the same transition is requested again, **Then** the
   operation does not produce an inconsistent or duplicated state change.

### Edge Cases

- What happens when two creation requests race for the same normalized email?
  Exactly one succeeds; the other is rejected as a duplicate, even under
  concurrent persistence attempts.
- How does the system handle a creation request with an invalid email format,
  a missing required profile field, or a password that fails the credential
  policy? The request is rejected with validation feedback; no partial user
  or profile record is persisted.
- What happens if credential creation succeeds but the domain user or profile
  cannot be persisted, or vice versa? No partial user/profile state is left;
  the overall creation operation fails consistently.
- How does the system handle retrieval of a nonexistent user identifier? A
  not-found result is returned without revealing persistence internals.
- What happens when a profile-update request attempts to change the login
  email? The request is rejected; email remains immutable through this
  operation in this feature.
- How does the system handle deactivating an already-inactive user, or
  reactivating an already-active user? The operation remains safe and does
  not create an invalid or duplicated state transition.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST introduce a global User identity concept with a
  stable unique identifier, a required login email, a normalized email
  representation, an active/inactive state, a creation timestamp, and an
  update timestamp, independent of any single consuming application.
- **FR-002**: The domain User type MUST NOT inherit from or otherwise depend
  on ASP.NET Core Identity types; Identity integration MUST remain an
  Infrastructure concern.
- **FR-003**: The system MUST introduce a UserProfile concept, conceptually
  separate from login identity and credentials, holding first name, last
  name, display name, an optional phone number, an optional avatar reference,
  a creation timestamp, and an update timestamp.
- **FR-004**: Each user MUST be associated with exactly one global profile;
  the system MUST NOT allow a user to have multiple competing profiles.
- **FR-005**: The global profile MUST NOT contain application-specific
  business information (for example employee, branch, customer, or
  reservation identifiers, department-specific attributes, application
  preferences, or business permissions); such data belongs to the consuming
  application.
- **FR-006**: Login email MUST be required, validated as an email address,
  and normalized consistently; case or other normalization differences MUST
  NOT allow two users to represent the same login identity.
- **FR-007**: Email uniqueness MUST be enforced authoritatively at the
  persistence level, in addition to application-level validation, so that
  concurrent creation requests for the same normalized email cannot both
  succeed.
- **FR-008**: The system MUST provide a user-creation operation that
  validates input, normalizes the email, rejects an already-existing
  normalized email, creates the required Identity credential representation,
  creates the domain user, creates the associated profile, and persists the
  resulting state consistently without leaving a partial user or profile
  record if any step fails.
- **FR-009**: User creation MUST accept an initial password supplied by the
  creating caller; ASP.NET Core Identity MUST perform credential hashing and
  storage. The system MUST NOT implement custom password hashing, and MUST
  NOT expose password hashes or other credential internals through Domain
  types, DTOs, API responses, or logs.
- **FR-010**: User creation in this feature MUST be treated as an
  administrative or system-provisioning operation; the feature MUST NOT
  expose public, unauthenticated self-registration.
- **FR-011**: The system MUST provide retrieval of a user, including its
  associated profile, by stable identifier. Responses MUST exclude password
  hashes, security stamps, internal credential tokens, and other sensitive
  Identity infrastructure fields.
- **FR-012**: The system MUST support updating the permitted profile fields
  (first name, last name, display name, phone number, avatar reference)
  without altering credential infrastructure.
- **FR-013**: Login email MUST remain immutable through profile-update
  operations in this feature; changing login email, if ever required, MUST be
  treated as a separate, explicitly validated identity operation outside this
  feature's scope.
- **FR-014**: The system MUST support activating and deactivating a user and
  querying its current state. Deactivation MUST preserve the user and profile
  records without physical deletion and MUST mark the identity ineligible for
  future authentication; reactivation MUST restore the active state.
- **FR-015**: The system MUST NOT implement authentication lockout as a
  domain state in this feature; lockout remains part of the future
  authentication feature and Identity infrastructure.
- **FR-016**: User and profile data MUST persist using the existing
  PostgreSQL 17 / EF Core foundation. Schema changes MUST be introduced
  through a version-controlled EF Core migration containing only the schema
  required for users, profiles, and necessary Identity integration; the
  migration MUST NOT introduce application, membership, role, permission, or
  session tables.
- **FR-017**: Domain MUST NOT depend on EF Core, ASP.NET Core, ASP.NET Core
  Identity, or PostgreSQL-specific APIs; Application MUST NOT depend on
  concrete persistence implementations; Infrastructure MUST implement the
  required persistence and Identity adapters.
- **FR-018**: The relationship between the domain user, its Identity
  credential representation, and its profile MUST remain stable and
  reconcilable; the system MUST avoid creating duplicate, independently
  generated identities that cannot be associated with one another, and MUST
  NOT expose Identity's persistence model as the public domain model.
- **FR-019**: External input for user creation and profile update MUST be
  validated for required fields, email format, explicit field length limits,
  and reasonable field values. Validation MUST NOT rely solely on database or
  persistence exceptions as the normal rejection path.
- **FR-020**: The API MUST provide consistent failure responses for invalid
  input, duplicate email, user not found, and invalid state transitions.
  Responses MUST NOT reveal password hashes, database internals, SQL,
  Identity internal details, stack traces, or sensitive configuration.
- **FR-021**: Logging for user and profile operations MUST use the existing
  structured logging foundation and MUST NOT record passwords, password
  hashes, Identity security stamps, secrets, or full request payloads that may
  contain credentials.
- **FR-022**: This feature MUST NOT introduce functional login, logout,
  tokens, sessions, application registration or memberships, application-
  scoped roles or permissions, password recovery or reset, email confirmation,
  MFA, social login, OAuth 2.0/OpenID Connect, an administrative frontend,
  file upload or avatar storage, or business-specific profile data.

### Key Entities

- **User**: The global identity managed by the authentication service.
  Represents a person independent of any consuming application; carries a
  stable identifier, login email, normalized email, active/inactive state,
  and creation/update timestamps. Associated with exactly one UserProfile and
  with an Infrastructure-level Identity credential representation that it
  does not depend on directly.
- **UserProfile**: Reusable, application-agnostic information about a user
  that consuming applications may read. Carries first name, last name,
  display name, optional phone number, optional avatar reference, and
  creation/update timestamps. Belongs to exactly one User.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A global user with a unique email and a complete profile can be
  created successfully whenever the submitted input is valid.
- **SC-002**: Login emails that differ only by case or other normalization
  differences are always treated as the same identity; zero duplicate
  identities are created, including under concurrent creation attempts.
- **SC-003**: A created user's identifier, login email, active state, and
  profile fields are retrievable immediately after creation.
- **SC-004**: Updating permitted profile fields changes only profile data;
  the login email and account identifier remain unchanged across 100% of
  update operations.
- **SC-005**: A deactivated user's record and profile remain intact and
  retrievable; zero physical deletions occur as a result of deactivation.
- **SC-006**: A previously deactivated user can be reactivated and returns to
  active state without loss of profile history.
- **SC-007**: Zero API responses or log entries expose password hashes,
  security stamps, or other sensitive Identity internals across creation,
  retrieval, update, and state-change operations.
- **SC-008**: Zero application-specific business attributes exist in the
  global profile schema.
- **SC-009**: The user/profile schema is introduced through a reviewable EF
  Core migration that applies successfully against the PostgreSQL 17
  development container and can be reapplied without schema drift.
- **SC-010**: A maintainer can begin `003-applications-memberships` using the
  established user/profile foundation without restructuring Domain,
  Application, or the persisted schema.

## Assumptions

- User creation in this feature is an administrative or system-provisioning
  operation; public self-registration is not introduced here and remains a
  decision for a future feature, consistent with the constitution's
  undecided-until-specified list.
- User creation requires an initial password supplied by the creating caller.
  Because password recovery/reset is explicitly out of scope for this
  feature, this is the only mechanism this feature offers to leave a user
  ready for a future authentication feature; ASP.NET Core Identity performs
  all hashing and storage.
- Login email is immutable through profile-update operations in this
  feature. A dedicated email-change capability, if ever needed, is deferred
  to a later, separately validated identity operation.
- First name, last name, and display name are required at user creation;
  phone number and avatar reference are optional, consistent with the
  feature description.
- Phone number is stored as optional contact information without uniqueness
  enforcement or carrier-specific normalization in this feature; only basic
  format and length validation apply.
- The avatar reference is an opaque, nullable value (for example an
  identifier or URL) with no validation of an underlying file's existence;
  physical file storage belongs to a later, separate feature.
- The domain User's stable identifier and the Infrastructure-level Identity
  credential representation are distinct but kept reconcilable; neither the
  Application layer nor API contracts expose Identity's persistence model
  directly.
